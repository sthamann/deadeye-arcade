using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Reaper.Core;
namespace Reaper.Windows;

public static class DolphinAccuracyPackage
{
    private const string Revision="e5a6dce4b6e12b0659381d56dfabe2d478dd25b1";
    private const string Hash="0a1fadaab5d32eefbbfe99b84677b942ad1a70518d935510a7473b3afc9548ea";
    public static string Folder(string data)=>Path.Combine(data,"gun-software","dolphin-accuracy",Revision);
    public static async Task<string> Prepare(string data)
    {
        string folder=Folder(data);if(File.Exists(Path.Combine(folder,".complete")))return folder;
        using var http=new HttpClient {Timeout=TimeSpan.FromMinutes(2)};
        var bytes=await http.GetByteArrayAsync($"https://codeload.github.com/ProfgLX/Dolphin-Lightguns-Accuracy-Inis/zip/{Revision}");
        if(bytes.Length>4_000_000 || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(Hash,StringComparison.OrdinalIgnoreCase))throw new IOException(I18n.T("Dolphin-Profilpaket konnte nicht bestätigt werden."));
        using var stream=new MemoryStream(bytes);using var zip=new ZipArchive(stream);
        string root=$"Dolphin-Lightguns-Accuracy-Inis-{Revision}/",prefix=root+"Accuracy and control mappings/";
        Directory.CreateDirectory(folder);
        foreach(var entry in zip.Entries)
        {
            string? relative=entry.FullName==root+"LICENSE"?"LICENSE":entry.FullName.StartsWith(prefix)?entry.FullName[prefix.Length..]:null;
            if(string.IsNullOrEmpty(relative) || entry.FullName.EndsWith('/'))continue;
            string target=Path.GetFullPath(Path.Combine(folder,relative));
            if(!target.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || entry.Length>100000 || (entry.ExternalAttributes>>16&0xf000)==0xa000)throw new IOException(I18n.T("Ungültiger Eintrag im Dolphin-Profilpaket."));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target,true);
        }
        if(!File.Exists(Path.Combine(folder,"LICENSE")))throw new IOException(I18n.T("Lizenz des Dolphin-Profilpakets fehlt."));
        File.WriteAllText(Path.Combine(folder,".complete"),Revision);return folder;
    }
}
