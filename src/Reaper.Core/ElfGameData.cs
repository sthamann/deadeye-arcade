using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reaper.Core;

// Some Linux games resolve junctions before opening their assets. An ASCII alias
// still fails when the physical directory contains Unicode. Keep originals and
// prepare real game data, including siblings of the common pm/game executable.
public static class ElfGameData
{
    private const string ManifestName = ".deadeye-game-source.json";
    private sealed record Stamp(long Length, long Modified);
    private sealed record Manifest(string SourceRoot, Dictionary<string, Stamp> Files);
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static string Key(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? source.ToUpperInvariant() : source)))[..24];
    private static bool Inside(string path, string root) => path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, Comparison);
    private static Manifest? ReadManifest(string file)
    {
        if (!File.Exists(file) && !File.Exists(file + ".bak")) return null;
        foreach (string candidate in new[] { file, file + ".bak" })
            try
            {
                if (!File.Exists(candidate)) continue;
                var value = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(candidate));
                string folder = Path.GetDirectoryName(file)!;
                if (value is not null && Path.IsPathFullyQualified(value.SourceRoot) && value.Files is not null &&
                    Key(value.SourceRoot) == Path.GetFileName(folder) && value.Files.All(p => p.Value is not null && !Path.IsPathRooted(p.Key) && Inside(Path.GetFullPath(Path.Combine(folder, p.Key)), folder))) return value;
            }
            catch (Exception e) when (e is JsonException or IOException or ArgumentException) { }
        throw new IOException("Prepared game metadata is damaged or unavailable. The original game files are unchanged. Restore its metadata backup or prepare the original game again in a new folder.");
    }

    public static string Prepare(string path, string storageRoot)
    {
        path = Path.GetFullPath(path); storageRoot = Path.GetFullPath(storageRoot);
        if (Directory.Exists(storageRoot)) storageRoot = PhysicalDirectory(storageRoot);
        // Revisit the original source on subsequent launches so patches propagate
        // without overwriting saves or settings changed only in the prepared game.
        if (Inside(path, storageRoot))
        {
            string relative = Path.GetRelativePath(storageRoot, path);
            string first = relative.Split(Path.DirectorySeparatorChar)[0];
            string directory = Path.Combine(storageRoot, first);
            string manifestPath = Path.Combine(directory, ManifestName);
            if (File.Exists(manifestPath) || File.Exists(manifestPath + ".bak"))
            {
                var prior = ReadManifest(manifestPath);
                if (prior is not null && Key(prior.SourceRoot) == first)
                {
                    string original = Path.Combine(prior.SourceRoot, Path.GetRelativePath(directory, path));
                    if (File.Exists(original)) path = original;
                }
            }
        }
        if (!File.Exists(path)) return path;
        string physical = Path.Combine(PhysicalDirectory(Path.GetDirectoryName(path)!), Path.GetFileName(path));
        var file = new FileInfo(physical);
        if (file.LinkTarget is not null) physical = file.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Cannot resolve the game executable.");
        if (physical.All(c => c < 128)) return physical;
        string name = Path.GetFileName(physical);
        if (name.Any(c => c >= 128)) throw new IOException("ElfLoader requires an executable name without special characters.");
        string source = Path.GetDirectoryName(physical)!;
        if (Path.GetFileName(source).Equals("pm", StringComparison.OrdinalIgnoreCase)) source = Path.GetDirectoryName(source)!;
        if (Inside(storageRoot, source) || storageRoot.Equals(source, Comparison)) throw new IOException("Prepared game storage must be outside the original game folder.");
        if (storageRoot.Any(c => c >= 128)) throw new IOException("Prepared ElfLoader storage requires a path without special characters.");
        Directory.CreateDirectory(storageRoot);
        storageRoot = PhysicalDirectory(storageRoot);
        if (storageRoot.Any(c => c >= 128)) throw new IOException("Prepared ElfLoader storage must physically reside in a path without special characters.");
        string cache = Path.Combine(storageRoot, Key(source));
        if (Directory.Exists(cache) && new DirectoryInfo(cache).LinkTarget is not null) throw new IOException("Prepared game storage must be a physical directory.");
        var files = new List<FileInfo>(); var directories = new List<string>(); var pending = new Stack<DirectoryInfo>(); pending.Push(new(source));
        while (pending.TryPop(out var directory))
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked game data requires a physical game copy before ElfLoader can start.");
                if (entry is DirectoryInfo child) { directories.Add(Path.GetRelativePath(source, child.FullName)); pending.Push(child); }
                else if (entry is FileInfo item) files.Add(item);
            }
        string manifestFile = Path.Combine(cache, ManifestName);
        Manifest? previous = ReadManifest(manifestFile);
        if (previous is not null && !previous.SourceRoot.Equals(source, Comparison)) throw new IOException("Prepared game storage belongs to a different source.");
        var stamps = files.ToDictionary(f => Path.GetRelativePath(source, f.FullName), f => new Stamp(f.Length, f.LastWriteTimeUtc.Ticks));
        var changed = files.Where(f => !File.Exists(Path.Combine(cache, Path.GetRelativePath(source, f.FullName))) || previous is null ||
            !previous.Files.TryGetValue(Path.GetRelativePath(source, f.FullName), out var old) || old != stamps[Path.GetRelativePath(source, f.FullName)]).ToArray();
        long required = checked(changed.Sum(f => f.Length) + 64L * 1024 * 1024);
        if (changed.Length > 0 && new DriveInfo(Path.GetPathRoot(storageRoot)!).AvailableFreeSpace < required) throw new IOException("Not enough free space to prepare ElfLoader game data while preserving the originals.");
        void EnsureDirectory(string directory)
        {
            if (!PhysicalDirectory(directory).Equals(directory, Comparison)) throw new IOException("Prepared game data contains a linked directory.");
            Directory.CreateDirectory(directory);
        }
        EnsureDirectory(cache);
        foreach (string directory in directories) EnsureDirectory(Path.Combine(cache, directory));
        foreach (var item in changed)
        {
            string target = Path.Combine(cache, Path.GetRelativePath(source, item.FullName));
            if (new FileInfo(target).LinkTarget is not null) throw new IOException("Prepared game data contains a linked file.");
            EnsureDirectory(Path.GetDirectoryName(target)!);
            string copy = Path.Combine(cache, ".deadeye-copy-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.Copy(item.FullName, copy);
                File.SetLastWriteTimeUtc(copy, item.LastWriteTimeUtc);
                item.Refresh();
                if (new Stamp(item.Length, item.LastWriteTimeUtc.Ticks) != stamps[Path.GetRelativePath(source, item.FullName)]) throw new IOException("Game data changed while it was being prepared. Please finish updating the game and try again.");
                File.Move(copy, target, true);
            }
            finally { if (File.Exists(copy)) File.Delete(copy); }
        }
        // Retire only unchanged files previously owned by the source. Modified
        // saves and files created by the running game remain in place.
        foreach (var removed in previous?.Files.Where(p => !stamps.ContainsKey(p.Key)) ?? [])
        {
            string oldPath = Path.Combine(cache, removed.Key);
            if (!File.Exists(oldPath)) continue;
            var old = new FileInfo(oldPath);
            if (old.LinkTarget is not null || !PhysicalDirectory(old.DirectoryName!).Equals(old.DirectoryName, Comparison)) throw new IOException("Prepared game data contains a link.");
            if (new Stamp(old.Length, old.LastWriteTimeUtc.Ticks) != removed.Value) continue;
            string retired = Path.Combine(storageRoot, ".retired", Key(source), Guid.NewGuid().ToString("N"), removed.Key);
            EnsureDirectory(Path.GetDirectoryName(retired)!);
            File.Move(oldPath, retired);
        }
        string json = JsonSerializer.Serialize(new Manifest(source, stamps));
        foreach (string destination in new[] { manifestFile + ".bak", manifestFile })
        {
            string temporary = destination + ".new";
            if (new FileInfo(temporary).LinkTarget is not null) throw new IOException("Prepared game metadata contains a link.");
            File.WriteAllText(temporary, json);
            File.Move(temporary, destination, true);
        }
        return Path.Combine(cache, Path.GetRelativePath(source, physical));
    }

    private static string PhysicalDirectory(string path, int depth = 0)
    {
        if (depth > 32) throw new IOException("The game directory contains a link cycle.");
        string full = Path.GetFullPath(path), root = Path.GetPathRoot(full)!;
        string result = root;
        foreach (string part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            result = Path.Combine(result, part);
            var directory = new DirectoryInfo(result);
            if (directory.LinkTarget is not null) result = PhysicalDirectory(directory.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Cannot resolve the game directory."), depth + 1);
        }
        return result;
    }
}
