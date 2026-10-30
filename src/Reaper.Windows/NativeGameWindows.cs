using System.Runtime.InteropServices;
using System.Text;
using Reaper.Core;

namespace Reaper.Windows;

internal static class NativeGameWindows
{
    private delegate bool EnumWindow(nint window,nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback,nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent,EnumWindow callback,nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint processId);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowTextW(nint window,StringBuilder text,int capacity);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassNameW(nint window,StringBuilder text,int capacity);
    public static string DialogText(nint window)
    {
        var lines=new List<string>();int count=0;
        EnumChildWindows(window,(child,_)=>{
            if(++count>32)return false;
            var name=new StringBuilder(128);GetClassNameW(child,name,name.Capacity);
            // Standard error messages live in static labels. Do not capture
            // editable fields or send messages to an unresponsive game thread.
            if(name.ToString().Equals("Static",StringComparison.OrdinalIgnoreCase) && IsWindowVisible(child))
            {
                var text=new StringBuilder(1024);GetWindowTextW(child,text,text.Capacity);
                if(text.Length>0)lines.Add(text.ToString());
            }
            return true;
        },0);
        return string.Join("\n",lines.Distinct());
    }
    public static nint DolphinRenderer(int processId,nint current)
    {
        var windows=new List<nint>();
        EnumWindows((window,_)=>{
            GetWindowThreadProcessId(window,out uint owner);
            if(owner==(uint)processId && IsWindowVisible(window))
            {
                var title=new StringBuilder(1024);GetWindowTextW(window,title,title.Capacity);
                if(GameRuntimeWindows.DolphinRenderer(title.ToString()))windows.Add(window);
            }
            return true;
        },0);
        // Keep the renderer when a library/configuration window is raised.
        return windows.Contains(current)?current:windows.FirstOrDefault();
    }
}
