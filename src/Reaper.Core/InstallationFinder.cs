namespace Reaper.Core;

public record Installation(string Kind, string Name, string Path);
public record BrowseEntry(string Name, string Path, bool Directory);
public record BrowsePage(string Path, string? Parent, List<BrowseEntry> Entries, string? Warning);

public static class InstallationFinder
{
    // Bounded search: no whole-disk crawl, no junctions, no game binaries are executed.
    private static readonly Dictionary<string, (string Kind, string Name)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TeknoParrotUi.exe"] = ("tekno", "TeknoParrot"), ["mame.exe"] = ("mame", "MAME"),
        ["mame64.exe"] = ("mame", "MAME"), ["Dolphin.exe"] = ("tool", "Dolphin"),
        ["duckstation-qt-x64-ReleaseLTCG.exe"] = ("tool", "DuckStation"), ["pcsx2-qt.exe"] = ("tool", "PCSX2"),
        ["flycast.exe"] = ("tool", "Flycast"), ["Supermodel.exe"] = ("tool", "Supermodel"),
        ["emulator_multicpu.exe"] = ("tool", "Model 2"), ["DemulShooter.exe"] = ("tool", "DemulShooter"),
        ["Hook of the Reaper.exe"] = ("tool", "Hook of the Reaper")
    };
    public static List<Installation> Find(IEnumerable<string> roots, int depth = 3)
    {
        var found = new Dictionary<string, Installation>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<(string Path, int Depth)>();
        foreach (var root in roots.Where(Directory.Exists)) pending.Enqueue((Path.GetFullPath(root), 0));
        while (pending.TryDequeue(out var current) && visited.Count < 4000)
        {
            if (!visited.Add(current.Path)) continue;
            try
            {
                if ((File.GetAttributes(current.Path) & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var file in Directory.EnumerateFiles(current.Path, "*.exe").Take(1000))
                    if (Names.TryGetValue(Path.GetFileName(file), out var info)) found[file] = new(info.Kind, info.Name, file);
                if (current.Depth < depth)
                    foreach (var child in Directory.EnumerateDirectories(current.Path).Take(200))
                    {
                        string name = Path.GetFileName(child);
                        if (name.StartsWith('.') || name.StartsWith('$') || name.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                        pending.Enqueue((child, current.Depth + 1));
                    }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return found.Values.OrderBy(x => x.Name).ThenBy(x => x.Path).ToList();
    }
    public static BrowsePage Browse(string directory, bool foldersOnly, string[] extensions)
    {
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Dieser Ordner ist nicht vorhanden.");
        List<BrowseEntry> entries = []; string? warning = null;
        try
        {
            foreach (var child in Directory.EnumerateDirectories(directory).OrderBy(Path.GetFileName).Take(300))
                entries.Add(new(Path.GetFileName(child), child, true));
            if (!foldersOnly)
                foreach (var file in Directory.EnumerateFiles(directory).Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).OrderBy(Path.GetFileName).Take(300))
                    entries.Add(new(Path.GetFileName(file), file, false));
            if (entries.Count >= 300) warning = "Große Ordner werden gekürzt angezeigt. Öffne einen Unterordner.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warning = "Dieser Ordner kann nicht vollständig gelesen werden."; }
        return new(directory, Directory.GetParent(directory)?.FullName, entries, warning);
    }
}
