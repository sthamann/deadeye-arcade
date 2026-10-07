using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Reaper.Windows;

/// <summary>F12 also reaches the launcher when RDP does not deliver Raw Input.</summary>
internal sealed class EmergencyExit : IDisposable
{
    private delegate nint KeyboardCallback(int code, nint message, nint data);
    private readonly KeyboardCallback callback;
    private readonly Action requestExit;
    private nint hook;
    private bool requested;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int type, KeyboardCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? module);

    public EmergencyExit(Action requestExit)
    {
        this.requestExit = requestExit;
        callback = OnKeyboard;
        hook = SetWindowsHookExW(13, callback, GetModuleHandleW(null), 0);
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && (message == 0x100 || message == 0x104) && Marshal.ReadInt32(data) == 0x7B && !requested)
        {
            requested = true;
            requestExit(); // Queue work; never wait inside the keyboard callback.
        }
        return CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (hook != 0) { UnhookWindowsHookEx(hook); hook = 0; }
        GC.KeepAlive(callback);
    }
}
