using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Reaper.Core;

namespace Reaper.Windows;

/// <summary>Dedicated message pump: independent of WebView, trigger and game focus.</summary>
internal sealed class EmergencyExit : IDisposable
{
    private delegate nint KeyboardCallback(int code, nint message, nint data);
    private readonly KeyboardCallback callback;
    private readonly Action requestExit;
    private readonly Action? requestOverlay;
    private readonly HashSet<int> down = [];
    private readonly (int Start, int Coin)[] pairs;
    private readonly EmergencyChord[] hookChords, pollChords;
    private readonly Thread thread;
    private Dispatcher? dispatcher;
    private nint hook;
    private bool overlayDown;
    private int requested, disposed;
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookExW(int type, KeyboardCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? module);

    public EmergencyExit(Action requestExit, Action? requestOverlay = null, IEnumerable<GunBinding>? bindings = null)
    {
        this.requestExit = requestExit; this.requestOverlay = requestOverlay;
        callback = OnKeyboard;
        pairs = (bindings ?? []).Select(b => StartupControls.Hardware(b))
            .Select(c => (Start: Key(c.FirstOrDefault(x => x.Id == "start")?.Token), Coin: Key(c.FirstOrDefault(x => x.Id == "coin")?.Token)))
            .Where(p => p.Start > 0 && p.Coin > 0 && p.Start != p.Coin).Distinct().ToArray();
        if (pairs.Length == 0) pairs = [(0x31, 0x35), (0x32, 0x36)];
        hookChords = pairs.Select(_ => new EmergencyChord()).ToArray(); pollChords = pairs.Select(_ => new EmergencyChord()).ToArray();
        thread = new Thread(Run) { IsBackground = true, Name = "Deadeye emergency exit" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    private static int Key(string? token) => token?.StartsWith("key:") == true && int.TryParse(token[4..], out var k) && k is > 0 and < 256 ? k : 0;
    private void Run()
    {
        dispatcher = Dispatcher.CurrentDispatcher;
        if (Volatile.Read(ref disposed) != 0) return;
        hook = SetWindowsHookExW(13, callback, GetModuleHandleW(null), 0);
        // Polling remains useful even if Windows removes a hook or a game consumes keys.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => {
            for (int i = 0; i < pairs.Length; i++)
            {
                var p = pairs[i]; long now = Environment.TickCount64;
                bool observed = hookChords[i].Sample(down.Contains(p.Start), down.Contains(p.Coin), now);
                bool polled = pollChords[i].Sample((GetAsyncKeyState(p.Start) & 0x8000) != 0, (GetAsyncKeyState(p.Coin) & 0x8000) != 0, now);
                if (observed || polled) RequestExit();
            }
        };
        timer.Start();
        if (Volatile.Read(ref disposed) != 0) dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        try { Dispatcher.Run(); }
        finally { timer.Stop(); if (hook != 0) UnhookWindowsHookEx(hook); hook = 0; GC.KeepAlive(callback); }
    }
    private void RequestExit()
    {
        if (Interlocked.Exchange(ref requested, 1) != 0 || Volatile.Read(ref disposed) != 0) return;
        _ = Task.Run(requestExit); // Never queue termination behind the frontend dispatcher.
    }
    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            int key = Marshal.ReadInt32(data);
            bool pressed = message == 0x100 || message == 0x104, released = message == 0x101 || message == 0x105;
            if (pressed) down.Add(key); else if (released) down.Remove(key);
            if (key == 0x79 && requestOverlay is not null)
            {
                if (released) overlayDown = false;
                if (pressed && !overlayDown) { overlayDown = true; _ = Task.Run(requestOverlay); }
                return 1;
            }
            if (key == 0x7B && pressed) RequestExit();
        }
        return CallNextHookEx(hook, code, message, data);
    }
    public void Dispose()
    {
        Interlocked.Exchange(ref disposed, 1);
        dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
    }
}
