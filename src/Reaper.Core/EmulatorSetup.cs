using System.Text.RegularExpressions;
namespace Reaper.Core;
public static class EmulatorSetup
{
    public static bool ConfigurePaths(GameEntry game)
    {
        if(game.Source=="teknoparrot") {
            bool changed=ConfigureTeknoMetadata(game);
            return ConfigureTeknoAsciiPath(game,LegacyPaths.ForElfLoader) || changed;
        }
        if(Path.GetFileName(game.Executable).StartsWith("pcsx2",StringComparison.OrdinalIgnoreCase)) return ConfigurePcsxStorage(game);
        // Model 2 accepts a set name, not a ROM filename. Add the explicit library folder to its ROM search path.
        if(!Path.GetFileName(game.Executable).StartsWith("emulator_",StringComparison.OrdinalIgnoreCase)||!File.Exists(game.SourcePath)||!game.SourcePath.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))return false;
        var config=Path.Combine(game.WorkingDirectory,"EMULATOR.INI");if(!File.Exists(config))return false;
        string text=File.ReadAllText(config),folder=LegacyPaths.ForAnsiEmulator(Path.GetDirectoryName(Path.GetFullPath(game.SourcePath))!);
        var section=Regex.Match(text,@"(?ims)^\[RomDirs\][^\r\n]*(?:\r?\n)(.*?)(?=^\[|\z)");if(!section.Success)return false;
        string contents=section.Groups[1].Value;
        var entries=Regex.Matches(contents,@"(?im)^Dir(\d+)[ \t]*=[ \t]*([^;\r\n]*)");
        foreach(Match entry in entries){var value=entry.Groups[2].Value.Trim();if(value.Length>0&&Path.GetFullPath(value,game.WorkingDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(folder,StringComparison.OrdinalIgnoreCase))return false;}
        var used=entries.Cast<Match>().Where(x=>!string.IsNullOrWhiteSpace(x.Groups[2].Value)).Select(x=>int.Parse(x.Groups[1].Value)).ToHashSet();
        int slot=Enumerable.Range(1,10).FirstOrDefault(i=>!used.Contains(i));if(slot==0)return false;
        var empty=entries.Cast<Match>().FirstOrDefault(x=>int.Parse(x.Groups[1].Value)==slot);
        string newline=text.Contains("\r\n")?"\r\n":"\n";
        if(empty is null)contents=$"Dir{slot}={folder}{newline}"+contents;
        else contents=contents[..empty.Index]+$"Dir{slot}={folder}"+contents[(empty.Index+empty.Length)..];
        string backup=config+".before-reaper-path-fix";if(!File.Exists(backup))File.Copy(config,backup);
        File.WriteAllText(config,text[..section.Groups[1].Index]+contents+text[(section.Groups[1].Index+section.Groups[1].Length)..]);return true;
    }
    private static bool ConfigureTeknoMetadata(GameEntry game)
    {
        if(!File.Exists(game.SourcePath))return false;
        string vendorPath=Path.Combine(Path.GetDirectoryName(game.Executable)!,"GameProfiles",Path.GetFileName(game.SourcePath));
        if(!File.Exists(vendorPath) || Path.GetFullPath(vendorPath)==Path.GetFullPath(game.SourcePath))return false;
        var user=System.Xml.Linq.XDocument.Load(game.SourcePath);
        var vendor=System.Xml.Linq.XDocument.Load(vendorPath);
        if(user.Root is null || vendor.Root is null || string.IsNullOrWhiteSpace(user.Root.Element("EmulationProfile")?.Value)
            || user.Root.Element("EmulationProfile")?.Value!=vendor.Root.Element("EmulationProfile")?.Value)return false;
        // Command-line launches can bypass TeknoParrot's profile editor migration.
        // Use installed vendor eligibility/loader metadata; preserve user inputs.
        bool changed=false;
        foreach(string name in new[]{"Patreon","EmulatorType","Is64Bit","RequiresAdmin","HasTwoExecutables","LaunchSecondExecutableFirst","ExecutableName2"})
        {
            var original=vendor.Root.Element(name);if(original is null)continue;
            var current=user.Root.Element(name);if(current?.Value==original.Value)continue;
            if(current is null)user.Root.Add(new System.Xml.Linq.XElement(name,original.Value));else current.Value=original.Value;
            changed=true;
        }
        // A loader revision can introduce a required companion process while
        // an old user profile still disables it and points at a removed drive.
        // Use only the vendor's exact filename beside an existing main binary.
        // A working custom path always wins; no directory search is performed.
        if(vendor.Root.Element("HasTwoExecutables")?.Value.Equals("true",StringComparison.OrdinalIgnoreCase)==true)
        {
            string? name=vendor.Root.Element("ExecutableName2")?.Value;
            var second=user.Root.Element("GamePath2");
            string? primary=user.Root.Element("GamePath")?.Value;
            if(!string.IsNullOrWhiteSpace(name)&&name is not "." and not ".."&&name.IndexOfAny(['/', '\\', ':'])<0&&!string.IsNullOrWhiteSpace(primary))
                try {
                    string main=Path.GetFullPath(primary,game.WorkingDirectory);
                    bool working=second is not null&&!string.IsNullOrWhiteSpace(second.Value)&&File.Exists(Path.GetFullPath(second.Value,game.WorkingDirectory));
                    string companion=Path.Combine(Path.GetDirectoryName(main)!,name);
                    if(!working&&File.Exists(main)&&File.Exists(companion))
                    {
                        if(second is null)user.Root.Add(new System.Xml.Linq.XElement("GamePath2",companion));else second.Value=companion;
                        changed=true;
                    }
                } catch(ArgumentException) { /* Leave invalid/unavailable paths for the explicit launch preflight. */ }
        }
        if(!changed)return false;
        string backup=game.SourcePath+".before-deadeye-vendor-fix";if(!File.Exists(backup))File.Copy(game.SourcePath,backup);
        string temporary=game.SourcePath+".deadeye-new";user.Save(temporary);File.Move(temporary,game.SourcePath,true);
        return true;
    }
    public static bool ConfigureTeknoAsciiPath(GameEntry game,Func<string,string> resolve)
    {
        if(game.Source!="teknoparrot" || !File.Exists(game.SourcePath))return false;
        var profile=System.Xml.Linq.XDocument.Load(game.SourcePath);
        if(profile.Root?.Element("EmulatorType")?.Value!="ElfLdr2")return false;
        bool two=profile.Root.Element("HasTwoExecutables")?.Value.Equals("true",StringComparison.OrdinalIgnoreCase)==true;
        var changes=new List<(System.Xml.Linq.XElement Node,string Path)>();
        foreach(string name in two?new[]{"GamePath","GamePath2"}:new[]{"GamePath"})
        {
            var path=profile.Root.Element(name);
            if(path is null || string.IsNullOrWhiteSpace(path.Value))continue;
            string original=Path.GetFullPath(path.Value,game.WorkingDirectory);
            string safe=Path.GetFullPath(resolve(original),game.WorkingDirectory);
            if(safe.Any(c=>c>=128))throw new IOException("ElfLoader 2 requires an ASCII game path.");
            if(safe.Equals(original,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))continue;
            if(!File.Exists(safe))throw new FileNotFoundException("The ASCII game path does not exist.",safe);
            changes.Add((path,safe));
        }
        if(changes.Count==0)return false;
        string backup=game.SourcePath+".before-deadeye-physical-path-fix";if(!File.Exists(backup))File.Copy(game.SourcePath,backup);
        foreach(var change in changes)change.Node.Value=change.Path;
        string temporary=game.SourcePath+".deadeye-new";profile.Save(temporary);File.Move(temporary,game.SourcePath,true);
        return true;
    }
    private static bool ConfigurePcsxStorage(GameEntry game)
    {
        // Repair only an unavailable absolute storage location in an existing portable installation.
        // A live external memory-card directory must never be replaced with empty local cards.
        var config=Path.Combine(game.WorkingDirectory,"inis","PCSX2.ini");
        if(!File.Exists(config))return false;
        string text=File.ReadAllText(config);
        var section=Regex.Match(text,@"(?ims)^\[Folders\][^\r\n]*(?:\r?\n)(.*?)(?=^\[|\z)");
        if(!section.Success)return false;
        var card=Regex.Match(section.Groups[1].Value,@"(?im)^MemoryCards[ \t]*=[ \t]*([^\r\n]*)");
        if(!card.Success)return false;
        string old=card.Groups[1].Value.Trim();
        if(!Path.IsPathRooted(old)||Directory.Exists(old))return false;
        var local=Path.Combine(game.WorkingDirectory,"memcards");Directory.CreateDirectory(local);
        string backup=config+".before-reaper-storage-fix";if(!File.Exists(backup))File.Copy(config,backup);
        int index=section.Groups[1].Index+card.Groups[1].Index;
        File.WriteAllText(config,text[..index]+local+text[(index+card.Groups[1].Length)..]);
        return true;
    }

}
