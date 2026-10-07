using System.Runtime.InteropServices;
using System.Text;
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
}
