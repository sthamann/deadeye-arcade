using System.Runtime.InteropServices;
using Reaper.Core;
namespace Reaper.Windows;

internal static class GamePresentation
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint handle,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint handle,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint handle,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint handle,int index,nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint handle,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool SetMenu(nint handle,nint menu);
    private static bool IsModel2(GameEntry game) => Path.GetFileName(game.Executable).ToLowerInvariant() is "emulator.exe" or "emulator_multicpu.exe" && game.Platform.Contains("Model 2",StringComparison.OrdinalIgnoreCase);
    public static void Prepare(GameEntry game)
    {
        TeknoDisplaySetup.Configure(game,GetSystemMetrics(0),GetSystemMetrics(1));
        if(IsModel2(game))
        {
            string path=Path.Combine(game.WorkingDirectory,"EMULATOR.INI");
            DolphinSetup.WriteMerged(path,"Renderer",new Dictionary<string,string>{["AutoFull"]="0"});
            if((game.Helpers??[]).Any(h=>h.Arguments.Contains("-target=model2")))
                DolphinSetup.WriteMerged(path,"Input",new Dictionary<string,string>{["XInput"]="0"});
        }
    }
    public static void Apply(GameEntry game,nint window)
    {
        if(!TeknoDisplaySetup.Supports(game) && !IsModel2(game)) return;
        var info=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(MonitorFromWindow(window,2),ref info)) return;
        SetWindowLongPtr(window,-16,GetWindowLongPtr(window,-16)&~(nint)0x00cf0000);
        if(IsModel2(game)) SetMenu(window,0);
        SetWindowPos(window,0,info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top,0x34);
    }
}
