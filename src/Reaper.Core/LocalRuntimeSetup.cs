using System.Xml.Linq;

namespace Reaper.Core;

public static class LocalRuntimeSetup
{
    // TeknoParrot ships its own OpenAL runtime. Resolve the matching architecture
    // locally for game executables instead of downloading arbitrary DLL files.
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

        var candidates = new[] { "TeknoParrot", "N2", Path.Combine("ElfLdr2", "libs") }
            .Select(subfolder => Path.Combine(game.WorkingDirectory, subfolder, "openal32.dll"));
        foreach (var executable in Directory.EnumerateFiles(folder, "*.exe").Take(32))
        {
            NativeImage image;
            try { image = NativeImports.Read(executable); }
            catch (Exception e) when (e is IOException or BadImageFormatException or ArgumentOutOfRangeException) { continue; }
            if (!image.Imports.Any(i => i.Name == "openal32.dll" && !i.Delayed)) continue;
            string local = Path.Combine(folder, "OpenAL32.dll");
            if (File.Exists(local)) continue;
            string system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                image.Architecture == "x86" && Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", "OpenAL32.dll");
            if (File.Exists(system)) continue;
            string? source = candidates.FirstOrDefault(file => Compatible(file, image.Architecture));
            if (source is null) continue;
            File.Copy(source, local, false);
            File.WriteAllText(local + ".deadeye-source.txt", source + Environment.NewLine);
        }
    }
    private static bool Compatible(string file, string architecture)
    {
        try { return File.Exists(file) && NativeImports.Read(file).Architecture == architecture; }
        catch (Exception e) when (e is IOException or BadImageFormatException or ArgumentOutOfRangeException) { return false; }
    }
}
