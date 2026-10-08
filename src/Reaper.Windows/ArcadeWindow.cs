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
    private readonly AppUpdater updater = new();
    private AppRelease? availableUpdate;
    private string updateStatus = "idle", updateError = "";
    private int updateProgress;
    private bool checkingUpdate;
    private DateTimeOffset? updateChecked;
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromHours(6) };
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
    private string? learningControl;
    private readonly DispatcherTimer gunTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly GameSession session = new();
    private readonly Dictionary<int, TriggerHold> triggerHolds = [];
    private readonly TriggerHold desktopTrigger = new();
    private readonly TriggerHold buttonTestHold = new();
    private int? buttonTestPlayer;
    private bool buttonTestRelease;
    private InGameOverlay? overlay;
    private StartupOverlay? startupOverlay;
    private GameEntry? activeGame;
    private ReaperCalibrationWindow? calibration;
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
        Content = I18n.T("App schließen · Windows"), MinWidth = 260, Height = 64,
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
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    public ArcadeWindow()
    {
        picker = new ArcadePicker(Send);
        Title = "Deadeye Arcade"; Width = 1280; Height = 800; MinWidth = 900; MinHeight = 620; Background = new SolidColorBrush(Color.FromRgb(12, 15, 21));
        // A native control remains usable independently of browser dialogs and loading states.
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Keep the control outside WebView2's HWND to avoid WPF airspace covering it.
        Grid.SetRow(exitButton, 1); layout.Children.Add(web); layout.Children.Add(exitButton); Content = layout;
        exitButton.Click += (_, _) => { if (buttonTestPlayer is null && !buttonTestRelease) Close(); };
        string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade");
        Directory.CreateDirectory(data); store = new(data); state = store.Load(); I18n.Language = state.Settings.Language; exitButton.Content = I18n.T("App schließen · Windows"); logPath = Path.Combine(data, "activity.log");
        SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source.AddHook(Hook); raw.Register(new WindowInteropHelper(this).Handle); };
        Loaded += async (_, _) => await Initialize();
        raw.Packet += Input; raw.DevicesChanged += () =>
        {
            var connected = raw.Devices.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var hold in triggerHolds.Values) hold.RetainDevices(connected);
            buttonTestHold.RetainDevices(connected);
            if (buttonTestRelease && !buttonTestHold.Pressed) buttonTestRelease = false;
            if (overlay is not null && overlayOpener != 0 && triggerHolds.GetValueOrDefault(overlayOpener)?.Pressed != true) overlay.Arm();
            if (session.Active) Log("input: device list changed; preserving connected trigger holds");
            if (ready && !closing) { gunTimer.Stop(); gunTimer.Start(); }
        };
        gunTimer.Tick += async (_, _) => { gunTimer.Stop(); if (!busy && !session.Active) await DiscoverGuns(); else { gunTimer.Start(); } };
        session.GameWindowReady += handle => Dispatcher.Invoke(() => ShowStartupControls(handle));
        session.Changed += status => Dispatcher.Invoke(() =>
        {
            emergencyExit?.Dispose(); emergencyExit = null;
            if (status == "running")
            {
                buttonTestPlayer = null; buttonTestRelease = false; buttonTestHold.Reset();
                Send("button-test-ended", new {});
                desktopTrigger.Reset();
                try { emergencyExit = new EmergencyExit(() => Dispatcher.BeginInvoke(new Action(() => { Log("exit: F12"); _ = session.End(); })), () => Dispatcher.BeginInvoke(new Action(() => { OpenOverlay(0); overlay?.Arm(); }))); }
                catch (System.ComponentModel.Win32Exception error) { Log("F12 fallback unavailable: " + error.Message); }
                WindowState = WindowState.Minimized;
            }
            else { CloseStartupControls(); CloseOverlay(false); desktopTrigger.Reset(); foreach (var hold in triggerHolds.Values) hold.Reset(); WindowState = WindowState.Normal; SetFullscreen(state.Settings.Fullscreen); Activate(); }
            Send("session", new { status });
        });
        timer.Tick += async (_, _) =>
        {
            if (buttonTestPlayer is not null && buttonTestHold.Ready(Environment.TickCount64))
            {
                buttonTestHold.Consume(); buttonTestPlayer = null; buttonTestRelease = true;
                gestures.Clear(); Send("button-test-ended", new {});
                Send("notice", new { message = I18n.T("Tastentest beendet. Abzug loslassen; die Gun steuert wieder das Menü.") });
            }
            if (buttonTestPlayer is not null || buttonTestRelease) return;
            if (session.Active)
            {
                // Independent fallback for games that capture mouse input or change Raw Input registration.
                desktopTrigger.Button("desktop", (GetAsyncKeyState(1) & 0x8000) != 0, Environment.TickCount64);
                if (overlay is not null && overlayOpener == 0 && !desktopTrigger.Pressed) overlay.Arm();
            }
            if (session.Active && overlay is null && !overlayAction && activeGame is not null)
            {
                var held = triggerHolds.FirstOrDefault(p => p.Value.Ready(Environment.TickCount64));
                if (held.Value is not null) { held.Value.Consume(); OpenOverlay(held.Key); }
                else if (desktopTrigger.Ready(Environment.TickCount64)) { desktopTrigger.Consume(); Log("overlay: desktop trigger fallback"); OpenOverlay(0); }
            }
            if (!gestures.Values.Any(g => g.Ready(DateTimeOffset.UtcNow))) return;
            foreach (var g in gestures.Values) g.Consume();
            if (session.Active) { Log("exit: gun to menu"); await session.End(); }
            else { Log("exit: gun to desktop"); Close(); }
        };
        timer.Start();
        updateTimer.Tick += async (_, _) => { if (state.Settings.CheckForUpdates && !session.Active && !busy) await CheckAppUpdate(); };
        updateTimer.Start();
        Closing += async (_, e) =>
        {
            if (session.Active)
            {
                e.Cancel = true;
                if (closeRequested) return;
                closeRequested = true; exitButton.Content = I18n.T("Spiel wird beendet …");
                await session.End();
                while (session.Active) await Task.Delay(100);
                if (!launching && !closing) Close();
                return;
            }
            calibration?.Close(); closing = true; ready = false; Log("exit: desktop");
        };
        Closed += (_, _) => { CloseStartupControls(); CloseOverlay(false); timer.Stop(); gunTimer.Stop(); updateTimer.Stop(); updater.Dispose(); emergencyExit?.Dispose(); raw.Dispose(); source?.RemoveHook(Hook); http.Dispose(); web.Dispose(); };
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled) { raw.Message(message, lParam); if (message == 0x219 && ready && !closing) { raw.Refresh(); gunTimer.Stop(); gunTimer.Start(); } return 0; }
    private async Task Initialize()
    {
        try
        {
            string webDirectory = Path.Combine(AppContext.BaseDirectory, "web");
            if (!System.IO.File.Exists(Path.Combine(webDirectory, "index.html"))) throw new IOException(I18n.T("Die Oberfläche fehlt. Bitte das vollständige Windows-Paket entpacken."));
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
        catch (WebView2RuntimeNotFoundException) { MessageBox.Show(I18n.T("Für die Oberfläche wird Microsoft Edge WebView2 Runtime benötigt.\nDownload: https://developer.microsoft.com/microsoft-edge/webview2/"), Title); Close(); }
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
            gameCompatibility = state.Games.ToDictionary(g=>g.Id,g=>GameCompatibility.Read(g,state.Bindings.Count(b=>guns.Any(gun=>gun.MouseId==b.MouseId)))),
            bindings = state.Bindings,
            devices = raw.Devices,
            gunSystems = GunSystems.Catalog,
            gunSoftware = GunSoftware.Status(store.DirectoryPath),
            guns,
            gunIssues,
            gunSignals,
            learning = learningPlayer is null ? null : new { player = learningPlayer, action = learningAction, control = learningControl },
            ports = SerialPort.GetPortNames().OrderBy(x => x),
            settings = new { state.Settings.StartWithWindows, state.Settings.Fullscreen, language = I18n.Normalize(state.Settings.Language), state.Settings.CheckForUpdates, hasCoverKey = state.Settings.CoverKey is not null },
            bindingStage = bindPlayer is null ? null : new { player = bindPlayer, stage = bindMouse is null ? "trigger" : "start" },
            version = AppUpdater.Current,
            update = new { status = updateStatus, release = availableUpdate, progress = updateProgress, error = updateError, checkedAt = updateChecked },
            remoteSession,
            calibrationPrepared = ReaperCalibrationPackage.Prepared(store.DirectoryPath),
            installations,
            dependencies = DependencyView(),
            calibrationTool = state.Settings.CalibrationTool is null ? null : Path.GetFileName(state.Settings.CalibrationTool),
            native = true
        });
    }
    private async Task CheckAppUpdate()
    {
        if (checkingUpdate || updateStatus is "downloading" or "installing" || closing) return;
        checkingUpdate = true; updateStatus = "checking"; updateError = ""; SendState();
        try { availableUpdate = await updater.Check(); updateChecked = DateTimeOffset.UtcNow; updateStatus = availableUpdate is null ? "current" : "available"; }
        catch (OperationCanceledException) when (closing) { }
        catch (Exception error) { updateStatus = "error"; updateError = error.Message; }
        finally { checkingUpdate = false; if (!closing) SendState(); }
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
        if(calibration is not null)
        {
            try { calibration.Input(packet); }
            catch(Exception e) { Send("error",new {message=e.Message}); }
            return;
        }
        // The physical trigger can always reach the native exit, even while learning a button.
        if (!session.Active && buttonTestPlayer is null && !buttonTestRelease && packet.Kind == "mouse" && (packet.Buttons & 1) != 0)
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
                { Send("error", new { message = I18n.T("Dieser Eingang ist bereits einem anderen Spieler zugeordnet. Bitte dessen Gun verwenden oder Zuordnung entfernen.") }); return; }
                state.Bindings.RemoveAll(b => b.Player == player);
                state.Bindings.Add(new(player, bindMouse, packet.DeviceId)); bindMouse = null; bindPlayer = null; Persist(); return;
            }
        }
        if (session.Active && emergencyExit is null && packet.Kind == "keyboard" && packet.Key == 0x7B && packet.Down) { _ = session.End(); return; }
        var binding = state.Bindings.FirstOrDefault(b => string.Equals(b.MouseId, packet.DeviceId, StringComparison.OrdinalIgnoreCase) || string.Equals(b.KeyboardId, packet.DeviceId, StringComparison.OrdinalIgnoreCase)
            || packet.Kind == "hid" && guns.Any(g => g.Id == b.PhysicalId && g.InputIds.Contains(packet.DeviceId, StringComparer.OrdinalIgnoreCase)));
        if (packet.Kind == "keyboard" && binding is not null && buttonTestPlayer is null && !buttonTestRelease)
        {
            if (!gestures.TryGetValue(packet.DeviceId, out var gesture)) gestures[packet.DeviceId] = gesture = new();
            gesture.Key(packet.Key, packet.Down, DateTimeOffset.UtcNow);
        }
        if (binding is not null)
        {
            foreach (var signal in Signals(packet))
            {
                var map = binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player,binding.SystemId);
                string action = map.GetValueOrDefault(signal.Token, "none");
                // RS3 joystick mode exposes its primary trigger on HID button usage 1.
                if (packet.Kind == "hid" && binding.SystemId == "rs3" && packet.Key == 1) action = "shoot";
                bool physicalTrigger = signal.Token == (binding.ControlMap?.GetValueOrDefault("trigger") ?? "mouse:1")
                    || binding.SystemId == "rs3" && packet.Kind == "hid" && packet.Key == 1;
                if ((buttonTestPlayer == binding.Player || buttonTestRelease) && physicalTrigger)
                {
                    buttonTestHold.Button(packet.DeviceId + "|" + signal.Token, signal.Down, Environment.TickCount64);
                    if (buttonTestRelease && !buttonTestHold.Pressed) { buttonTestRelease = false; buttonTestHold.Reset(); }
                }
                if (session.Active && action == "shoot")
                {
                    Log($"input: P{binding.Player} {signal.Token} {(signal.Down ? "down" : "up")}");
                    if (!triggerHolds.TryGetValue(binding.Player, out var hold)) triggerHolds[binding.Player] = hold = new();
                    hold.Button(packet.DeviceId + "|" + signal.Token, signal.Down, Environment.TickCount64);
                    if (!signal.Down && !hold.Pressed && overlay is not null && binding.Player == overlayOpener) overlay.Arm();
                }
                if (overlay is not null && signal.Down)
                {
                    if (action == "shoot") { if (packet.Kind == "hid") overlay.Navigate("confirm"); else overlay.Shoot(PacketPoint(packet)); } else overlay.Navigate(action);
                }
                if (binding.PhysicalId is not null)
                {
                    bool firstSignal = !gunSignals.ContainsKey(binding.PhysicalId); gunSignals[binding.PhysicalId] = DateTimeOffset.UtcNow;
                    if (firstSignal) SendState();
                }
                Send("gun-input", new { player = binding.Player, token = signal.Token, down = signal.Down, action });
                if (learningPlayer == binding.Player && signal.Down && !session.Active)
                {
                    int index = state.Bindings.IndexOf(binding);
                    if (learningControl is not null) {
                        var controls = new Dictionary<string,string>(binding.ControlMap ?? []); controls[learningControl]=signal.Token;
                        state.Bindings[index] = binding with { ControlMap=controls };
                    } else {
                        var updated = new Dictionary<string,string>(map);
                        foreach(var old in updated.Where(kv=>kv.Value==learningAction).Select(kv=>kv.Key).ToArray()) updated.Remove(old);
                        updated[signal.Token]=learningAction!;
                        state.Bindings[index]=binding with { ButtonMap=GunSystems.ValidateMap(updated) };
                    }
                    learningPlayer=null; learningAction=null; learningControl=null; Persist(); return;
                }
            }
        }
        if (session.Active || remoteSession || learningPlayer is not null || binding is not null && (buttonTestPlayer is not null || buttonTestRelease)) return;
        long now = Environment.TickCount64;
        if (packet.Kind == "mouse" && packet.Buttons == 0 && now - lastMove < 30) return;
        if (packet.Kind == "mouse") lastMove = now;
        if (binding is not null && packet.Kind == "mouse")
            foreach (var signal in Signals(packet).Where(s => s.Down))
            {
                string action = (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player,binding.SystemId)).GetValueOrDefault(signal.Token, "none");
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
                    string action = (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player,binding.SystemId)).GetValueOrDefault(signal.Token, "none");
                    if (action == "shoot") menuButtons |= 1; if (action == "reload") menuButtons |= 4;
                }
            var client = web.PointFromScreen(point);
            Send("input", new { packet.DeviceId, kind = packet.Kind, player = binding?.Player ?? 0, x = client.X / Math.Max(1, web.ActualWidth), y = client.Y / Math.Max(1, web.ActualHeight), buttons = menuButtons });
        }
        else Send("input", new { packet.DeviceId, kind = packet.Kind, player = binding?.Player ?? 0, packet.Key, packet.Down, action = binding is null ? null : (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player,binding.SystemId)).GetValueOrDefault("key:" + packet.Key, "none") });
    }
    private void OpenOverlay(int player)
    {
        if (!session.Active || activeGame is null || overlay is not null) return;
        CloseStartupControls();
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
    private void ShowStartupControls(nint handle)
    {
        if(!session.Active || activeGame is null || overlay is not null || closing) return;
        CloseStartupControls();
        try {
            var controls=OverlayControls.Read(activeGame,store.DirectoryPath,state.Bindings);
            var window=new StartupOverlay(activeGame,controls,state.Bindings,handle,session.HasGameForeground);
            startupOverlay=window;
            window.Closed+=(_,_)=> {if(startupOverlay==window)startupOverlay=null;Log("startup controls: closed");};
            window.Show(); Log("startup controls: shown · "+activeGame.Title);
        } catch(Exception error) {CloseStartupControls();Log("startup controls unavailable: "+error.Message);}
    }
    private void CloseStartupControls()
    {
        var window=startupOverlay; startupOverlay=null; window?.Close();
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
        if (packet.Kind == "hid") { yield return ("hid:" + packet.Key, packet.Down); yield break; }
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
        if (scanningGuns || closing || calibration is not null) return;
        scanningGuns = true; Send("busy", new { message = I18n.T("Lightguns werden erkannt und zugeordnet …") });
        try
        {
            var devices = raw.Devices.ToArray(); guns = await Task.Run(() => GunDiscovery.Scan(devices)); gunIssues.Clear();
            foreach (var gun in guns.Where(g => g.SystemId == "rs3"))
            {
                try
                {
                    if (!gun.DriverHealthy) throw new IOException(string.Join("; ", gun.Issues));
                    if (gun.Port is null || gun.MouseId is null || gun.KeyboardId is null) throw new IOException(I18n.T("USB-Maus, Tastatur oder COM-Port fehlen. Mausmodus prüfen."));
                    int player = await serial.Probe(gun.Port);
                    if (player > 2) throw new IOException(I18n.F($"Gun meldet P{player}. Die Oberfläche unterstützt aktuell P1/P2."));
                    var existing = state.Bindings.FirstOrDefault(b => b.Player == player);
                    if (existing is not null && !string.Equals(existing.MouseId, gun.MouseId, StringComparison.OrdinalIgnoreCase) && existing.PhysicalId != gun.Id && guns.Any(g => g.Id == existing.PhysicalId || string.Equals(g.MouseId, existing.MouseId, StringComparison.OrdinalIgnoreCase)))
                        throw new IOException(I18n.F($"P{player} ist schon durch eine andere angeschlossene Gun belegt. DIP-Spielerzuordnung prüfen."));
                    if (state.Bindings.Any(b => b.Player != player && (b.PhysicalId == gun.Id || b.MouseId == gun.MouseId)))
                        throw new IOException(I18n.T("Dieselbe Gun ist bereits einem anderen Spieler zugeordnet."));
                    var feedback = existing?.Feedback ?? new();
                    await serial.Command(gun.Port, player, GunSystems.ReaperConfiguration(feedback));
                    state.Bindings.RemoveAll(b => b.Player == player);
                    state.Bindings.Add(new(player, gun.MouseId, gun.KeyboardId, gun.Port, "rs3", gun.Id, existing is null ? GunSystems.DefaultMap(player) : GunSystems.UpgradeMap(existing), feedback, true, existing?.ControlMap));
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
                if (player != 0) state.Bindings.Add(new(player, gun.MouseId!, gun.KeyboardId, gun.Port, gun.SystemId, gun.Id, GunSystems.DefaultMap(player,gun.SystemId)));
            }
            if (!closing) { store.Save(state); SendState(); }
        }
        catch (Exception error) { Log("gun discovery: " + error.Message); Send("error", new { message = error.Message }); }
        finally { scanningGuns = false; Send("busy", new { message = "" }); }
    }
    private static string? Folder(string title) { var picker = new OpenFolderDialog { Title = title }; return picker.ShowDialog() == true ? picker.FolderName : null; }
    private static string? File(string title, string filter = "Windows-Anwendung|*.exe") { var picker = new OpenFileDialog { Title = title, Filter = I18n.T(filter) }; return picker.ShowDialog() == true ? picker.FileName : null; }
    private async Task Handle(JsonElement message)
    {
        string type = message.GetProperty("type").GetString() ?? "";
        JsonElement payload = message.TryGetProperty("payload", out var p) ? p : default;
        string Str(string name) => payload.GetProperty(name).GetString() ?? "";
        int Player() => payload.GetProperty("player").GetInt32() is var n && n is >= 1 and <= 2 ? n : throw new ArgumentException(I18n.T("Ungültiger Spieler."));
        GameEntry Game() => state.Games.FirstOrDefault(g => g.Id == Str("id")) ?? throw new ArgumentException(I18n.T("Das Spiel existiert nicht mehr."));
        if (type == "set-language")
        {
            string language = Str("language");
            if (language is not ("en" or "de")) throw new ArgumentException("Unsupported language.");
            state = state with { Settings = state.Settings with { Language = language } };
            I18n.Language = language; exitButton.Content = I18n.T("App schließen · Windows"); Persist(); return;
        }
        if (type == "close") { Close(); return; }
        if (type == "browse-path") { picker.Browse(Str("path")); return; }
        if (type == "choose-path") { picker.Choose(Str("path")); return; }
        if (type == "cancel-picker") { picker.Cancel(); return; }
        if (picker.Active) throw new InvalidOperationException(I18n.T("Bitte zuerst die Dateiauswahl schließen."));
        if (type == "ready") { ready = true; SendState(); await DiscoverGuns(); await CheckDependencies(); if (state.Settings.CheckForUpdates) _ = CheckAppUpdate(); return; }
        if (type == "button-test")
        {
            if (session.Active) return;
            buttonTestPlayer = payload.TryGetProperty("player", out var testPlayer) && testPlayer.ValueKind == JsonValueKind.Number ? Player() : null;
            if (!buttonTestRelease) buttonTestHold.Reset();
            gestures.Clear(); return;
        }
        if (type == "end-game") { await session.End(); return; }
        if (type == "show-overlay") { OpenOverlay(0); overlay?.Arm(); return; }
        if (type == "cancel-bind") { bindPlayer = null; bindMouse = null; SendState(); return; }
        if (session.Active && type != "fullscreen") throw new InvalidOperationException(I18n.T("Bitte zuerst das laufende Spiel beenden."));
        if (type == "cancel-learn") { learningPlayer = null; learningAction = null; learningControl = null; SendState(); return; }
        if(calibration is not null) throw new InvalidOperationException(I18n.T("Bitte zuerst die Kalibrierung beenden."));
        if (busy || scanningGuns) throw new InvalidOperationException(I18n.T("Die laufende Aktion wird noch abgeschlossen."));
        switch (type)
        {

            case "check-updates": await CheckAppUpdate(); break;
            case "update-preference":
                state = state with { Settings = state.Settings with { CheckForUpdates = payload.GetProperty("enabled").GetBoolean() } }; Persist();
                if (state.Settings.CheckForUpdates) _ = CheckAppUpdate(); break;
            case "install-update":
                if (availableUpdate is null || checkingUpdate) throw new InvalidOperationException(I18n.T("Bitte zuerst nach einem Update suchen."));
                busy = true; updateStatus = "downloading"; updateProgress = 0; updateError = ""; SendState();
                try
                {
                    var release = availableUpdate;
                    string installer = await updater.Download(release, store.DirectoryPath, new Progress<int>(value => { if (!closing) { updateProgress = value; SendState(); } }));
                    if (closing) return;
                    store.Save(state); AppUpdater.Install(installer, store.DirectoryPath);
                    updateStatus = "installing"; SendState(); Close();
                }
                catch (OperationCanceledException) when (closing) { }
                catch (Exception error) { updateStatus = "error"; updateError = error.Message; }
                finally { busy = false; if (!closing) SendState(); }
                break;
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
                            if (code == 3010) Send("notice", new { message = I18n.T("Die Laufzeit meldet einen nötigen Neustart. Bitte später selbst neu starten.") });
                            else if (code != 0) { Send("notice", new { message = $"{package.Name}: Installation abgebrochen oder fehlgeschlagen (Code {code})." }); break; }
                        }
                    }
                    finally { busy = false; Send("busy", new { message = "" }); await CheckDependencies(); }
                    break;
                }
            case "scan-installations":
                {
                    busy = true; Send("busy", new { message = I18n.T("Bekannte Spieleordner werden durchsucht …") });
                    try
                    {
                        var roots = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                        {
                            roots.Add(drive.RootDirectory.FullName);
                            foreach (var folder in new[] { "Games", "Arcade", "Emulators", "RetroBat", "LaunchBox", "TeknoParrot" }) roots.Add(Path.Combine(drive.RootDirectory.FullName, folder));
                        }
                        installations = await Task.Run(() => InstallationFinder.Find(roots)); SendState();
                        Send("notice", new { message = installations.Count == 0 ? I18n.T("In den üblichen Ordnern nichts gefunden. Du kannst den richtigen Ordner über die großen Schaltflächen wählen.") : I18n.F($"{installations.Count} Programme gefunden. Wähle die gewünschte Installation.") });
                    }
                    finally { busy = false; Send("busy", new { message = "" }); }
                    break;
                }
            case "set-calibration":
                {
                    string? exe = await picker.Open(I18n.T("Hersteller-Kalibrierprogramm auswählen"), false); if (exe is null) return;
                    state = state with { Settings = state.Settings with { CalibrationTool = exe } }; Persist(); break;
                }
            case "run-calibration":
                {
                    if (remoteSession) throw new InvalidOperationException(I18n.T("Die Gun am echten Bildschirm lokal kalibrieren. Remote Desktop verändert die Anzeige."));
                    string exe = state.Settings.CalibrationTool ?? throw new InvalidOperationException(I18n.T("Bitte zuerst das Herstellerprogramm auswählen."));
                    await session.Run(GameImporter.Pc(exe, "RS3-Kalibrierung"), store.DirectoryPath, state.Bindings); break;
                }
            case "prepare-calibration":
                busy=true; Send("busy",new {message=I18n.T("Hersteller-Kalibrierung wird vorbereitet …")});
                try { await ReaperCalibrationPackage.Prepare(store.DirectoryPath); Send("notice",new {message=I18n.T("RS3-Kalibrierung bereit. Am echten Bildschirm P1 oder P2 wählen und kalibrieren.")}); }
                finally {busy=false;Send("busy",new {message=""});SendState();}
                break;
            case "calibrate-rs3":
                {
                    if(remoteSession) throw new InvalidOperationException(I18n.T("Die Gun am echten Bildschirm lokal kalibrieren. Remote Desktop verändert die Anzeige."));
                    var binding=state.Bindings.SingleOrDefault(b=>b.Player==Player() && b.SystemId=="rs3") ?? throw new InvalidOperationException(I18n.T("Keine Gun zugeordnet."));
                    if(binding.SerialPort is null || !guns.Any(g=>g.MouseId==binding.MouseId && g.DriverHealthy)) throw new IOException(I18n.T("Die ausgewählte Gun ist nicht mehr verbunden."));
                    string pid=$"vid_0483&pid_{0x574f+binding.Player:x4}";
                    if(guns.Count(g=>g.MouseId?.Contains(pid,StringComparison.OrdinalIgnoreCase)==true)!=1) throw new IOException(I18n.T("Gun-ID und Spielerzuordnung stimmen nicht überein."));
                    busy=true; string dll;
                    try { dll=await ReaperCalibrationPackage.Prepare(store.DirectoryPath); await serial.Command(binding.SerialPort,binding.Player,GunSystems.ReaperConfiguration((binding.Feedback??new()) with {Aspect="16:9"})); }
                    catch { try { await serial.Command(binding.SerialPort,binding.Player,GunSystems.ReaperConfiguration(binding.Feedback??new())); } catch(Exception e) {Log("calibration preparation restore: "+e.Message);} throw; }
                    finally {busy=false;Send("busy",new {message=""});}
                    if(closing) { try { await serial.Command(binding.SerialPort,binding.Player,GunSystems.ReaperConfiguration(binding.Feedback??new())); } catch(Exception e) {Log("calibration closing restore: "+e.Message);} return; }
                    ReaperCalibrationWindow window;
                    try { window=new ReaperCalibrationWindow(this,binding,dll,()=>raw.Devices.Any(d=>d.Id.Equals(binding.MouseId,StringComparison.OrdinalIgnoreCase))); }
                    catch { try { await serial.Command(binding.SerialPort,binding.Player,GunSystems.ReaperConfiguration(binding.Feedback??new())); } catch(Exception e) {Log("calibration module restore: "+e.Message);} throw; }
                    calibration=window;
                    window.Closed+=async (_,_)=> {
                        calibration=null;
                        try { await serial.Command(binding.SerialPort,binding.Player,GunSystems.ReaperConfiguration(binding.Feedback??new())); }
                        catch(Exception e) {Log("calibration restore: "+e.Message);}
                        if(!closing) {Activate();SendState();Send("notice",new {message=window.CommandsSent?I18n.T("Kalibrierungsbefehle gesendet. Jetzt mit dem Zieltest die tatsächliche Genauigkeit prüfen."):I18n.T("Kalibrierung abgebrochen. Vorherige Kalibrierung bleibt erhalten.")});}
                    };
                    window.Show(); window.Activate(); break;
                }
            case "validate-library":
                {
                    var warnings = new List<string>();
                    foreach (var game in state.Games.ToArray())
                    {
                        LaunchRules.RepairTeknoParrotPath(game);
                        TeknoGunSetup.Configure(game,state.Bindings.Where(b=>guns.Any(g=>g.MouseId==b.MouseId)));
                        var checkedGame = LaunchRules.Validate(game);
                        state.Games[state.Games.IndexOf(game)] = checkedGame;
                        warnings.AddRange((checkedGame.SetupIssues ?? []).Select(issue => game.Title + ": " + issue));
                    }
                    Persist();
                    Send("import-result", new { count = state.Games.Count(g => g.Status != "needs-setup"), warnings, validation = true }); break;
                }
            case "import-collection":
                {
                    string? path = await picker.Open(I18n.T("spiele.json aus der Übergabe auswählen"), false, [".json"]);
                    if (path is not null) { await Import(() => CollectionImporter.Read(path)); await CheckDependencies(); }
                    break;
                }
            case "test-feedback":
                {
                    if (remoteSession) throw new InvalidOperationException(I18n.T("Feedback am lokalen Bildschirm testen und die Gun dabei in der Hand halten."));
                    var b = state.Bindings.First(x => x.Player == Player());
                    if (b.SystemId != "rs3" || b.SerialPort is null) throw new InvalidOperationException(I18n.T("Bitte zuerst den Gun-COM-Port zuordnen."));
                    string effect = Str("effect");
                    var feedback = b.Feedback ?? new();
                    if (effect is "recoil" or "combined" && !feedback.Recoil || effect is "rumble" or "combined" && !feedback.Rumble) throw new InvalidOperationException(I18n.T("Diesen Testeffekt zuerst in der Feedback-Auswahl aktivieren."));
                    string[] commands = effect switch { "recoil" => ["ZS", "Z5", "ZX"], "rumble" => ["ZS", "ZZ", "ZX"], "combined" => ["ZS", "Z5", "ZZ", "ZX"], _ => throw new ArgumentException(I18n.T("Unbekannter Feedbacktest.")) };
                    busy = true;
                    try { await serial.Command(b.SerialPort, b.Player, commands); Send("notice", new { message = I18n.T("Einzelimpuls gesendet. Stärke und Gefühl beurteilst du an der Gun; dies bestätigt noch kein Spielefeedback.") }); }
                    finally { busy = false; }
                    break;
                }
            case "refresh": raw.Refresh(); await DiscoverGuns(); break;
            case "learn-control":
                {
                    int player=Player(); var binding=state.Bindings.First(b=>b.Player==player); string control=Str("control");
                    if(!GunSystems.ValidControl(binding.SystemId,control)) throw new ArgumentException(I18n.T("Ungültige Gun-Belegung."));
                    learningPlayer=player; learningControl=control; learningAction=null; SendState(); break;
                }
            case "learn-button":
                {
                    int player = Player(); string action = Str("action");
                    if (!GunSystems.Actions.Contains(action) || action == "none") throw new ArgumentException(I18n.T("Unbekannte Aktion."));
                    if (!state.Bindings.Any(b => b.Player == player)) throw new InvalidOperationException(I18n.T("Bitte zuerst die Gun einrichten."));
                    learningPlayer = player; learningAction = action; learningControl = null; SendState(); break;
                }
            case "reset-button-map":
                {
                    int i = state.Bindings.FindIndex(b => b.Player == Player()); if (i < 0) throw new InvalidOperationException(I18n.T("Keine Gun zugeordnet."));
                    state.Bindings[i] = state.Bindings[i] with { ButtonMap = GunSystems.DefaultMap(Player(),state.Bindings[i].SystemId) }; Persist(); break;
                }
            case "set-gun-feedback":
                {
                    int i = state.Bindings.FindIndex(b => b.Player == Player()); if (i < 0) throw new InvalidOperationException(I18n.T("Keine Gun zugeordnet."));
                    var feedback = JsonSerializer.Deserialize<GunFeedback>(payload.GetProperty("feedback"), JsonDefaults.Options) ?? throw new ArgumentException(I18n.T("Einstellungen fehlen."));
                    _ = GunSystems.ReaperConfiguration(feedback);
                    var b = state.Bindings[i];
                    if (b.SystemId != "rs3" || b.SerialPort is null) throw new InvalidOperationException(I18n.T("Diese Konfiguration benötigt eine erkannte RS3 mit COM-Port."));
                    await serial.Command(b.SerialPort, b.Player, GunSystems.ReaperConfiguration(feedback));
                    state.Bindings[i] = b with { Feedback = feedback, SoftwareConfigured = true }; Persist();
                    Send("notice", new { message = I18n.T("Mausmodus, Bildformat und Offscreen-Reload angefordert. Feedback-Auswahl für die Testimpulse gespeichert; die DIP-Schalter an der Gun bleiben maßgeblich.") }); break;
                }
            case "setup-gun":
                {
                    string system = Str("system"); if (!GunSystems.Catalog.Any(g => g.Id == system)) throw new ArgumentException(I18n.T("Unbekanntes Lightgun-System."));
                    if (system == "rs3") { await DiscoverGuns(); busy=true; try {await ReaperCalibrationPackage.Prepare(store.DirectoryPath);} finally {busy=false;SendState();} break; }
                    busy = true;
                    try { var path = await GunSoftware.Prepare(system, store.DirectoryPath, text => Send("busy", new { message = text })); Send("notice", new { message = path }); }
                    finally { busy = false; Send("busy", new { message = "" }); SendState(); }
                    break;
                }
            case "assign-gun":
                {
                    int player = Player(); var gun = guns.First(g => g.Id == Str("id"));
                    if (gun.SystemId == "rs3") throw new InvalidOperationException(I18n.T("RS3-Spieler werden über die Hardware-ID zugeordnet. DIP-Spieler prüfen und neu erkennen."));
                    if (gun.MouseId is null) throw new InvalidOperationException(I18n.T("Noch kein Maus-Eingang der Gun vorhanden. Zuerst Hersteller-Software einrichten."));
                    if (state.Bindings.Any(b => b.Player != player && b.PhysicalId == gun.Id)) throw new InvalidOperationException(I18n.T("Die Gun gehört schon zu einem anderen Spieler."));
                    state.Bindings.RemoveAll(b => b.Player == player); state.Bindings.Add(new(player, gun.MouseId, gun.KeyboardId, gun.Port, gun.SystemId, gun.Id, GunSystems.DefaultMap(player,gun.SystemId))); Persist(); break;
                }
            case "bind": bindPlayer = Player(); bindMouse = null; SendState(); break;
            case "unbind": state.Bindings.RemoveAll(b => b.Player == Player()); Persist(); break;
            case "favorite": { var game = Game(); state.Games[state.Games.IndexOf(game)] = game with { Favorite = !game.Favorite }; Persist(); break; }
            case "remove-game": state.Games.Remove(Game()); Persist(); break;
            case "set-port":
                {
                    int player = Player(); string port = Str("port");
                    if (port != "" && !SerialPort.GetPortNames().Contains(port, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException(I18n.T("Der Port ist nicht vorhanden."));
                    if (port != "" && state.Bindings.Any(b => b.Player != player && b.SerialPort == port)) throw new ArgumentException(I18n.T("Dieser Port ist bereits vergeben."));
                    int index = state.Bindings.FindIndex(b => b.Player == player); if (index < 0) throw new InvalidOperationException(I18n.T("Bitte zuerst die Gun zuordnen."));
                    state.Bindings[index] = state.Bindings[index] with { SerialPort = port == "" ? null : port }; Persist(); break;
                }
            case "test-serial":
                {
                    var binding = state.Bindings.First(b => b.Player == Player()); if (binding.SystemId != "rs3" || binding.SerialPort is null) throw new InvalidOperationException(I18n.T("Bitte den RS3-COM-Port auswählen."));
                    busy = true; try { int number = await serial.Command(binding.SerialPort, binding.Player); Send("notice", new { message = I18n.F($"RS3 antwortet: Spieler {number}. Zielgenauigkeit und Recoil sind damit noch nicht geprüft.") }); } finally { busy = false; }
                    break;
                }
            case "mouse-mode":
                {
                    var b = state.Bindings.First(b => b.Player == Player()); if (b.SystemId != "rs3" || b.SerialPort is null) throw new InvalidOperationException(I18n.T("Bitte den RS3-COM-Port auswählen."));
                    busy = true; try { await serial.Command(b.SerialPort, b.Player, "ZS", "ZM", "ZW", "ZX"); Send("notice", new { message = I18n.T("Mausmodus und 16:9 angefordert. Mit dem Zieltest prüfen.") }); } finally { busy = false; }
                    break;
                }
            case "import-tekno":
                {
                    string? root = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("path", out var detected) && installations.Any(i => i.Kind == "tekno" && i.Path == detected.GetString()) ? Path.GetDirectoryName(detected.GetString()) : await picker.Open(I18n.T("TeknoParrot-Ordner auswählen"), true); if (root is null) return;
                    await Import(() => GameImporter.TeknoParrot(root)); break;
                }
            case "import-mame":
                {
                    string? exe = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("path", out var detectedMame) && installations.Any(i => i.Kind == "mame" && i.Path == detectedMame.GetString()) ? detectedMame.GetString() : await picker.Open(I18n.T("MAME-Anwendung auswählen"), false); if (exe is null) return;
                    string? roms = await picker.Open(I18n.T("MAME-ROM-Ordner auswählen"), true); if (roms is null) return;
                    busy = true; Send("busy", new { message = I18n.T("MAME-Spielekatalog wird gelesen …") });
                    try
                    {
                        var info = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                        info.ArgumentList.Add("-listxml"); using var process = Process.Start(info) ?? throw new IOException(I18n.T("MAME startet nicht."));
                        var error = process.StandardError.ReadToEndAsync();
                        var catalogTask = Task.Run(() => GameImporter.MameGunCatalog(process.StandardOutput.BaseStream));
                        try { await Task.WhenAll(catalogTask, process.WaitForExitAsync(), error).WaitAsync(TimeSpan.FromMinutes(2)); }
                        catch { if (!process.HasExited) process.Kill(); throw; }
                        if (process.ExitCode != 0) throw new IOException(I18n.T("MAME-Katalog konnte nicht gelesen werden: ") + await error);
                        ApplyImport(GameImporter.Mame(exe, roms, await catalogTask));
                    }
                    finally { busy = false; Send("busy", new { message = "" }); }
                    break;
                }
            case "add-pc": { string? exe = await picker.Open(I18n.T("Lightgun-Spiel auswählen"), false); if (exe is not null) ApplyImport(new([GameImporter.Pc(exe)], [])); break; }
            case "add-cover":
                {
                    var game = Game(); string? file = await picker.Open(I18n.T("Cover auswählen"), false, [".png", ".jpg", ".jpeg", ".webp"]); if (file is null) return;
                    if (new FileInfo(file).Length > 8_000_000) throw new IOException(I18n.T("Das Cover ist größer als 8 MB."));
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
                { var game = Game(); string aspect = Str("aspect"); if (aspect is not ("4:3" or "16:9")) throw new ArgumentException(I18n.T("Ungültiges Bildformat.")); state.Games[state.Games.IndexOf(game)] = game with { Aspect = aspect }; Persist(); break; }
            case "mark-tested":
                { var game = Game(); state.Games[state.Games.IndexOf(game)] = game with { Status = "tested" }; Persist(); break; }
            case "launch": await Launch(Game()); break;
            case "export-diagnostics":
                {
                    var picker = new SaveFileDialog { Title = I18n.T("Diagnose speichern"), Filter = "JSON|*.json", FileName = "reaper-diagnose.json" }; if (picker.ShowDialog() != true) return;
                    System.IO.File.WriteAllText(picker.FileName, JsonSerializer.Serialize(new
                    {
                        version = AppUpdater.Current,
            update = new { status = updateStatus, release = availableUpdate, progress = updateProgress, error = updateError, checkedAt = updateChecked },
            remoteSession,
            installations,
            calibrationTool = state.Settings.CalibrationTool is null ? null : Path.GetFileName(state.Settings.CalibrationTool),
                        os = Environment.OSVersion.ToString(),
                        devices = raw.Devices,
                        bindings = state.Bindings,
                        games = state.Games.Select(g => new { g.Title, g.Platform, g.Status, starterExists = System.IO.File.Exists(g.Executable) })
                    }, JsonDefaults.Options)); Send("notice", new { message = I18n.T("Diagnose gespeichert. Sie enthält Gerätekennungen und keine API-Schlüssel.") }); break;
                }
            default: throw new ArgumentException(I18n.T("Unbekannte Aktion."));
        }
        if ((type is "import-tekno" or "import-mame" or "add-pc") && state.Settings.CoverKey is not null)
            await Covers();
        if (type is "import-tekno" or "import-mame" or "add-pc" or "validate-library") await CheckDependencies();
    }
    private async Task Import(Func<ImportResult> operation)
    { busy = true; Send("busy", new { message = I18n.T("Spiele werden eingelesen …") }); try { ApplyImport(await Task.Run(operation)); } finally { busy = false; Send("busy", new { message = "" }); } }
    private void ApplyImport(ImportResult result)
    { LibraryStore.Merge(state, result.Games); Persist(); Log($"import: {result.Games.Count} entries"); Send("import-result", new { count = result.Games.Count, warnings = result.Warnings }); }
    private async Task Covers()
    {
        if (state.Settings.CoverKey is null) throw new InvalidOperationException(I18n.T("Bitte zuerst einen SteamGridDB-API-Schlüssel hinterlegen oder eigene Cover auswählen."));
        string key = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(state.Settings.CoverKey), null, DataProtectionScope.CurrentUser));
        busy = true; int found = 0; List<string> missing = []; Send("busy", new { message = I18n.T("Cover werden ergänzt …") });
        try
        {
            var service = new CoverService(http, Path.Combine(store.DirectoryPath, "covers"));
            foreach (var game in state.Games.Where(g => g.Cover is null).ToArray())
            {
                string? cover = await service.Download(game.Title, key, CancellationToken.None);
                if (cover is null) missing.Add(game.Title); else { state.Games[state.Games.IndexOf(game)] = game with { Cover = cover }; found++; store.Save(state); }
                await Task.Delay(250);
            }
            Send("notice", new { message = I18n.F($"{found} Cover ergänzt. {missing.Count} Titel ohne eindeutiges Cover.") });
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
        busy = true; Send("busy", new { message = I18n.T("Laufzeiten für Spiele und Emulatoren werden geprüft …") });
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
            Send("dependency-blocked", new { message = I18n.T("Für dieses Spiel fehlen Laufzeiten. Du kannst sie hier installieren und danach erneut starten.") });
            return;
        }
        busy = true; launching = true; var changed = new List<GunBinding>();
        try
        {
            raw.Refresh(); SupermodelSetup.Configure(game,state.Bindings,raw.Devices);
            if(DolphinSetup.IsDolphin(game) && state.Bindings.Any(b=>b.Player==1 && b.SystemId=="rs3" && guns.Any(g=>g.MouseId==b.MouseId)))
            {
                Send("busy",new {message=I18n.T("Dolphin-Spielprofil wird für RS3 vorbereitet …")});
                var pack=await DolphinAccuracyPackage.Prepare(store.DirectoryPath);
                if(!DolphinSetup.Configure(game,pack,state.Bindings)) Send("notice",new {message=I18n.T("Für diese Spielregion liegt kein geprüftes Dolphin-Profil vor. Bestehende Belegung wird verwendet.")});
            }
            foreach (var binding in state.Bindings.Where(b => b.SystemId == "rs3" && b.SerialPort is not null && guns.Any(g => g.Id == b.PhysicalId || g.MouseId == b.MouseId)))
            {
                changed.Add(binding);
                await serial.Command(binding.SerialPort!, binding.Player, GunSystems.ReaperConfiguration((binding.Feedback ?? new()) with { Aspect = game.Aspect }));
                // Keep a second physical mouse from steering P1's shared Dolphin cursor.
                // P2 remains pending until its independent DirectInput controls are verified.
                if(DolphinSetup.IsDolphin(game) && binding.Player==2) await serial.Command(binding.SerialPort!,binding.Player,["ZJ"]);
            }
            foreach (var gesture in gestures.Values) gesture.Reset();
            foreach (var hold in triggerHolds.Values) hold.Reset(); activeGame = game;
            state.Games[state.Games.IndexOf(game)] = game with { LastPlayed = DateTimeOffset.UtcNow }; store.Save(state); Log("launch: " + game.Title);
            Send("busy", new { message = "" });
            await session.Run(game, store.DirectoryPath, state.Bindings.Where(b => guns.Any(g => g.MouseId is not null && string.Equals(g.MouseId, b.MouseId, StringComparison.OrdinalIgnoreCase))));
        }
        finally
        {
            foreach (var binding in changed)
            { try { await serial.Command(binding.SerialPort!, binding.Player, GunSystems.ReaperConfiguration(binding.Feedback ?? new())); } catch (Exception e) { Log("restore: " + e.Message); Send("error", new { message = I18n.T("Menümodus konnte nicht wiederhergestellt werden: ") + e.Message }); } }
            busy = false; launching = false; activeGame = null;
            Send("busy", new { message = "" });
            if (closeRequested) Close(); else SendState();
        }
    }
}
