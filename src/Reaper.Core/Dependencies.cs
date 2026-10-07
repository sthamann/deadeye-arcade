using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;

namespace Reaper.Core;

public record NativeImport(string Name, bool Delayed);
public record NativeImage(string Architecture, NativeImport[] Imports);
public record RuntimePackage(string Id, string Name, string Url, string Documentation);
public record DependencyFinding(string GameId, string Game, string Binary, string Architecture, string Dll,
    string? PackageId, bool Missing, bool Required);
public record DependencyReport(DateTimeOffset Time, int Games, int CheckedBinaries, DependencyFinding[] Findings, string[] Unchecked)
{
    public string[] MissingPackages => Findings.Where(f => f.Missing && f.PackageId is not null).Select(f => f.PackageId!).Distinct().ToArray();
}

// Reads PE headers only; never loads or executes the inspected game/DLL.
public static class NativeImports
{
    public static NativeImage Read(string file)
    {
        using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var pe = new PEReader(stream);
        var header = pe.PEHeaders.PEHeader ?? throw new BadImageFormatException(I18n.T("Kein Windows-Programm."));
        string arch = pe.PEHeaders.CoffHeader.Machine switch { Machine.I386 => "x86", Machine.Amd64 => "x64", Machine.Arm64 => "arm64", _ => "unknown" };
        var imports = new List<NativeImport>();
        ReadTable(header.ImportTableDirectory, 20, 12, false);
        ReadTable(header.DelayImportTableDirectory, 32, 4, true);
        return new(arch, imports.Distinct().ToArray());
        void ReadTable(DirectoryEntry table, int stride, int nameOffset, bool delayed)
        {
            if (table.RelativeVirtualAddress == 0 || table.Size == 0) return;
            var reader = pe.GetSectionData(table.RelativeVirtualAddress).GetReader();
            int size = Math.Min(table.Size, reader.Length);
            for (int offset = 0; offset + stride <= size && offset / stride < 4096; offset += stride)
            {
                reader.Offset = offset; uint flags = reader.ReadUInt32();
                reader.Offset = offset + nameOffset; uint name = reader.ReadUInt32();
                if (name == 0) break;
                if (delayed && (flags & 1) == 0) name = checked((uint)(name - header.ImageBase));
                var text = pe.GetSectionData(checked((int)name)).GetReader();
                var bytes = new List<byte>();
                while (text.RemainingBytes > 0 && bytes.Count < 260) { byte b = text.ReadByte(); if (b == 0) break; bytes.Add(b); }
                string dll = System.Text.Encoding.ASCII.GetString(bytes.ToArray()).ToLowerInvariant();
                if (dll.Length == 0 || dll.IndexOfAny(['/', '\\', ':']) >= 0) throw new BadImageFormatException(I18n.T("Ungültiger DLL-Name."));
                imports.Add(new(dll, delayed));
            }
        }
    }
}

public static class RuntimeCatalog
{
    public const string VcDocs = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/";
    public static string? Family(string dll) => dll.ToLowerInvariant() switch
    {
        "msvcr100.dll" or "msvcp100.dll" or "mfc100.dll" or "mfc100u.dll" => "vc2010",
        "msvcr110.dll" or "msvcp110.dll" or "mfc110.dll" or "mfc110u.dll" => "vc2012",
        "msvcr120.dll" or "msvcp120.dll" or "mfc120.dll" or "mfc120u.dll" => "vc2013",
        "vcruntime140.dll" or "vcruntime140_1.dll" or "msvcp140.dll" or "msvcp140_1.dll" or "msvcp140_2.dll" or "mfc140.dll" or "mfc140u.dll" or "concrt140.dll" => "vc14",
        var name when Regex.IsMatch(name, @"^(d3dx(9|10|11)_\d+|d3dcompiler_43|xinput1_3|xaudio2_7|xactengine3_\d+)\.dll$") => "directx",
        _ => null
    };
    public static RuntimePackage? Get(string id)
    {
        if (id == "directx") return new(id, I18n.T("DirectX Zusatzbibliotheken"), "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe", "https://www.microsoft.com/en-us/download/details.aspx?id=35");
        if (Regex.IsMatch(id, @"^dotnet(8|9|10)-(x86|x64)$"))
        {
            var parts = id.Split('-'); string version = parts[0][6..];
            return new(id, $".NET Desktop {version} ({parts[1]})", $"https://aka.ms/dotnet/{version}.0/windowsdesktop-runtime-win-{parts[1]}.exe", $"https://dotnet.microsoft.com/en-us/download/dotnet/{version}.0");
        }
        string[] split = id.Split('-'); if (split.Length != 2 || split[1] is not ("x86" or "x64")) return null;
        string arch = split[1]; string? url = split[0] switch
        {
            "vc2010" => $"https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_{arch}.exe",
            "vc2012" => $"https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_{arch}.exe",
            "vc2013" => $"https://aka.ms/highdpimfc2013{arch}enu",
            "vc14" => $"https://aka.ms/vc14/vc_redist.{arch}.exe",
            _ => null
        };
        return url is null ? null : new(id, $"Visual C++ {split[0][2..]} ({arch})", url, VcDocs);
    }
    public static string? For(string dll, string architecture)
    {
        string? family = Family(dll);
        return family == "directx" ? family : family is not null && architecture is "x86" or "x64" ? family + "-" + architecture : null;
    }
}

