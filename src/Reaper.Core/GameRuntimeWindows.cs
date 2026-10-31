namespace Reaper.Core;

public static class GameRuntimeWindows
{
    // Dolphin Core::UpdateTitle and both Qt render modes use this runtime
    // status title. Its library/configuration windows do not carry it.
    // https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Core.cpp
    public static bool DolphinRenderer(string title)=>System.Text.RegularExpressions.Regex.IsMatch(title,@"^Dolphin[^|]* \| [^|]+ (?:DC|SC) \| [^|]+ \| (?:HLE|LLE)(?: \||$)");
    public static bool KnownLauncher(string path)=>new[]{"steam.exe","steamwebhelper.exe","EpicGamesLauncher.exe","TeknoParrotUi.exe","Hotd3Arcade_Launcher.exe"}.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase);
    public static bool UnrealShipping(string image)=>System.Text.RegularExpressions.Regex.IsMatch(image.Replace('\\','/'),@"(?:^|/)Binaries/(Win32|Win64)/[^/]+-\1-Shipping\.exe$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
            && (parts[^2].Equals("Win64",StringComparison.OrdinalIgnoreCase)||parts[^2].Equals("Win32",StringComparison.OrdinalIgnoreCase))
            && (name.EndsWith("-"+parts[^2]+"-Shipping.exe",StringComparison.OrdinalIgnoreCase)
                // Packaged development builds keep the bootstrap's exact name.
                // This still requires an owned child inside the configured game
                // tree, under Binaries/Win32 or Binaries/Win64, never an updater.
                || !KnownLauncher(launcher)&&name.Equals(Path.GetFileName(launcher),StringComparison.OrdinalIgnoreCase));
    }
}
