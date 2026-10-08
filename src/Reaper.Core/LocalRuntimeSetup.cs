using System.Xml.Linq;

namespace Reaper.Core;

public static class LocalRuntimeSetup
{
    // Install only matching, already owned ELF libraries required by these vendor profiles.
    // OpenAL is handled by the reviewed KnownFixes package and architecture checks.
    public static void Configure(GameEntry game)
    {
        if (game.Source != "teknoparrot" || !File.Exists(game.SourcePath)) return;
        var profile = XDocument.Load(game.SourcePath);
        var target = profile.Descendants().FirstOrDefault(e => e.Name.LocalName == "GamePath")?.Value;
        if (string.IsNullOrWhiteSpace(target)) return;
        string folder = Path.GetDirectoryName(Path.GetFullPath(target, game.WorkingDirectory))!;
        if (!Directory.Exists(folder)) return;
        if(profile.Root?.Element("EmulatorType")?.Value=="ElfLdr2" && profile.Root.Element("EmulationProfile")?.Value is "PrimevalHunt" or "Hotd4ex")
        {
            // These two vendor profiles require their own Lindbergh shared libraries
            // in the loader's search directory. Retain already installed libraries.
            string destination=Path.Combine(Path.GetDirectoryName(game.Executable)!,"ElfLdr2","libs");
            foreach(string name in new[]{"librnalindbergh_jr.so","libcri_soundoutput_lindbergh_jr.so"})
            {
                string source=Path.Combine(folder,name),targetLibrary=Path.Combine(destination,name);
                if(!File.Exists(source)||File.Exists(targetLibrary))continue;
                using var library=File.OpenRead(source);byte[] header=new byte[4];
                if(library.Read(header)!=4||!header.AsSpan().SequenceEqual(new byte[]{0x7f,0x45,0x4c,0x46}))continue;
                Directory.CreateDirectory(destination);File.Copy(source,targetLibrary,false);
                File.WriteAllText(targetLibrary+".deadeye-source.txt",source+Environment.NewLine);
            }
        }

    }
}
