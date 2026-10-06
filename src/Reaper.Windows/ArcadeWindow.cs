using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Reaper.Core;

namespace Reaper.Windows;

public sealed class ArcadeWindow : Window
{
    private readonly WebView2 web = new();
    private readonly RawInput raw = new();
    private readonly ArcadePicker picker;
    private readonly bool remoteSession = GetSystemMetrics(0x1000) != 0 || (Environment.GetEnvironmentVariable("SESSIONNAME")?.StartsWith("RDP-", StringComparison.OrdinalIgnoreCase) ?? false);
    private List<Installation> installations = [];
    private readonly GunSerial serial = new();
    private readonly GameSession session = new();
    private readonly LibraryStore store;
    private LibraryState state;
    private readonly Dictionary<string, ExitGesture> gestures = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private int? bindPlayer;
    private string? bindMouse;
    private bool ready, busy;
    private long lastMove;
    private HwndSource? source;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string logPath;
    [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out ScreenPoint point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    public ArcadeWindow()
    {
        picker = new ArcadePicker(Send);
        Title = "Reaper Arcade"; Width = 1280; Height = 800; MinWidth = 900; MinHeight = 620; Background = new SolidColorBrush(Color.FromRgb(12, 15, 21)); Content = web;
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade");
        Directory.CreateDirectory(data); store = new(data); state = store.Load(); logPath = Path.Combine(data, "activity.log");
        SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source.AddHook(Hook); raw.Register(new WindowInteropHelper(this).Handle); };
        Loaded += async (_, _) => await Initialize();
        raw.Packet += Input; raw.DevicesChanged += () => SendState();
        session.Changed += status => Dispatcher.Invoke(() => { Send("session", new { status }); if (status == "running") WindowState = WindowState.Minimized; else { WindowState = WindowState.Normal; SetFullscreen(state.Settings.Fullscreen); Activate(); } });
        timer.Tick += async (_, _) => { if (session.Active && gestures.Values.Any(g => g.Ready(DateTimeOffset.UtcNow))) { foreach (var g in gestures.Values) g.Reset(); await session.End(); } };
        timer.Start();
        Closing += (_, e) => { if (session.Active) { e.Cancel = true; _ = session.End(); Send("notice", new { message = "Das Spiel wird beendet. Danach kannst du die App schließen." }); } };
        Closed += (_, _) => { timer.Stop(); raw.Dispose(); source?.RemoveHook(Hook); http.Dispose(); };
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled) { raw.Message(message, lParam); return 0; }
    private async Task Initialize()
    {
        try
        {
            string webDirectory = Path.Combine(AppContext.BaseDirectory, "web");
            if (!System.IO.File.Exists(Path.Combine(webDirectory, "index.html"))) throw new IOException("Die Oberfläche fehlt. Bitte das vollständige Windows-Paket entpacken.");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(store.DirectoryPath, "browser"));
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("reaper.local", webDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
            Directory.CreateDirectory(Path.Combine(store.DirectoryPath, "covers"));
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("covers.reaper.local", Path.Combine(store.DirectoryPath, "covers"), CoreWebView2HostResourceAccessKind.DenyCors);
            Directory.CreateDirectory(Path.Combine(store.DirectoryPath, "media"));
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("media.reaper.local", Path.Combine(store.DirectoryPath, "media"), CoreWebView2HostResourceAccessKind.DenyCors);
            if (Directory.Exists(@"C:\Lightgun\Media"))
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("collection.reaper.local", @"C:\Lightgun\Media", CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            web.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            web.CoreWebView2.NavigationCompleted += (_, _) => web.Focus();
            web.CoreWebView2.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://reaper.local/", StringComparison.Ordinal)) e.Cancel = true; };
            web.CoreWebView2.WebMessageReceived += async (_, e) =>
            {
                if (!e.Source.StartsWith("https://reaper.local/", StringComparison.Ordinal)) return;
                try { using var doc = JsonDocument.Parse(e.WebMessageAsJson); await Handle(doc.RootElement.Clone()); }
                catch (Exception error) { Log("error: " + error.Message); Send("error", new { message = error.Message }); }
            };
            SetFullscreen(state.Settings.Fullscreen); web.Source = new Uri("https://reaper.local/index.html");
        }
        catch (WebView2RuntimeNotFoundException) { MessageBox.Show("Für die Oberfläche wird Microsoft Edge WebView2 Runtime benötigt.\nDownload: https://developer.microsoft.com/microsoft-edge/webview2/", Title); Close(); }
    }
    private void SetFullscreen(bool enabled)
    { WindowState = WindowState.Normal; WindowStyle = enabled ? WindowStyle.None : WindowStyle.SingleBorderWindow; ResizeMode = enabled ? ResizeMode.NoResize : ResizeMode.CanResize; if (enabled) WindowState = WindowState.Maximized; }
    private void Send(string type, object payload)
    { if (ready && web.CoreWebView2 is not null) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, payload }, JsonDefaults.Options)); }
    private void SendState()
    {
        if (web.CoreWebView2 is not null && Directory.Exists(@"C:\Lightgun\Media"))
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("collection.reaper.local", @"C:\Lightgun\Media", CoreWebView2HostResourceAccessKind.DenyCors);
        foreach (var binding in state.Bindings.Where(b => !raw.Devices.Any(d => d.Id == b.KeyboardId)))
            if (binding.KeyboardId is not null && gestures.TryGetValue(binding.KeyboardId, out var gesture)) gesture.Reset();
        Send("state", new
        {
            games = state.Games.Select(g => g with { Cover = MediaUrl(g.Cover, true), PreviewVideo = MediaUrl(g.PreviewVideo), Screenshot = MediaUrl(g.Screenshot), Logo = MediaUrl(g.Logo) }),
            bindings = state.Bindings,
            devices = raw.Devices,
            ports = SerialPort.GetPortNames().OrderBy(x => x),
            settings = new { state.Settings.StartWithWindows, state.Settings.Fullscreen, hasCoverKey = state.Settings.CoverKey is not null },
            bindingStage = bindPlayer is null ? null : new { player = bindPlayer, stage = bindMouse is null ? "trigger" : "start" },
            version = "0.3.0",
            remoteSession,
            installations,
            calibrationTool = state.Settings.CalibrationTool is null ? null : Path.GetFileName(state.Settings.CalibrationTool),
            native = true
        });
    }
    private string? MediaUrl(string? file, bool cover = false)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        if (!Path.IsPathRooted(file)) file = Path.Combine(store.DirectoryPath, file);
        return (cover ? MediaPaths.Url(file, Path.Combine(store.DirectoryPath, "covers"), "covers.reaper.local") : null)
            ?? MediaPaths.Url(file, Path.Combine(store.DirectoryPath, "media"), "media.reaper.local")
            ?? MediaPaths.Url(file, @"C:\Lightgun\Media", "collection.reaper.local");
    }
    private void Persist() { store.Save(state); SendState(); }
    private void Log(string message) { System.IO.File.AppendAllText(logPath, DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine); }
    private void Input(RawPacket packet)
    {
        if (bindPlayer.HasValue && !session.Active)
        {
            if (bindMouse is null && packet.Kind == "mouse" && (packet.Buttons & 1) != 0)
            { bindMouse = packet.DeviceId; SendState(); return; }
            if (bindMouse is not null && packet.Kind == "keyboard" && packet.Down && packet.Key == (bindPlayer.Value == 1 ? 0x31 : 0x32))
            {
                int player = bindPlayer.Value;
                if (state.Bindings.Any(b => b.Player != player && (b.MouseId == bindMouse || b.KeyboardId == packet.DeviceId)))
                { Send("error", new { message = "Dieser Eingang ist bereits einem anderen Spieler zugeordnet. Bitte dessen Gun verwenden oder Zuordnung entfernen." }); return; }
                state.Bindings.RemoveAll(b => b.Player == player);
                state.Bindings.Add(new(player, bindMouse, packet.DeviceId)); bindMouse = null; bindPlayer = null; Persist(); return;
            }
        }
        if (session.Active && packet.Kind == "keyboard" && packet.Key == 0x7B && packet.Down) { _ = session.End(); return; }
        var binding = state.Bindings.FirstOrDefault(b => b.MouseId == packet.DeviceId || b.KeyboardId == packet.DeviceId);
        if (packet.Kind == "keyboard" && binding is not null)
        {
            if (!gestures.TryGetValue(packet.DeviceId, out var gesture)) gestures[packet.DeviceId] = gesture = new();
            gesture.Key(packet.Key, packet.Down, DateTimeOffset.UtcNow);
        }
        if (session.Active || remoteSession) return;
        long now = Environment.TickCount64;
        if (packet.Kind == "mouse" && packet.Buttons == 0 && now - lastMove < 30) return;
        if (packet.Kind == "mouse") lastMove = now;
        // No global shared cursor is used for absolute lightgun packets.
        if (packet.Kind == "mouse")
        {
            double sx, sy;
            if (packet.Absolute)
            {
                int left = packet.VirtualDesktop ? GetSystemMetrics(76) : 0, top = packet.VirtualDesktop ? GetSystemMetrics(77) : 0;
                int width = GetSystemMetrics(packet.VirtualDesktop ? 78 : 0), height = GetSystemMetrics(packet.VirtualDesktop ? 79 : 1);
                sx = left + packet.X / 65535.0 * (width - 1); sy = top + packet.Y / 65535.0 * (height - 1);
            }
            else { GetCursorPos(out var point); sx = point.X; sy = point.Y; }
            var client = web.PointFromScreen(new Point(sx, sy));
            Send("input", new { packet.DeviceId, kind = packet.Kind, player = binding?.Player ?? 0, x = client.X / Math.Max(1, web.ActualWidth), y = client.Y / Math.Max(1, web.ActualHeight), packet.Buttons });
        }
        else Send("input", new { packet.DeviceId, kind = packet.Kind, player = binding?.Player ?? 0, packet.Key, packet.Down });
    }
    private static string? Folder(string title) { var picker = new OpenFolderDialog { Title = title }; return picker.ShowDialog() == true ? picker.FolderName : null; }
    private static string? File(string title, string filter = "Windows-Anwendung|*.exe") { var picker = new OpenFileDialog { Title = title, Filter = filter }; return picker.ShowDialog() == true ? picker.FileName : null; }
    private async Task Handle(JsonElement message)
    {
        string type = message.GetProperty("type").GetString() ?? "";
        JsonElement payload = message.TryGetProperty("payload", out var p) ? p : default;
        string Str(string name) => payload.GetProperty(name).GetString() ?? "";
        int Player() => payload.GetProperty("player").GetInt32() is var n && n is >= 1 and <= 2 ? n : throw new ArgumentException("Ungültiger Spieler.");
        GameEntry Game() => state.Games.FirstOrDefault(g => g.Id == Str("id")) ?? throw new ArgumentException("Das Spiel existiert nicht mehr.");
        if (type == "browse-path") { picker.Browse(Str("path")); return; }
        if (type == "choose-path") { picker.Choose(Str("path")); return; }
        if (type == "cancel-picker") { picker.Cancel(); return; }
        if (picker.Active) throw new InvalidOperationException("Bitte zuerst die Dateiauswahl schließen.");
        if (type == "ready") { ready = true; SendState(); return; }
        if (type == "end-game") { await session.End(); return; }
        if (type == "cancel-bind") { bindPlayer = null; bindMouse = null; SendState(); return; }
        if (session.Active && type != "fullscreen") throw new InvalidOperationException("Bitte zuerst das laufende Spiel beenden.");
        if (busy) throw new InvalidOperationException("Die laufende Aktion wird noch abgeschlossen.");
        switch (type)
        {
            case "scan-installations":
                {
                    busy = true; Send("busy", new { message = "Bekannte Spieleordner werden durchsucht …" });
                    try
                    {
                        var roots = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                        {
                            roots.Add(drive.RootDirectory.FullName);
                            foreach (var folder in new[] { "Games", "Arcade", "Emulators", "RetroBat", "LaunchBox", "TeknoParrot" }) roots.Add(Path.Combine(drive.RootDirectory.FullName, folder));
                        }
                        installations = await Task.Run(() => InstallationFinder.Find(roots)); SendState();
                        Send("notice", new { message = installations.Count == 0 ? "In den üblichen Ordnern nichts gefunden. Du kannst den richtigen Ordner über die großen Schaltflächen wählen." : $"{installations.Count} Programme gefunden. Wähle die gewünschte Installation." });
                    }
                    finally { busy = false; Send("busy", new { message = "" }); }
                    break;
                }
            case "set-calibration":
                {
                    string? exe = await picker.Open("Hersteller-Kalibrierprogramm auswählen", false); if (exe is null) return;
                    state = state with { Settings = state.Settings with { CalibrationTool = exe } }; Persist(); break;
                }
            case "run-calibration":
                {
                    if (remoteSession) throw new InvalidOperationException("Die Gun am echten Bildschirm lokal kalibrieren. Remote Desktop verändert die Anzeige.");
                    string exe = state.Settings.CalibrationTool ?? throw new InvalidOperationException("Bitte zuerst das Herstellerprogramm auswählen.");
                    await session.Run(GameImporter.Pc(exe, "RS3-Kalibrierung"), store.DirectoryPath, state.Bindings); break;
                }
            case "validate-library":
                {
                    var warnings = new List<string>();
                    foreach (var game in state.Games.ToArray())
                    {
                        var checkedGame = LaunchRules.Validate(game);
                        state.Games[state.Games.IndexOf(game)] = checkedGame;
                        warnings.AddRange((checkedGame.SetupIssues ?? []).Select(issue => game.Title + ": " + issue));
                    }
                    Persist();
                    Send("import-result", new { count = state.Games.Count(g => g.Status != "needs-setup"), warnings, validation = true }); break;
                }
            case "import-collection":
                {
                    string? path = await picker.Open("spiele.json aus der Übergabe auswählen", false, [".json"]);
                    if (path is not null) await Import(() => CollectionImporter.Read(path));
                    break;
                }
            case "test-feedback":
                {
                    if (remoteSession) throw new InvalidOperationException("Feedback am lokalen Bildschirm testen und die Gun dabei in der Hand halten.");
                    var b = state.Bindings.First(x => x.Player == Player());
                    if (b.SerialPort is null) throw new InvalidOperationException("Bitte zuerst den Gun-COM-Port zuordnen.");
                    string effect = Str("effect");
                    string[] commands = effect switch { "recoil" => ["ZS", "Z5", "ZX"], "rumble" => ["ZS", "ZZ", "ZX"], "combined" => ["ZS", "Z5", "ZZ", "ZX"], _ => throw new ArgumentException("Unbekannter Feedbacktest.") };
                    busy = true;
                    try { await serial.Command(b.SerialPort, b.Player, commands); Send("notice", new { message = "Einzelimpuls gesendet. Stärke und Gefühl beurteilst du an der Gun; dies bestätigt noch kein Spielefeedback." }); }
                    finally { busy = false; }
                    break;
                }
            case "refresh": raw.Refresh(); SendState(); break;
            case "bind": bindPlayer = Player(); bindMouse = null; SendState(); break;
            case "unbind": state.Bindings.RemoveAll(b => b.Player == Player()); Persist(); break;
            case "favorite": { var game = Game(); state.Games[state.Games.IndexOf(game)] = game with { Favorite = !game.Favorite }; Persist(); break; }
            case "remove-game": state.Games.Remove(Game()); Persist(); break;
            case "set-port":
                {
                    int player = Player(); string port = Str("port");
                    if (port != "" && !SerialPort.GetPortNames().Contains(port, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Der Port ist nicht vorhanden.");
                    if (port != "" && state.Bindings.Any(b => b.Player != player && b.SerialPort == port)) throw new ArgumentException("Dieser Port ist bereits vergeben.");
                    int index = state.Bindings.FindIndex(b => b.Player == player); if (index < 0) throw new InvalidOperationException("Bitte zuerst die Gun zuordnen.");
                    state.Bindings[index] = state.Bindings[index] with { SerialPort = port == "" ? null : port }; Persist(); break;
                }
            case "test-serial":
                {
                    var binding = state.Bindings.First(b => b.Player == Player()); if (binding.SerialPort is null) throw new InvalidOperationException("Bitte den RS3-COM-Port auswählen.");
                    busy = true; try { int number = await serial.Command(binding.SerialPort, binding.Player); Send("notice", new { message = $"RS3 antwortet: Spieler {number}. Zielgenauigkeit und Recoil sind damit noch nicht geprüft." }); } finally { busy = false; }
                    break;
                }
            case "mouse-mode":
                {
                    var b = state.Bindings.First(b => b.Player == Player()); if (b.SerialPort is null) throw new InvalidOperationException("Bitte den RS3-COM-Port auswählen.");
                    busy = true; try { await serial.Command(b.SerialPort, b.Player, "ZS", "ZM", "ZW", "ZX"); Send("notice", new { message = "Mausmodus und 16:9 angefordert. Mit dem Zieltest prüfen." }); } finally { busy = false; }
                    break;
                }
            case "import-tekno":
                {
                    string? root = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("path", out var detected) && installations.Any(i => i.Kind == "tekno" && i.Path == detected.GetString()) ? Path.GetDirectoryName(detected.GetString()) : await picker.Open("TeknoParrot-Ordner auswählen", true); if (root is null) return;
                    await Import(() => GameImporter.TeknoParrot(root)); break;
                }
            case "import-mame":
                {
                    string? exe = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("path", out var detectedMame) && installations.Any(i => i.Kind == "mame" && i.Path == detectedMame.GetString()) ? detectedMame.GetString() : await picker.Open("MAME-Anwendung auswählen", false); if (exe is null) return;
                    string? roms = await picker.Open("MAME-ROM-Ordner auswählen", true); if (roms is null) return;
                    busy = true; Send("busy", new { message = "MAME-Spielekatalog wird gelesen …" });
                    try
                    {
                        var info = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                        info.ArgumentList.Add("-listxml"); using var process = Process.Start(info) ?? throw new IOException("MAME startet nicht.");
                        var error = process.StandardError.ReadToEndAsync();
                        var catalogTask = Task.Run(() => GameImporter.MameGunCatalog(process.StandardOutput.BaseStream));
                        try { await Task.WhenAll(catalogTask, process.WaitForExitAsync(), error).WaitAsync(TimeSpan.FromMinutes(2)); }
                        catch { if (!process.HasExited) process.Kill(); throw; }
                        if (process.ExitCode != 0) throw new IOException("MAME-Katalog konnte nicht gelesen werden: " + await error);
                        ApplyImport(GameImporter.Mame(exe, roms, await catalogTask));
                    }
                    finally { busy = false; Send("busy", new { message = "" }); }
                    break;
                }
            case "add-pc": { string? exe = await picker.Open("Lightgun-Spiel auswählen", false); if (exe is not null) ApplyImport(new([GameImporter.Pc(exe)], [])); break; }
            case "add-cover":
                {
                    var game = Game(); string? file = await picker.Open("Cover auswählen", false, [".png", ".jpg", ".jpeg", ".webp"]); if (file is null) return;
                    if (new FileInfo(file).Length > 8_000_000) throw new IOException("Das Cover ist größer als 8 MB.");
                    string dest = Path.Combine(store.DirectoryPath, "covers", game.Id + Path.GetExtension(file).ToLowerInvariant()); System.IO.File.Copy(file, dest, true);
                    state.Games[state.Games.IndexOf(game)] = game with { Cover = dest }; Persist(); break;
                }
            case "set-cover-key":
                {
                    string key = Str("key").Trim(); string? encrypted = key == "" ? null : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
                    state = state with { Settings = state.Settings with { CoverKey = encrypted } }; Persist(); break;
                }
            case "fetch-covers": await Covers(); break;
            case "fullscreen":
                { bool on = payload.GetProperty("enabled").GetBoolean(); state = state with { Settings = state.Settings with { Fullscreen = on } }; SetFullscreen(on); Persist(); break; }
            case "autostart":
                {
                    bool on = payload.GetProperty("enabled").GetBoolean();
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                    if (on) key.SetValue("ReaperArcade", "\"" + Environment.ProcessPath + "\""); else key.DeleteValue("ReaperArcade", false);
                    state = state with { Settings = state.Settings with { StartWithWindows = on } }; Persist(); break;
                }
            case "set-aspect":
                { var game = Game(); string aspect = Str("aspect"); if (aspect is not ("4:3" or "16:9")) throw new ArgumentException("Ungültiges Bildformat."); state.Games[state.Games.IndexOf(game)] = game with { Aspect = aspect }; Persist(); break; }
            case "mark-tested":
                { var game = Game(); state.Games[state.Games.IndexOf(game)] = game with { Status = "tested" }; Persist(); break; }
            case "launch": await Launch(Game()); break;
            case "close": Close(); break;
            case "export-diagnostics":
                {
                    var picker = new SaveFileDialog { Title = "Diagnose speichern", Filter = "JSON|*.json", FileName = "reaper-diagnose.json" }; if (picker.ShowDialog() != true) return;
                    System.IO.File.WriteAllText(picker.FileName, JsonSerializer.Serialize(new
                    {
                        version = "0.3.0",
            remoteSession,
            installations,
            calibrationTool = state.Settings.CalibrationTool is null ? null : Path.GetFileName(state.Settings.CalibrationTool),
                        os = Environment.OSVersion.ToString(),
                        devices = raw.Devices,
                        bindings = state.Bindings,
                        games = state.Games.Select(g => new { g.Title, g.Platform, g.Status, starterExists = System.IO.File.Exists(g.Executable) })
                    }, JsonDefaults.Options)); Send("notice", new { message = "Diagnose gespeichert. Sie enthält Gerätekennungen und keine API-Schlüssel." }); break;
                }
            default: throw new ArgumentException("Unbekannte Aktion.");
        }
        if ((type is "import-tekno" or "import-mame" or "add-pc") && state.Settings.CoverKey is not null)
            await Covers();
    }
    private async Task Import(Func<ImportResult> operation)
    { busy = true; Send("busy", new { message = "Spiele werden eingelesen …" }); try { ApplyImport(await Task.Run(operation)); } finally { busy = false; Send("busy", new { message = "" }); } }
    private void ApplyImport(ImportResult result)
    { LibraryStore.Merge(state, result.Games); Persist(); Log($"import: {result.Games.Count} entries"); Send("import-result", new { count = result.Games.Count, warnings = result.Warnings }); }
    private async Task Covers()
    {
        if (state.Settings.CoverKey is null) throw new InvalidOperationException("Bitte zuerst einen SteamGridDB-API-Schlüssel hinterlegen oder eigene Cover auswählen.");
        string key = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(state.Settings.CoverKey), null, DataProtectionScope.CurrentUser));
        busy = true; int found = 0; List<string> missing = []; Send("busy", new { message = "Cover werden ergänzt …" });
        try
        {
            var service = new CoverService(http, Path.Combine(store.DirectoryPath, "covers"));
            foreach (var game in state.Games.Where(g => g.Cover is null).ToArray())
            {
                string? cover = await service.Download(game.Title, key, CancellationToken.None);
                if (cover is null) missing.Add(game.Title); else { state.Games[state.Games.IndexOf(game)] = game with { Cover = cover }; found++; store.Save(state); }
                await Task.Delay(250);
            }
            Send("notice", new { message = $"{found} Cover ergänzt. {missing.Count} Titel ohne eindeutiges Cover." });
        }
        finally { busy = false; Send("busy", new { message = "" }); SendState(); }
    }
    private async Task Launch(GameEntry game)
    {
        _ = LaunchRules.Prepare(game); busy = true; var changed = new List<GunBinding>();
        try
        {
            foreach (var binding in state.Bindings.Where(b => b.SerialPort is not null))
            { await serial.Command(binding.SerialPort!, binding.Player, "ZS", "ZM", game.Aspect == "4:3" ? "ZN" : "ZW", "ZX"); changed.Add(binding); }
            foreach (var gesture in gestures.Values) gesture.Reset();
            state.Games[state.Games.IndexOf(game)] = game with { LastPlayed = DateTimeOffset.UtcNow }; store.Save(state); Log("launch: " + game.Title);
            await session.Run(game, store.DirectoryPath, state.Bindings.Where(b => raw.Devices.Any(d => d.Id == b.MouseId)));
        }
        finally
        {
            foreach (var binding in changed)
            { try { await serial.Command(binding.SerialPort!, binding.Player, "ZS", "ZM", "ZW", "ZX"); } catch (Exception e) { Log("restore: " + e.Message); Send("error", new { message = "Menümodus konnte nicht wiederhergestellt werden: " + e.Message }); } }
            busy = false; SendState();
        }
    }
}
