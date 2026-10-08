using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
namespace Reaper.Core;
public static class LegacyPaths
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern uint GetShortPathName(string path,StringBuilder output,uint length);
    public static string ForAnsiEmulator(string path)
    {
        if(!OperatingSystem.IsWindows()||path.All(c=>c<128))return path;
        var buffer=new StringBuilder(32768);uint length=GetShortPathName(path,buffer,(uint)buffer.Capacity);
        if(length>0&&length<buffer.Capacity&&buffer.ToString().All(c=>c<128))return buffer.ToString();
        throw new IOException(I18n.T("Dieser ältere Emulator benötigt einen Spielpfad ohne Sonderzeichen. Bitte einen ASCII-Ordner verwenden."));
    }
    public static string ForElfLoader(string path)
    {
        if(!OperatingSystem.IsWindows() || path.All(c=>c<128))return path;
        // TeknoParrot expands 8.3 names back to their Unicode long path. A directory
        // junction retains an ASCII name without copying or moving the game data.
        string source=Path.GetDirectoryName(Path.GetFullPath(path))!;
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),"DeadeyeArcade","paths");
        string name=Path.GetFileName(path);
        if(root.Any(c=>c>=128) || name.Any(c=>c>=128))throw new IOException("ElfLoader requires an ASCII alias location and executable name.");
        Directory.CreateDirectory(root);
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToUpperInvariant())))[..24];
        string alias=Path.Combine(root,hash);
        if(!Directory.Exists(alias)) {
            string Quote(string value)=>"'"+value.Replace("'","''")+"'";
            string script="$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path "+Quote(alias)+" -Target "+Quote(source)+" | Out-Null";
            var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe")) {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("-NoProfile");info.ArgumentList.Add("-NonInteractive");info.ArgumentList.Add("-EncodedCommand");info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
            using var process=Process.Start(info)??throw new IOException("Cannot create the game path alias.");
            string error=process.StandardError.ReadToEnd();process.WaitForExit();
            if(process.ExitCode!=0)throw new IOException("Cannot create the game path alias: "+error);
        }
        string? target=new DirectoryInfo(alias).ResolveLinkTarget(true)?.FullName;
        if(target is null || !Path.GetFullPath(target).TrimEnd('\\').Equals(source.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))
            throw new IOException("The existing game path alias points to a different directory.");
        return Path.Combine(alias,name);
    }
}
