using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reaper.Core;

public static class GameTestFingerprint
{
    public static string Create(GameEntry game,IEnumerable<GunBinding> bindings,string data,string version)
    {
        game=FlycastSetup.ResolveContent(game);
        var text=new StringBuilder(version).Append(JsonSerializer.Serialize(game,JsonDefaults.Options)).Append(JsonSerializer.Serialize(bindings,JsonDefaults.Options));
        var files=KnownFixes.InputFiles(game,data).Concat(KnownFixes.MultiplayerFiles(game))
            .Concat(new[]{game.Executable,game.SourcePath,KnownFixes.Target(game)??""}).Concat(game.RequiredFiles??[])
            .Concat((game.Helpers??[]).SelectMany(h=>new[]{h.Executable}.Concat(h.Arguments.Where(a=>!a.StartsWith('-')).Select(a=>Path.GetFullPath(a,h.WorkingDirectory)))))
            .Concat(game.Arguments.Where(a=>!a.StartsWith('-')).SelectMany(a=>a.Split('|')).Select(a=>Path.GetFullPath(a,game.WorkingDirectory)));
        if(game.Source=="mame")files=files.Append(MameControls.MetadataPath(game,data));
        foreach(string path in files.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            text.Append('\0').Append(path).Append('\0');
            if(File.Exists(path))
            {
                var file=new FileInfo(path);text.Append(file.Length).Append(file.LastWriteTimeUtc.Ticks);
                if(file.Length<2_000_000 || file.Extension.Equals(".exe",StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".dll",StringComparison.OrdinalIgnoreCase))
                {using var stream=File.OpenRead(path);text.Append(Convert.ToHexString(SHA256.HashData(stream)));}
            }
            else text.Append(Directory.Exists(path)?"directory":"missing");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
