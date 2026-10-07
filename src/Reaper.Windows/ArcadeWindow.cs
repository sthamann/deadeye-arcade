using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
    private PhysicalGun[] guns = [];
    private readonly Dictionary<string, string> gunIssues = [];
    private readonly Dictionary<string, DateTimeOffset> gunSignals = [];
    private bool scanningGuns;
    private readonly HashSet<string> preparedVendors = [];
    private int? learningPlayer;
    private string? learningAction;
    private readonly DispatcherTimer gunTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly GameSession session = new();
    private readonly Dictionary<int, TriggerHold> triggerHolds = [];
    private InGameOverlay? overlay;
    private GameEntry? activeGame;
    private int overlayOpener;
    private bool overlayAction;
    private EmergencyExit? emergencyExit;
    private DependencyReport? dependencies;
    private readonly LibraryStore store;
    private LibraryState state;
    private readonly Dictionary<string, ExitGesture> gestures = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private int? bindPlayer;
    private string? bindMouse;
    private bool ready, busy, closing, closeRequested, launching;
    private readonly Button exitButton = new()
    {
        Content = "App schließen · Windows", MinWidth = 260, Height = 64,
        FontSize = 17, HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20),
        Background = new SolidColorBrush(Color.FromRgb(38, 45, 58)), Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(104, 115, 137)), Cursor = System.Windows.Input.Cursors.Hand
    };
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
        Title = "Reaper Arcade"; Width = 1280; Height = 800; MinWidth = 900; MinHeight = 620; Background = new SolidColorBrush(Color.FromRgb(12, 15, 21));
        // A native control remains usable independently of browser dialogs and loading states.
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Keep the control outside WebView2's HWND to avoid WPF airspace covering it.
        Grid.SetRow(exitButton, 1); layout.Children.Add(web); layout.Children.Add(exitButton); Content = layout;
        exitButton.Click += (_, _) => Close();
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade");
        Directory.CreateDirectory(data); store = new(data); state = store.Load(); logPath = Path.Combine(data, "activity.log");
        SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source.AddHook(Hook); raw.Register(new WindowInteropHelper(this).Handle); };
        Loaded += async (_, _) => await Initialize();
        raw.Packet += Input; raw.DevicesChanged += () => { foreach (var hold in triggerHolds.Values) hold.Reset(); if (overlay is not null) overlay.Arm(); if (ready && !closing) { gunTimer.Stop(); gunTimer.Start(); } };
        gunTimer.Tick += async (_, _) => { gunTimer.Stop(); if (!busy && !session.Active) await DiscoverGuns(); else { gunTimer.Start(); } };
        session.Changed += status => Dispatcher.Invoke(() =>
        {
            emergencyExit?.Dispose(); emergencyExit = null;
            if (status == "running")
            {
                try { emergencyExit = new EmergencyExit(() => Dispatcher.BeginInvoke(new Action(() => { Log("exit: F12"); _ = session.End(); })), () => Dispatcher.BeginInvoke(new Action(() => { OpenOverlay(0); overlay?.Arm(); }))); }
                catch (System.ComponentModel.Win32Exception error) { Log("F12 fallback unavailable: " + error.Message); }
                WindowState = WindowState.Minimized;
            }
            else { CloseOverlay(false); foreach (var hold in triggerHolds.Values) hold.Reset(); WindowState = WindowState.Normal; SetFullscreen(state.Settings.Fullscreen); Activate(); }
            Send("session", new { status });
        });
        timer.Tick += async (_, _) =>
        {
            if (session.Active && overlay is null && !overlayAction && activeGame is not null)
            {
                var held = triggerHolds.FirstOrDefault(p => p.Value.Ready(Environment.TickCount64));
                if (held.Value is not null) { held.Value.Consume(); OpenOverlay(held.Key); }
            }
            if (!gestures.Values.Any(g => g.Ready(DateTimeOffset.UtcNow))) return;
            foreach (var g in gestures.Values) g.Consume();
            if (session.Active) { Log("exit: gun to menu"); await session.End(); }
            else { Log("exit: gun to desktop"); Close(); }
        };
        timer.Start();
        Closing += async (_, e) =>
        {
            if (session.Active)
            {
                e.Cancel = true;
                if (closeRequested) return;
                closeRequested = true; exitButton.Content = "Spiel wird beendet …";
                await session.End();
                while (session.Active) await Task.Delay(100);
                if (!launching && !closing) Close();
                return;
            }
            closing = true; ready = false; Log("exit: desktop");
        };
        Closed += (_, _) => { CloseOverlay(false); timer.Stop(); gunTimer.Stop(); emergencyExit?.Dispose(); raw.Dispose(); source?.RemoveHook(Hook); http.Dispose(); web.Dispose(); };
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled) { raw.Message(message, lParam); if (message == 0x219 && ready && !closing) { raw.Refresh(); gunTimer.Stop(); gunTimer.Start(); } return 0; }
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
        foreach (var binding in state.Bindings.Where(b => !raw.Devices.Any(d => string.Equals(d.Id, b.KeyboardId, StringComparison.OrdinalIgnoreCase))))
            if (binding.KeyboardId is not null && gestures.TryGetValue(binding.KeyboardId, out var gesture)) gesture.Reset();
        Send("state", new
        {
            games = state.Games.Select(g => g with { Cover = MediaUrl(g.Cover, true), PreviewVideo = MediaUrl(g.PreviewVideo), Screenshot = MediaUrl(g.Screenshot), Logo = MediaUrl(g.Logo) }),
            bindings = state.Bindings,
            devices = raw.Devices,
            gunSystems = GunSystems.Catalog,
            gunSoftware = GunSoftware.Status(store.DirectoryPath),
            guns,
            gunIssues,
            gunSignals,
            learning = learningPlayer is null ? null : new { player = learningPlayer, action = learningAction },
            ports = SerialPort.GetPortNames().OrderBy(x => x),
            settings = new { state.Settings.StartWithWindows, state.Settings.Fullscreen, hasCoverKey = state.Settings.CoverKey is not null },
            bindingStage = bindPlayer is null ? null : new { player = bindPlayer, stage = bindMouse is null ? "trigger" : "start" },
            version = "0.3.1",
            remoteSession,
            installations,
            dependencies = DependencyView(),
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
        // The physical trigger can always reach the native exit, even while learning a button.
        if (!session.Active && packet.Kind == "mouse" && (packet.Buttons & 1) != 0)
        {
            var exitPoint = exitButton.PointFromScreen(PacketPoint(packet));
            if (exitPoint.X >= 0 && exitPoint.Y >= 0 && exitPoint.X < exitButton.ActualWidth && exitPoint.Y < exitButton.ActualHeight)
            { Close(); return; }
        }
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
        if (session.Active && emergencyExit is null && packet.Kind == "keyboard" && packet.Key == 0x7B && packet.Down) { _ = session.End(); return; }
        var binding = state.Bindings.FirstOrDefault(b => string.Equals(b.MouseId, packet.DeviceId, StringComparison.OrdinalIgnoreCase) || string.Equals(b.KeyboardId, packet.DeviceId, StringComparison.OrdinalIgnoreCase));
        if (packet.Kind == "keyboard" && binding is not null)
        {
            if (!gestures.TryGetValue(packet.DeviceId, out var gesture)) gestures[packet.DeviceId] = gesture = new();
            gesture.Key(packet.Key, packet.Down, DateTimeOffset.UtcNow);
        }
        if (binding is not null)
        {
            foreach (var signal in Signals(packet))
            {
                var map = binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player);
                string action = map.GetValueOrDefault(signal.Token, "none");
                if (session.Active && action == "shoot")
                {
                    if (!triggerHolds.TryGetValue(binding.Player, out var hold)) triggerHolds[binding.Player] = hold = new();
                    hold.Button(packet.DeviceId + "|" + signal.Token, signal.Down, Environment.TickCount64);
                    if (!signal.Down && !hold.Pressed && overlay is not null && binding.Player == overlayOpener) overlay.Arm();
                }
                if (overlay is not null && signal.Down)
                {
                    if (action == "shoot") overlay.Shoot(PacketPoint(packet)); else overlay.Navigate(action);
                }
                if (binding.PhysicalId is not null)
                {
                    bool firstSignal = !gunSignals.ContainsKey(binding.PhysicalId); gunSignals[binding.PhysicalId] = DateTimeOffset.UtcNow;
                    if (firstSignal) SendState();
                }
                Send("gun-input", new { player = binding.Player, token = signal.Token, down = signal.Down, action });
                if (learningPlayer == binding.Player && signal.Down && !session.Active)
                {
                    var updated = new Dictionary<string, string>(map);
                    foreach (var old in updated.Where(kv => kv.Value == learningAction).Select(kv => kv.Key).ToArray()) updated.Remove(old);
                    updated[signal.Token] = learningAction!;
                    int index = state.Bindings.IndexOf(binding);
                    state.Bindings[index] = binding with { ButtonMap = GunSystems.ValidateMap(updated) };
                    learningPlayer = null; learningAction = null; Persist(); return;
                }
            }
        }
        if (session.Active || remoteSession || learningPlayer is not null) return;
        long now = Environment.TickCount64;
        if (packet.Kind == "mouse" && packet.Buttons == 0 && now - lastMove < 30) return;
        if (packet.Kind == "mouse") lastMove = now;
        if (binding is not null && packet.Kind == "mouse")
            foreach (var signal in Signals(packet).Where(s => s.Down))
            {
                string action = (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player)).GetValueOrDefault(signal.Token, "none");
                if (action is not ("shoot" or "reload" or "none")) Send("menu-action", new { action });
            }
        // No global shared cursor is used for absolute lightgun packets.
        if (packet.Kind == "mouse")
        {
            var point = PacketPoint(packet);
            int menuButtons = binding is null ? packet.Buttons : 0;
            if (binding is not null)
                foreach (var signal in Signals(packet).Where(s => s.Down))
                {
                    string action = (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player)).GetValueOrDefault(signal.Token, "none");
                    if (action == "shoot") menuButtons |= 1; if (action == "reload") menuButtons |= 4;
                }
            var client = web.PointFromScreen(point);
            Send("input", new { packet.DeviceId, kind = packet.Kind, player = binding?.Player ?? 0, x = client.X / Math.Max(1, web.ActualWidth), y = client.Y / Math.Max(1, web.ActualHeight), buttons = menuButtons });
        }
        else Send("input", new { packet.DeviceId, kind = packet.Kind, player = binding?.Player ?? 0, packet.Key, packet.Down, action = binding is null ? null : (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player)).GetValueOrDefault("key:" + packet.Key, "none") });
    }
    private void OpenOverlay(int player)
    {
        if (!session.Active || activeGame is null || overlay is not null) return;
        overlayOpener = player;
        foreach (var hold in triggerHolds.Values.Where(h => h.Pressed)) hold.Consume();
        var controls = OverlayControls.Read(activeGame, store.DirectoryPath, state.Bindings);
        session.HideForOverlay();
        overlay = new InGameOverlay(activeGame, controls, state.Bindings, action => _ = OverlayCommand(action));
        overlay.Closed += (_, _) => { if (overlay is not null) { overlay = null; session.RestoreFromOverlay(); } };
        overlay.Show(); Log("overlay: opened P" + player);
    }
    private void CloseOverlay(bool resume)
    {
        var window = overlay; overlay = null; window?.Close();
        if (resume) session.RestoreFromOverlay();
    }
    private async Task OverlayCommand(string action)
    {
        if (overlayAction || !session.Active) return;
        if (action == "resume") { CloseOverlay(true); Log("overlay: resume"); return; }
        if (action is not ("end" or "restart")) return;
        overlayAction = true; var game = activeGame;
        try
        {
            Log("overlay: " + action); await session.End();
            while (session.Active || launching) await Task.Delay(50);
            if (action == "restart" && game is not null && !closing && !closeRequested)
            {
                overlayAction = false;
                await Launch(state.Games.FirstOrDefault(g => g.Id == game.Id) ?? game);
            }
        }
        catch (Exception error) { Log("overlay error: " + error.Message); Send("error", new { message = error.Message }); }
        finally { overlayAction = false; }
    }
    private static Point PacketPoint(RawPacket packet)
    {
        if (!packet.Absolute) { GetCursorPos(out var point); return new(point.X, point.Y); }
        int left = packet.VirtualDesktop ? GetSystemMetrics(76) : 0, top = packet.VirtualDesktop ? GetSystemMetrics(77) : 0;
        int width = GetSystemMetrics(packet.VirtualDesktop ? 78 : 0), height = GetSystemMetrics(packet.VirtualDesktop ? 79 : 1);
        return new(left + packet.X / 65535.0 * (width - 1), top + packet.Y / 65535.0 * (height - 1));
    }
    private static IEnumerable<(string Token, bool Down)> Signals(RawPacket packet)
    {
        if (packet.Kind == "keyboard") { yield return ("key:" + packet.Key, packet.Down); yield break; }
        if (packet.Kind != "mouse") yield break;
        for (int i = 0; i < 5; i++)
        {
            if ((packet.Buttons & (1 << (i * 2))) != 0) yield return ("mouse:" + (i + 1), true);
            if ((packet.Buttons & (2 << (i * 2))) != 0) yield return ("mouse:" + (i + 1), false);
        }
    }
    private async Task DiscoverGuns()
    {
        if (scanningGuns || closing) return;
        scanningGuns = true; Send("busy", new { message = "Lightguns werden erkannt und zugeordnet …" });
        try
        {
            var devices = raw.Devices.ToArray(); guns = await Task.Run(() => GunDiscovery.Scan(devices)); gunIssues.Clear();
            foreach (var gun in guns.Where(g => g.SystemId == "rs3"))
            {
                try
                {
                    if (!gun.DriverHealthy) throw new IOException(string.Join("; ", gun.Issues));
                    if (gun.Port is null || gun.MouseId is null || gun.KeyboardId is null) throw new IOException("USB-Maus, Tastatur oder COM-Port fehlen. Mausmodus prüfen.");
                    int player = await serial.Probe(gun.Port);
                    if (player > 2) throw new IOException($"Gun meldet P{player}. Die Oberfläche unterstützt aktuell P1/P2.");
                    var existing = state.Bindings.FirstOrDefault(b => b.Player == player);
                    if (existing is not null && !string.Equals(existing.MouseId, gun.MouseId, StringComparison.OrdinalIgnoreCase) && existing.PhysicalId != gun.Id && guns.Any(g => g.Id == existing.PhysicalId || string.Equals(g.MouseId, existing.MouseId, StringComparison.OrdinalIgnoreCase)))
                        throw new IOException($"P{player} ist schon durch eine andere angeschlossene Gun belegt. DIP-Spielerzuordnung prüfen.");
                    if (state.Bindings.Any(b => b.Player != player && (b.PhysicalId == gun.Id || b.MouseId == gun.MouseId)))
                        throw new IOException("Dieselbe Gun ist bereits einem anderen Spieler zugeordnet.");
                    var feedback = existing?.Feedback ?? new();
                    await serial.Command(gun.Port, player, GunSystems.ReaperConfiguration(feedback));
                    state.Bindings.RemoveAll(b => b.Player == player);
                    state.Bindings.Add(new(player, gun.MouseId, gun.KeyboardId, gun.Port, "rs3", gun.Id, existing?.ButtonMap ?? GunSystems.DefaultMap(player), feedback, true));
                }
                catch (Exception error) { gunIssues[gun.Id] = error.Message; Log("gun setup: " + error.Message); }
            }
            foreach (var gun in guns.Where(g => g.SystemId != "rs3" && g.DriverHealthy))
                if (preparedVendors.Add(gun.Id))
                    try { string result = await GunSoftware.Prepare(gun.SystemId, store.DirectoryPath, text => Send("busy", new { message = text })); Send("notice", new { message = result }); }
                    catch (Exception error) { gunIssues[gun.Id] = error.Message; }
            foreach (var gun in guns.Where(g => g.SystemId != "rs3" && g.DriverHealthy && g.MouseId is not null))
            {
                if (state.Bindings.Any(b => b.PhysicalId == gun.Id)) continue;
                int player = Enumerable.Range(1, 2).FirstOrDefault(p => !state.Bindings.Any(b => b.Player == p));
                if (player != 0) state.Bindings.Add(new(player, gun.MouseId!, gun.KeyboardId, gun.Port, gun.SystemId, gun.Id, GunSystems.DefaultMap(player)));
            }
            if (!closing) { store.Save(state); SendState(); }
        }
        catch (Exception error) { Log("gun discovery: " + error.Message); Send("error", new { message = error.Message }); }
        finally { scanningGuns = false; Send("busy", new { message = "" }); }
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
        if (type == "close") { Close(); return; }
        if (type == "browse-path") { picker.Browse(Str("path")); return; }
        if (type == "choose-path") { picker.Choose(Str("path")); return; }
        if (type == "cancel-picker") { picker.Cancel(); return; }
        if (picker.Active) throw new InvalidOperationException("Bitte zuerst die Dateiauswahl schließen.");
        if (type == "ready") { ready = true; SendState(); await DiscoverGuns(); await CheckDependencies(); return; }
        if (type == "end-game") { await session.End(); return; }
        if (type == "show-overlay") { OpenOverlay(0); overlay?.Arm(); return; }
        if (type == "cancel-bind") { bindPlayer = null; bindMouse = null; SendState(); return; }
        if (session.Active && type != "fullscreen") throw new InvalidOperationException("Bitte zuerst das laufende Spiel beenden.");
        if (type == "cancel-learn") { learningPlayer = null; learningAction = null; SendState(); return; }
        if (busy || scanningGuns) throw new InvalidOperationException("Die laufende Aktion wird noch abgeschlossen.");
        switch (type)
        {
            case "check-dependencies": await CheckDependencies(); break;
            case "install-dependencies":
                {
                    busy = true;
                    try
                    {
                        dependencies = await Task.Run(() => RuntimeInstaller.Scan(state.Games.ToArray()));
                        var packages = dependencies.MissingPackages.Select(RuntimeCatalog.Get).OfType<RuntimePackage>().ToArray();
                        foreach (var package in packages)
                        {
                            int code;
                            try
                            {
                                code = await RuntimeInstaller.Install(package, store.DirectoryPath,
                                    text => Send("busy", new { message = text }),
                                    () => { if (closing) throw new OperationCanceledException(); WindowState = WindowState.Minimized; });
                            }
                            finally { SetFullscreen(state.Settings.Fullscreen); Activate(); }
                            Log($"runtime: {package.Id} installer exit {code}");
                            if (code == 3010) Send("notice", new { message = "Die Laufzeit meldet einen nötigen Neustart. Bitte später selbst neu starten." });
                            else if (code != 0) { Send("notice", new { message = $"{package.Name}: Installation abgebrochen oder fehlgeschlagen (Code {code})." }); break; }
                        }
                    }
                    finally { busy = false; Send("busy", new { message = "" }); await CheckDependencies(); }
                    break;
                }
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
                    if (path is not null) { await Import(() => CollectionImporter.Read(path)); await CheckDependencies(); }
                    break;
                }
            case "test-feedback":
                {
                    if (remoteSession) throw new InvalidOperationException("Feedback am lokalen Bildschirm testen und die Gun dabei in der Hand halten.");
                    var b = state.Bindings.First(x => x.Player == Player());
                    if (b.SystemId != "rs3" || b.SerialPort is null) throw new InvalidOperationException("Bitte zuerst den Gun-COM-Port zuordnen.");
                    string effect = Str("effect");
                    var feedback = b.Feedback ?? new();
                    if (effect is "recoil" or "combined" && !feedback.Recoil || effect is "rumble" or "combined" && !feedback.Rumble) throw new InvalidOperationException("Diesen Testeffekt zuerst in der Feedback-Auswahl aktivieren.");
                    string[] commands = effect switch { "recoil" => ["ZS", "Z5", "ZX"], "rumble" => ["ZS", "ZZ", "ZX"], "combined" => ["ZS", "Z5", "ZZ", "ZX"], _ => throw new ArgumentException("Unbekannter Feedbacktest.") };
                    busy = true;
                    try { await serial.Command(b.SerialPort, b.Player, commands); Send("notice", new { message = "Einzelimpuls gesendet. Stärke und Gefühl beurteilst du an der Gun; dies bestätigt noch kein Spielefeedback." }); }
                    finally { busy = false; }
                    break;
                }
            case "refresh": raw.Refresh(); await DiscoverGuns(); break;
            case "learn-button":
                {
                    int player = Player(); string action = Str("action");
                    if (!GunSystems.Actions.Contains(action) || action == "none") throw new ArgumentException("Unbekannte Aktion.");
                    if (!state.Bindings.Any(b => b.Player == player)) throw new InvalidOperationException("Bitte zuerst die Gun einrichten.");
                    learningPlayer = player; learningAction = action; SendState(); break;
                }
            case "reset-button-map":
                {
                    int i = state.Bindings.FindIndex(b => b.Player == Player()); if (i < 0) throw new InvalidOperationException("Keine Gun zugeordnet.");
                    state.Bindings[i] = state.Bindings[i] with { ButtonMap = GunSystems.DefaultMap(Player()) }; Persist(); break;
                }
            case "set-gun-feedback":
                {
                    int i = state.Bindings.FindIndex(b => b.Player == Player()); if (i < 0) throw new InvalidOperationException("Keine Gun zugeordnet.");
                    var feedback = JsonSerializer.Deserialize<GunFeedback>(payload.GetProperty("feedback"), JsonDefaults.Options) ?? throw new ArgumentException("Einstellungen fehlen.");
                    _ = GunSystems.ReaperConfiguration(feedback);
                    var b = state.Bindings[i];
                    if (b.SystemId != "rs3" || b.SerialPort is null) throw new InvalidOperationException("Diese Konfiguration benötigt eine erkannte RS3 mit COM-Port.");
                    await serial.Command(b.SerialPort, b.Player, GunSystems.ReaperConfiguration(feedback));
                    state.Bindings[i] = b with { Feedback = feedback, SoftwareConfigured = true }; Persist();
                    Send("notice", new { message = "Mausmodus, Bildformat und Offscreen-Reload angefordert. Feedback-Auswahl für die Testimpulse gespeichert; die DIP-Schalter an der Gun bleiben maßgeblich." }); break;
                }
            case "setup-gun":
                {
                    string system = Str("system"); if (!GunSystems.Catalog.Any(g => g.Id == system)) throw new ArgumentException("Unbekanntes Lightgun-System.");
                    if (system == "rs3") { await DiscoverGuns(); break; }
                    busy = true;
                    try { var path = await GunSoftware.Prepare(system, store.DirectoryPath, text => Send("busy", new { message = text })); Send("notice", new { message = path }); }
                    finally { busy = false; Send("busy", new { message = "" }); SendState(); }
                    break;
                }
            case "assign-gun":
                {
                    int player = Player(); var gun = guns.First(g => g.Id == Str("id"));
                    if (gun.SystemId == "rs3") throw new InvalidOperationException("RS3-Spieler werden über die Hardware-ID zugeordnet. DIP-Spieler prüfen und neu erkennen.");
                    if (gun.MouseId is null) throw new InvalidOperationException("Noch kein Maus-Eingang der Gun vorhanden. Zuerst Hersteller-Software einrichten.");
                    if (state.Bindings.Any(b => b.Player != player && b.PhysicalId == gun.Id)) throw new InvalidOperationException("Die Gun gehört schon zu einem anderen Spieler.");
                    state.Bindings.RemoveAll(b => b.Player == player); state.Bindings.Add(new(player, gun.MouseId, gun.KeyboardId, gun.Port, gun.SystemId, gun.Id, GunSystems.DefaultMap(player))); Persist(); break;
                }
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
                    var binding = state.Bindings.First(b => b.Player == Player()); if (binding.SystemId != "rs3" || binding.SerialPort is null) throw new InvalidOperationException("Bitte den RS3-COM-Port auswählen.");
                    busy = true; try { int number = await serial.Command(binding.SerialPort, binding.Player); Send("notice", new { message = $"RS3 antwortet: Spieler {number}. Zielgenauigkeit und Recoil sind damit noch nicht geprüft." }); } finally { busy = false; }
                    break;
                }
            case "mouse-mode":
                {
                    var b = state.Bindings.First(b => b.Player == Player()); if (b.SystemId != "rs3" || b.SerialPort is null) throw new InvalidOperationException("Bitte den RS3-COM-Port auswählen.");
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
        if (type is "import-tekno" or "import-mame" or "add-pc" or "validate-library") await CheckDependencies();
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
    private object? DependencyView() => dependencies is null ? null : new
    {
        dependencies.Time, dependencies.Games, dependencies.CheckedBinaries,
        packages = dependencies.Findings.Where(f => f.PackageId is not null).GroupBy(f => f.PackageId!).Select(group => new
        {
            id = group.Key, name = RuntimeCatalog.Get(group.Key)?.Name ?? group.Key,
            missing = group.Any(f => f.Missing), games = group.Select(f => f.Game).Distinct(),
            dlls = group.Where(f => f.Missing).Select(f => f.Dll).Distinct()
        }),
        uncheckedCount = dependencies.Unchecked.Length,
        uncheckedFiles = dependencies.Unchecked.Take(50)
    };
    private async Task CheckDependencies()
    {
        busy = true; Send("busy", new { message = "Laufzeiten für Spiele und Emulatoren werden geprüft …" });
        try
        {
            var games = state.Games.ToArray();
            dependencies = await Task.Run(() => RuntimeInstaller.Scan(games));
            System.IO.File.WriteAllText(Path.Combine(store.DirectoryPath, "dependencies.json"), JsonSerializer.Serialize(dependencies, JsonDefaults.Options));
            SendState();
        }
        finally { busy = false; Send("busy", new { message = "" }); }
    }
    private async Task Launch(GameEntry game)
    {
        _ = LaunchRules.Prepare(game);
        busy = true;
        DependencyReport launchDependencies;
        try { launchDependencies = await Task.Run(() => RuntimeInstaller.Scan([game])); }
        finally { busy = false; }
        if (launchDependencies.Findings.Any(f => f.Missing && f.Required && f.PackageId is not null))
        {
            await CheckDependencies();
            Send("dependency-blocked", new { message = "Für dieses Spiel fehlen Laufzeiten. Du kannst sie hier installieren und danach erneut starten." });
            return;
        }
        busy = true; launching = true; var changed = new List<GunBinding>();
        try
        {
            foreach (var binding in state.Bindings.Where(b => b.SystemId == "rs3" && b.SerialPort is not null && guns.Any(g => g.Id == b.PhysicalId || g.MouseId == b.MouseId)))
            { await serial.Command(binding.SerialPort!, binding.Player, GunSystems.ReaperConfiguration((binding.Feedback ?? new()) with { Aspect = game.Aspect })); changed.Add(binding); }
            foreach (var gesture in gestures.Values) gesture.Reset();
            foreach (var hold in triggerHolds.Values) hold.Reset(); activeGame = game;
            state.Games[state.Games.IndexOf(game)] = game with { LastPlayed = DateTimeOffset.UtcNow }; store.Save(state); Log("launch: " + game.Title);
            await session.Run(game, store.DirectoryPath, state.Bindings.Where(b => guns.Any(g => g.MouseId is not null && string.Equals(g.MouseId, b.MouseId, StringComparison.OrdinalIgnoreCase))));
        }
        finally
        {
            foreach (var binding in changed)
            { try { await serial.Command(binding.SerialPort!, binding.Player, GunSystems.ReaperConfiguration(binding.Feedback ?? new())); } catch (Exception e) { Log("restore: " + e.Message); Send("error", new { message = "Menümodus konnte nicht wiederhergestellt werden: " + e.Message }); } }
            busy = false; launching = false; activeGame = null;
            if (closeRequested) Close(); else SendState();
        }
    }
}