public static class DependencyScanner
{
    public static DependencyReport Scan(IEnumerable<GameEntry> library, string windowsDirectory, Func<string, string, Version, bool>? managedInstalled = null)
    {
        var games = library.ToArray(); var findings = new List<DependencyFinding>(); var uncheckedFiles = new List<string>();
        var images = new Dictionary<string, NativeImage>(StringComparer.OrdinalIgnoreCase); var inspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games)
        {
            var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var roots = new[] { game.Executable }.Concat((game.Helpers ?? []).Select(h => h.Executable))
                .Concat((game.RequiredFiles ?? []).Where(f => Path.GetExtension(f).Equals(".exe", StringComparison.OrdinalIgnoreCase)))
                .Concat(ProfileExecutables(game));
            foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) Inspect(root, Path.GetDirectoryName(root) ?? "", 0, false);
            void Inspect(string file, string executableDirectory, int depth, bool inheritedOptional)
            {
                // A required path must be inspected again if an optional import reached it first.
                if (visited.TryGetValue(file, out bool optional) && (!optional || inheritedOptional)) return;
                visited[file] = inheritedOptional;
                if (!File.Exists(file)) { uncheckedFiles.Add(game.Title + ": " + file + I18n.T(" fehlt")); return; }
                if (depth > 3 || visited.Count > 128) { uncheckedFiles.Add(game.Title + I18n.T(": Prüftiefe erreicht")); return; }
                NativeImage image;
                try { if (!images.TryGetValue(file, out image!)) { image = NativeImports.Read(file); images[file] = image; } }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentOutOfRangeException or OverflowException)
                { uncheckedFiles.Add(game.Title + ": " + Path.GetFileName(file) + I18n.T(" nicht lesbar")); return; }
                inspected.Add(file);
                InspectManaged(file, image.Architecture);
                foreach (var import in image.Imports)
                {
                    string? package = RuntimeCatalog.For(import.Name, image.Architecture);
                    string local = Path.Combine(executableDirectory, import.Name);
                    string system = Path.Combine(windowsDirectory, image.Architecture == "x86" && Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", import.Name);
                    bool exists = Compatible(local, image.Architecture) || Compatible(system, image.Architecture);
                    if (package is not null) findings.Add(new(game.Id, game.Title, file, image.Architecture, import.Name, package, !exists, !inheritedOptional && !import.Delayed));
                    else if (!exists && !import.Name.StartsWith("api-ms-") && !import.Name.StartsWith("ext-ms-"))
                        uncheckedFiles.Add(game.Title + ": " + import.Name + I18n.T(" nicht aufgelöst (Profil oder lokale Installation prüfen)"));
                    if (Compatible(local, image.Architecture)) Inspect(local, executableDirectory, depth + 1, inheritedOptional || import.Delayed);
                    else if (File.Exists(local)) uncheckedFiles.Add(game.Title + ": " + import.Name + I18n.T(" hat ein falsches oder nicht lesbares Dateiformat"));
                }
            }
            void InspectManaged(string file, string architecture)
            {
                string config = Path.ChangeExtension(file, ".runtimeconfig.json");
                if (!File.Exists(config) || managedInstalled is null) return;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(config));
                    var runtime = doc.RootElement.GetProperty("runtimeOptions");
                    var frameworks = new List<JsonElement>();
                    if (runtime.TryGetProperty("framework", out var one)) frameworks.Add(one);
                    if (runtime.TryGetProperty("frameworks", out var many)) frameworks.AddRange(many.EnumerateArray());
                    foreach (var framework in frameworks)
                    {
                        string name = framework.GetProperty("name").GetString() ?? "";
                        if (!Version.TryParse(framework.GetProperty("version").GetString(), out var version)) continue;
                        string? id = name is "Microsoft.NETCore.App" or "Microsoft.WindowsDesktop.App" && version.Major is 8 or 9 or 10 && architecture is "x86" or "x64" ? $"dotnet{version.Major}-{architecture}" : null;
                        bool installed = managedInstalled(architecture, name, version);
                        if (id is null) { uncheckedFiles.Add(game.Title + ": " + name + " " + version + I18n.T(" separat prüfen")); continue; }
                        findings.Add(new(game.Id, game.Title, file, architecture, name + " " + version, id, !installed, true));
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
                { uncheckedFiles.Add(game.Title + I18n.T(": .NET-Konfiguration nicht lesbar")); }
            }
        }
        IEnumerable<string> ProfileExecutables(GameEntry game)
        {
            if (game.Source != "teknoparrot" || !File.Exists(game.SourcePath)) return [];
            try
            {
                var profile = XDocument.Load(game.SourcePath);
                bool two = profile.Descendants().Any(e => e.Name.LocalName == "HasTwoExecutables" && e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
                return profile.Descendants().Where(e => e.Name.LocalName == "GamePath" || (two && e.Name.LocalName == "GamePath2"))
                    .Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => Path.GetFullPath(e.Value, game.WorkingDirectory)).ToArray();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException)
            { uncheckedFiles.Add(game.Title + I18n.T(": TeknoParrot-Profil nicht lesbar")); return []; }
        }
        return new(DateTimeOffset.Now, games.Length, inspected.Count, findings.Distinct().ToArray(), uncheckedFiles.Distinct().ToArray());
        bool Compatible(string path, string architecture)
        {
            if (!File.Exists(path)) return false;
            try { if (!images.TryGetValue(path, out var image)) { image = NativeImports.Read(path); images[path] = image; } return image.Architecture == architecture; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentOutOfRangeException or OverflowException) { return false; }
        }
    }
}
