using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Text;
using Reaper.Core;

namespace Reaper.Windows;

internal record CodeIntegrityReport(bool Checked,CodeIntegrityEvent[] Blocks,string? Error=null);

internal static class CodeIntegrityDiagnostics
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,ExactSpelling=true)] private static extern uint QueryDosDeviceW(string name,StringBuilder target,int size);
    private static string NativePath(string path)
    {
        string full=Path.GetFullPath(path);
        if(full.Length<3 || full[1]!=':')throw new IOException("Code Integrity path correlation requires a mapped Windows drive.");
        var device=new StringBuilder(1024);
        if(QueryDosDeviceW(full[..2],device,device.Capacity)==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return device.ToString().Split('\0')[0]+full[2..];
    }
    public static CodeIntegrityReport Read(GameEntry game,DateTimeOffset since)
    {
        var blocks=new List<CodeIntegrityEvent>();
        try
        {
            string target=NativePath(KnownFixes.Target(game)??game.Executable);
            string directory=target[..(target.LastIndexOf('\\')+1)];
            string launcher=NativePath(game.Executable);
            long milliseconds=Math.Clamp((long)(DateTimeOffset.UtcNow-since).TotalMilliseconds+1000,1000,600000);
            var query=new EventLogQuery("Microsoft-Windows-CodeIntegrity/Operational",PathType.LogName,$"*[System[(EventID=3077) and TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]"){ReverseDirection=true};
            using var reader=new EventLogReader(query);
            var limit=Stopwatch.StartNew();bool complete=false,schemaWarning=false;
            for(int i=0;i<100 && limit.Elapsed<TimeSpan.FromSeconds(3);i++)
            {
                using var entry=reader.ReadEvent(TimeSpan.FromMilliseconds(250));if(entry is null){complete=true;break;}
                var block=CodeIntegrityEvent.Parse(entry.ToXml(),entry.TimeCreated is {} time?new DateTimeOffset(time):null);
                if(block is null){schemaWarning=true;continue;}
                string file=block.File;
                bool related=file.Equals(target,StringComparison.OrdinalIgnoreCase) || file.Equals(launcher,StringComparison.OrdinalIgnoreCase)
                    || directory.Length>3 && file.StartsWith(directory,StringComparison.OrdinalIgnoreCase);
                if(!related)continue;
                blocks.Add(block);
            }
            return new(complete && !schemaWarning,blocks.Distinct().ToArray(),schemaWarning?"Some Code Integrity event metadata could not be read.":complete?null:"Code Integrity event scan reached its limit.");
        }
        catch(Exception e) when(e is EventLogException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or IOException or System.Xml.XmlException)
        {return new(false,blocks.ToArray(),e.Message);}
    }
}
