namespace Reaper.Core;

public static class GameRuntimeWindows
{
    public static bool KnownLauncher(string path)=>new[]{"steam.exe","steamwebhelper.exe","EpicGamesLauncher.exe","TeknoParrotUi.exe","Hotd3Arcade_Launcher.exe"}.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase);
    // A random owned child can be an updater or launcher. Accept known packaged
    // game runtimes only, inside the explicitly configured game directory.
    public static bool ChildRuntime(string launcher,string directory,string image)
    {
        string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(image);
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
        string name=Path.GetFileName(full);
        if(Path.GetFileName(launcher).Equals("Hotd3Arcade_Launcher.exe",StringComparison.OrdinalIgnoreCase))return name.Equals("hod3pc.exe",StringComparison.OrdinalIgnoreCase);
        string[] parts=Path.GetRelativePath(root,full).Replace('\\','/').Split('/');
        return parts.Length>=3 && parts[^3].Equals("Binaries",StringComparison.OrdinalIgnoreCase)
            && (parts[^2].Equals("Win64",StringComparison.OrdinalIgnoreCase) && name.EndsWith("-Win64-Shipping.exe",StringComparison.OrdinalIgnoreCase)
                || parts[^2].Equals("Win32",StringComparison.OrdinalIgnoreCase) && name.EndsWith("-Win32-Shipping.exe",StringComparison.OrdinalIgnoreCase));
    }
}
