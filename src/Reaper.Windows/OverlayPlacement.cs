using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Reaper.Windows;

internal static class OverlayPlacement
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint handle, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint handle, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);

    public static void Place(Window window, nint gameWindow, bool introduction)
    {
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(gameWindow, 2), ref monitor)) throw new IOException("Cannot locate game monitor.");
        var handle = new WindowInteropHelper(window).Handle;
        // Move first, so Windows supplies THIS window's DPI on the game's monitor.
        // Old games often report 96 DPI even when Windows scales at 200%.
        // Keep native screen coordinates: dividing a monitor's origin breaks mixed-DPI desktops.
        SetWindowPos(handle, 0, monitor.Monitor.Left, monitor.Monitor.Top, 0, 0, 0x15);
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96d;
        int width = monitor.Monitor.Right - monitor.Monitor.Left, height = monitor.Monitor.Bottom - monitor.Monitor.Top;
        int margin = (int)Math.Round(24 * scale);
        int x = monitor.Monitor.Left, y = monitor.Monitor.Top;
        if (introduction)
        {
            width = Math.Max(1, Math.Min((int)Math.Round(1320 * scale), width - 2 * margin));
            height = Math.Max(1, Math.Min((int)Math.Round(720 * scale), (int)(height * .67)));
            x += margin; y = monitor.Monitor.Bottom - height - margin;
        }
        window.Width = width / scale; window.Height = height / scale;
        SetWindowPos(handle, 0, x, y, width, height, 0x14);
    }
}
