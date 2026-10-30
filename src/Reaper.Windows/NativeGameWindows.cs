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
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window,uint flags);
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
    public static nint UnrealRenderer(int processId,nint current,nint fallback,nint foreground)
    {
        bool Renderer(nint window)
        {
            if(window==0||!IsWindowVisible(window)||IsIconic(window))return false;
            GetWindowThreadProcessId(window,out uint owner);
            if(owner!=(uint)processId)return false;
            var name=new StringBuilder(128);GetClassNameW(window,name,name.Capacity);
            return name.ToString().Equals("UnrealWindow",StringComparison.Ordinal);
        }
        // Unreal games can retain their first window after replacing it.
        // Follow only a visible renderer of this exact owned process, keeping
        // it when a modal dialog, overlay or another application is raised.
        return Renderer(foreground)?foreground:Renderer(current)?current:Renderer(fallback)?fallback:0;
    }
    public static bool UnrealInputForeground(int processId,nint renderer,nint foreground)
    {
        if(renderer==0||foreground==0||renderer==foreground||!IsWindowVisible(renderer)||IsIconic(renderer)
            ||IsWindowVisible(foreground)||IsIconic(foreground)||GetAncestor(foreground,2)!=foreground)return false;
        bool OwnedUnreal(nint window)
        {
            GetWindowThreadProcessId(window,out uint owner);
            if(owner!=(uint)processId)return false;
            var name=new StringBuilder(128);GetClassNameW(window,name,name.Capacity);
            return name.ToString().Equals("UnrealWindow",StringComparison.Ordinal);
        }
        // Some packaged games activate a hidden top-level Unreal input window
        // while continuing to render into their visible window. Never use the
        // hidden handle for overlays, presentation or renderer availability.
        return OwnedUnreal(renderer)&&OwnedUnreal(foreground);
    }
}
