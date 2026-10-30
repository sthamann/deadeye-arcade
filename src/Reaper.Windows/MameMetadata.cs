using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Reaper.Core;

namespace Reaper.Windows;

internal static class MameMetadata
{
    public static async Task Prepare(GameEntry game,string data)
    {
        string? rom=MameControls.Rom(game);if(rom is null)return;
        async Task<string> Query(IEnumerable<string> arguments)
        {
            var info=new ProcessStartInfo(game.Executable){WorkingDirectory=game.WorkingDirectory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in arguments)info.ArgumentList.Add(arg);
            using var p=Process.Start(info)??throw new IOException("MAME metadata process did not start.");
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var output=p.StandardOutput.ReadToEndAsync(timeout.Token);var error=p.StandardError.ReadToEndAsync(timeout.Token);
            try {await p.WaitForExitAsync(timeout.Token);}
            catch(OperationCanceledException){p.Kill(true);await p.WaitForExitAsync();throw new IOException("MAME metadata query timed out.");}
            if(p.ExitCode!=0)throw new IOException("MAME metadata: "+await error);
            return await output;
        }
        string xml=await Query([rom,"-listxml"]);
        using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null});
        var machine=XDocument.Load(reader).Descendants().SingleOrDefault(e=>e.Name.LocalName is "machine" or "game" && (string?)e.Attribute("name")==rom)
            ??throw new IOException("The installed MAME did not describe the selected game.");
        string config=await Query(game.Arguments.Append("-showconfig"));
        string? directory=config.Split('\n').Select(l=>l.Trim()).Where(l=>l.StartsWith("cfg_directory ",StringComparison.Ordinal)).Select(l=>l["cfg_directory".Length..].Trim().Trim('"')).SingleOrDefault();
        if(string.IsNullOrWhiteSpace(directory)||directory.Contains(';'))throw new IOException("MAME cfg_directory could not be resolved unambiguously.");
        var binary=new FileInfo(game.Executable);
        var metadata=new MameInputMetadata(rom,machine.ToString(),Path.GetFullPath(directory,game.WorkingDirectory),binary.Length,binary.LastWriteTimeUtc.Ticks);
        string path=MameControls.MetadataPath(game,data);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path+".new",JsonSerializer.Serialize(metadata,JsonDefaults.Options));File.Move(path+".new",path,true);
    }
}
