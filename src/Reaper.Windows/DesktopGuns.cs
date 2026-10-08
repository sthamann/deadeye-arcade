using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Windows.Threading;
using Microsoft.Win32;
using Reaper.Core;

namespace Reaper.Windows;

// A separate process preserves the frontend's Raw Input registration and lifetime.
internal sealed class DesktopGuns : IDisposable
{
    private readonly RawInput raw = new();
    private readonly LibraryStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade"));
    private readonly Dictionary<int, AimMarker> markers = [];
    private readonly Dictionary<int, (int X, int Y)> points = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly HwndSource source;
    private GunBinding[] bindings = [];
    private readonly EventWaitHandle stop = new(false, EventResetMode.AutoReset, "Local\\DeadeyeDesktopGuns-stop-v1");
    private readonly DateTimeOffset started = DateTimeOffset.Now;
    private long lastReport;
    private DateTime libraryStamp;
    private long lastRefresh;
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);

    public static void Run()
    {
        var initial = new LibraryStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade"));
        if (!initial.Load().Settings.DesktopCrosshairs) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try { using var companion = new DesktopGuns(); app.Run(); }
        catch (Exception error)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade");
            Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "desktop-guns-error.txt"), error.ToString());
        }
    }
    public static void Stop()
    {
        try { using var signal = EventWaitHandle.OpenExisting("Local\\DeadeyeDesktopGuns-stop-v1"); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException) { return; }
        // Installer waits for the companion to release its executable, without killing unrelated processes.
        try { using var mutex = Mutex.OpenExisting("Local\\DeadeyeDesktopGuns-v1"); if (mutex.WaitOne(5000)) mutex.ReleaseMutex(); }
        catch (WaitHandleCannotBeOpenedException) { }
        catch (AbandonedMutexException) { }
    }
    public static void ConfigureStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            key.SetValue("DeadeyeDesktopGuns", "\"" + Environment.ProcessPath + "\" --desktop-guns");
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { Arguments = "--desktop-guns", UseShellExecute = false, CreateNoWindow = true });
        }
        else key.DeleteValue("DeadeyeDesktopGuns", false);
    }
    private DesktopGuns()
    {
        source = new HwndSource(new HwndSourceParameters("Deadeye desktop input") { ParentWindow = new nint(-3), WindowStyle = 0 });
        source.AddHook(Hook); raw.Packet += Input;
        raw.DevicesChanged += () => { points.Clear(); UpdateVisibility(); };
        raw.Register(source.Handle);
        RefreshSettings(); timer.Tick += (_, _) => {
            if (stop.WaitOne(0)) { Application.Current.Shutdown(); return; }
            RefreshSettings(); UpdateVisibility(); Report();
        }; timer.Start(); Report();
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    { raw.Message(message, lParam); return 0; }
    private void RefreshSettings()
    {
        if (Environment.TickCount64 - lastRefresh < 800) return;
        lastRefresh = Environment.TickCount64;
        string path = System.IO.Path.Combine(store.DirectoryPath, "library.json");
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        if (stamp == libraryStamp) return;
        try
        {
            var state = store.Load();
            if (!state.Settings.DesktopCrosshairs) { Application.Current.Shutdown(); return; }
            bindings = state.Bindings.ToArray(); points.Clear(); libraryStamp = stamp;
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or InvalidDataException) { /* Retry an in-progress library update. */ }
    }
    private static bool DesktopForeground()
    {
        nint window = GetForegroundWindow(); if (window == 0) return false;
        GetWindowThreadProcessId(window, out uint pid);
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
    private void Input(RawPacket packet)
    {
        if (packet.Kind != "mouse" || !packet.Absolute || !DesktopForeground()) return;
        var binding = DesktopAim.Binding(bindings, packet.DeviceId); if (binding is null) return;
        // RS3 off-screen reload reports the zero corner; don't leave a false target there.
        if (binding.SystemId == "rs3" && packet.X == 0 && packet.Y == 0) { points.Remove(binding.Player); UpdateVisibility(); return; }
        int left = packet.VirtualDesktop ? GetSystemMetrics(76) : 0, top = packet.VirtualDesktop ? GetSystemMetrics(77) : 0;
        int width = GetSystemMetrics(packet.VirtualDesktop ? 78 : 0), height = GetSystemMetrics(packet.VirtualDesktop ? 79 : 1);
        var position = DesktopAim.Position(packet.X, packet.Y, left, top, width, height);
        if (position is null) { points.Remove(binding.Player); return; }
        points[binding.Player] = position.Value; UpdateVisibility();
    }
    private void UpdateVisibility()
    {
        bool desktop = DesktopForeground();
        if (!desktop) points.Clear(); // Require fresh physical input after returning from a game.
        for (int player = 1; player <= 2; player++)
        {
            bool connected = bindings.Any(b => b.Player == player && raw.Devices.Any(d => d.Id.Equals(b.MouseId, StringComparison.OrdinalIgnoreCase)));
            bool show = desktop && connected && points.ContainsKey(player);
            if (!show) { if (markers.TryGetValue(player, out var hidden)) hidden.Hide(); continue; }
            if (!markers.TryGetValue(player, out var marker)) markers[player] = marker = new AimMarker(player);
            var point = points[player]; marker.Place(point.X, point.Y);
        }
    }
    private void Report()
    {
        if (Environment.TickCount64 - lastReport < 2000) return;
        lastReport = Environment.TickCount64;
        var rows = bindings.OrderBy(b => b.Player).Select(b => new { b.Player, b.SystemId,
            connected = raw.Devices.Any(d => d.Id.Equals(b.MouseId, StringComparison.OrdinalIgnoreCase)),
            positioned = points.ContainsKey(b.Player), visible = markers.GetValueOrDefault(b.Player)?.IsVisible == true });
        try { Directory.CreateDirectory(store.DirectoryPath); File.WriteAllText(Path.Combine(store.DirectoryPath, "desktop-guns-status.json"), JsonSerializer.Serialize(new {
            started, time = DateTimeOffset.Now, pid = Environment.ProcessId, session = Environment.GetEnvironmentVariable("SESSIONNAME"),
            desktop = DesktopForeground(), players = rows }, JsonDefaults.Options)); }
        catch (IOException) { }
    }
    public void Dispose()
    {
        timer.Stop(); stop.Dispose(); raw.Dispose(); source.RemoveHook(Hook); source.Dispose();
        foreach (var marker in markers.Values) marker.Close();
    }
}

internal sealed class AimMarker : Window
{
    private nint handle;
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint insert, int x, int y, int width, int height, uint flags);
    public AimMarker(int player)
    {
        Title = "Deadeye P" + player; Width = 64; Height = 64;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        ShowActivated = false; ShowInTaskbar = false; Focusable = false; IsHitTestVisible = false;
        var blue = new SolidColorBrush(Color.FromRgb(50, 157, 255));
        var canvas = new Canvas { Width = 64, Height = 64 };
        var ring = new Ellipse { Width = 30, Height = 30, Stroke = blue, StrokeThickness = 2.5, Fill = new SolidColorBrush(Color.FromArgb(100, 8, 20, 34)) };
        Canvas.SetLeft(ring, 17); Canvas.SetTop(ring, 17); canvas.Children.Add(ring);
        foreach (var line in new[] { new Line { X1 = 5, Y1 = 32, X2 = 19, Y2 = 32 }, new Line { X1 = 45, Y1 = 32, X2 = 59, Y2 = 32 }, new Line { X1 = 32, Y1 = 5, X2 = 32, Y2 = 19 }, new Line { X1 = 32, Y1 = 45, X2 = 32, Y2 = 59 } })
        { line.Stroke = blue; line.StrokeThickness = 2.5; canvas.Children.Add(line); }
        var label = new TextBlock { Text = "P" + player, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold, Width = 30, TextAlignment = TextAlignment.Center };
        Canvas.SetLeft(label, 17); Canvas.SetTop(label, 24); canvas.Children.Add(label); Content = canvas;
        SourceInitialized += (_, _) => {
            handle = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(handle, -20, GetWindowLongPtr(handle, -20) | 0x08000000 | 0x20 | 0x80);
            HwndSource.FromHwnd(handle).AddHook((nint h, int m, nint w, nint l, ref bool done) => {
                if (m == 0x21) { done = true; return 3; }
                if (m == 0x84) { done = true; return -1; }
                return 0;
            });
        };
    }
    public void Place(int x, int y)
    {
        if (!IsVisible) Show();
        int size = (int)Math.Round(64 * Math.Max(96, GetDpiForWindow(handle)) / 96d);
        SetWindowPos(handle, new nint(-1), x - size / 2, y - size / 2, size, size, 0x10); // NOACTIVATE
    }
}
