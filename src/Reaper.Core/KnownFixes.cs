using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Reaper.Core;

public record FixRule(string Id, string Name, string Mode, string Scope, string Source, string Verification);
public record FixFinding(string GameId, string Game, string RuleId, string Status, string Message, string[] Files);
public record FixReport(DateTimeOffset Time, int CatalogVersion, int Games, FixFinding[] Findings);
public record FixPackage(string Id, string Url, string Sha256, int MaxBytes);

// Reviewed rules only: the catalog cannot contain scripts, executable commands or arbitrary downloads.
public static class KnownFixes
{
    public const int Version = 1;
    public static FixRule[] Catalog { get; } = LoadCatalog();
    public static readonly FixPackage OpenAl = new("openal-soft-1.25.2",
        "https://openal-soft.org/openal-binaries/openal-soft-1.25.2-bin.zip",
        "67a0c4b800bd860c93c04f38caf8cbe4875f9c84700ac430efc451f70e265434", 30_000_000);
    public static readonly FixPackage Hypseus = new("hypseus-2.11.1",
        "https://github.com/DirtBagXon/hypseus-singe/releases/download/v2.11.1/Hypseus.Singe-v2.11.1-win64.zip",
        "50d23be1a22868f39505a9711fd8b00b0c0e97a98b41a10a12230029494bf3bc", 15_000_000);
    public const string HypseusExecutableHash = "28bb5ae18dceec60df76fe1fc8a9ccab8afd2b0c1b3f311d855f9ab75131d287";
    private static readonly object historyLock = new();
    private static readonly string[] HypseusAssets = [
"pics/led10.bmp","pics/player1.bmp","pics/oncaptain.bmp","pics/led6.bmp","pics/overlayleds1.bmp","pics/led2.bmp","pics/cadet.bmp","pics/led11.bmp","pics/captain.bmp","pics/led14.bmp","pics/led8.bmp","pics/led7.bmp","pics/annunon.png","pics/player2.bmp","pics/led3.bmp","pics/led12.bmp","pics/overlayleds2.bmp","pics/lives.bmp","pics/ldp1450font.bmp","pics/offcadet.bmp","pics/led4.bmp","pics/led16.bmp","pics/tqkeys.png","pics/led13.bmp","pics/annunoff.png","pics/led0.bmp","pics/onspaceace.bmp","pics/led9.bmp","pics/offcaptain.bmp","pics/led15.bmp","pics/shoot.bmp","pics/credits.bmp","pics/led5.bmp","pics/spaceace.bmp","pics/oncadet.bmp","pics/ConsoleFont.bmp","pics/led17.bmp","pics/offspaceace.bmp","pics/led1.bmp","sound/grumble.wav"];
    private static FixRule[] LoadCatalog()
    {
        using var stream = typeof(KnownFixes).Assembly.GetManifestResourceStream("Reaper.Core.KnownFixes.json")!;
        return JsonSerializer.Deserialize<FixRule[]>(stream, JsonDefaults.Options)!;
    }
    public static string Hash(string path)
    { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    public static bool ValidPackage(string path, FixPackage package) => File.Exists(path)
        && new FileInfo(path).Length <= package.MaxBytes && Hash(path).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase);

    public static async Task<FixReport> Apply(IEnumerable<GameEntry> entries, string data, bool administrator,
        Func<FixPackage, Task<string>> preparePackage)
    {
        var games = entries.ToArray(); var findings = new List<FixFinding>();
        foreach (var game in games)
        {
            try
            {
                if (game.Source == "teknoparrot" && File.Exists(game.SourcePath))
                {
                    bool repaired = LaunchRules.RepairTeknoParrotPath(game);
                    if (repaired) Add(game, "tekno-path", "repaired", "Spielpfad aus der eindeutigen Bibliotheksdatei repariert.", [game.SourcePath]);
                }
                if (EmulatorSetup.ConfigurePaths(game))
                    Add(game, "portable-paths", "repaired", "Emulatorpfade oder Hersteller-Metadaten mit Sicherung repariert.", [game.Source == "teknoparrot" ? game.SourcePath : Path.GetFileName(game.Executable).StartsWith("pcsx2",StringComparison.OrdinalIgnoreCase) ? Path.Combine(game.WorkingDirectory,"inis","PCSX2.ini") : Path.Combine(game.WorkingDirectory,"EMULATOR.INI")]);
                foreach (var issue in LaunchRules.Issues(game))
                    Add(game, "missing-files", "needs-action", issue, []);
                if (game.Source == "teknoparrot" && File.Exists(game.SourcePath))
                {
                    var profile = XDocument.Load(game.SourcePath);
                    if (!administrator && profile.Root?.Element("RequiresAdmin")?.Value == "true")
                        Add(game, "administrator", "needs-action", "Dieses Herstellerprofil verlangt Administratorrechte. Windows muss den erhöhten Start bestätigen.", [game.SourcePath]);
                    if (profile.Root?.Element("Patreon")?.Value == "true")
                        Add(game, "vendor-access", "needs-action", "Dieses Herstellerprofil verlangt einen gültigen TeknoParrot-Zugang. Die Anmeldung muss im Emulator geprüft werden.", [game.SourcePath]);
                }
                if (Path.GetFileName(game.Executable).StartsWith("pcsx2", StringComparison.OrdinalIgnoreCase))
                {
                    string ini = Path.Combine(game.WorkingDirectory, "inis", "PCSX2.ini");
                    if (File.Exists(ini) && SharedPcsxPointer(File.ReadAllText(ini)))
                        Add(game, "pcsx2-shared-pointer", "needs-action", "Beide Guncon2-Ports teilen denselben Mauszeiger. Zwei unabhängige Spieler sind damit nicht eingerichtet.", [ini]);
                }
                await RepairOpenAl(game);
                await RepairHypseus(game);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Xml.XmlException or HttpRequestException or TaskCanceledException or InvalidDataException or ArgumentException)
            { Add(game, "repair-error", "needs-action", error.Message, []); }
        }
        var report = new FixReport(DateTimeOffset.Now, Version, games.Length, findings.ToArray());
        Directory.CreateDirectory(data);
        string destination = Path.Combine(data, games.Length == 1 ? "fixes-" + Regex.Replace(games[0].Id, "[^a-zA-Z0-9_-]", "_") + ".json" : "fixes-report.json");
        File.WriteAllText(destination + ".new", JsonSerializer.Serialize(report, JsonDefaults.Options));
        File.Move(destination + ".new", destination, true);
        foreach (var finding in findings.Where(f => f.Status == "repaired"))
            AppendHistory(data, finding, null);
        return report;

        void Add(GameEntry game, string rule, string status, string message, string[] files) => findings.Add(new(game.Id, game.Title, rule, status, I18n.T(message), files));
        async Task RepairOpenAl(GameEntry game)
        {
            string? executable = Target(game);
            if (executable is null || !File.Exists(executable)) return;
            string folder = Path.GetDirectoryName(executable)!;
            var binaries = new[] { executable }.Concat(Directory.EnumerateFiles(folder, "*.exe").Take(32)).Distinct(StringComparer.OrdinalIgnoreCase);
            var required = new List<(string Binary, NativeImage Image)>();
            var consumers = new List<string>();
            foreach (string binary in binaries)
            {
                NativeImage image;
                try { image = NativeImports.Read(binary); } catch (Exception e) when (e is IOException or BadImageFormatException or ArgumentOutOfRangeException) { continue; }
                if (image.Architecture is not ("x86" or "x64") || !image.Imports.Any(i => i.Name == "openal32.dll" && !i.Delayed)) continue;
                consumers.Add(image.Architecture);
                string local = Path.Combine(folder, "OpenAL32.dll");
                if (File.Exists(local))
                {
                    bool compatible;
                    try { compatible = NativeImports.Read(local).Architecture == image.Architecture; }
                    catch (Exception e) when (e is IOException or BadImageFormatException or ArgumentOutOfRangeException) { compatible = false; }
                    if (!compatible) Add(game, "openal", "needs-action", "Die vorhandene OpenAL-Datei passt nicht eindeutig zum Spiel. Sie wurde zur Sicherheit beibehalten.", [local, binary]);
                    continue;
                }
                string system = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), image.Architecture == "x86" && Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", "OpenAL32.dll");
                if (File.Exists(local) || File.Exists(system)) continue;
                required.Add((binary, image));
            }
            if (required.Count == 0) return;
            if (consumers.Distinct().Count() != 1)
            { Add(game, "openal", "needs-action", "Dieser Ordner enthält Spiele verschiedener Architekturen. Eine gemeinsame OpenAL-Datei wäre nicht eindeutig kompatibel.", required.Select(r => r.Binary).ToArray()); return; }
            {
                var (binary, image) = required[0];
                string local = Path.Combine(folder, "OpenAL32.dll");
                string archive = await preparePackage(OpenAl);
                if (!ValidPackage(archive, OpenAl)) throw new InvalidDataException(I18n.T("Das Reparaturpaket stimmt nicht mit der geprüften Herstellerdatei überein."));
                using var zip = ZipFile.OpenRead(archive);
                string prefix = "openal-soft-1.25.2-bin/";
                var entry = zip.GetEntry(prefix + "bin/" + (image.Architecture == "x86" ? "Win32" : "Win64") + "/soft_oal.dll") ?? throw new InvalidDataException("OpenAL component missing.");
                string temporary = local + ".deadeye-new";
                try
                {
                    entry.ExtractToFile(temporary, true);
                    if (NativeImports.Read(temporary).Architecture != image.Architecture) throw new InvalidDataException("OpenAL architecture mismatch.");
                    // License first; no game binaries or existing DLLs are overwritten.
                    CopyMissing(zip, prefix + "COPYING", Path.Combine(folder, "DeadeyeThirdParty", "OpenAL-Soft-COPYING.txt"));
                    File.Move(temporary, local, false);
                    File.WriteAllText(local + ".deadeye-source.json", JsonSerializer.Serialize(new { package = OpenAl, binary, image.Architecture }, JsonDefaults.Options));
                    Add(game, "openal", "repaired", "Fehlende OpenAL-Laufzeit in passender Architektur ergänzt und erneut gelesen.", [local]);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        async Task RepairHypseus(GameEntry game)
        {
            if (!Path.GetFileName(game.Executable).Equals("hypseus.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(game.Executable)) return;
            // A different release or modified emulator must keep its own matching assets.
            if (Hash(game.Executable) != HypseusExecutableHash)
            { Add(game, "hypseus-assets", "needs-action", "Hypseus-Version weicht vom geprüften Paket ab. Passende Zusatzdateien müssen zuerst bestätigt werden.", [game.Executable]); return; }
            if (HypseusAssets.All(relative => File.Exists(Path.Combine(game.WorkingDirectory, relative)))) return;
            string archive = await preparePackage(Hypseus);
            if (!ValidPackage(archive, Hypseus)) throw new InvalidDataException(I18n.T("Das Reparaturpaket stimmt nicht mit der geprüften Herstellerdatei überein."));
            using var zip = ZipFile.OpenRead(archive);
            const string prefix = "Hypseus Singe/";
            var assets = HypseusAssets.Select(relative => zip.GetEntry(prefix + relative) ?? throw new InvalidDataException("Hypseus asset manifest mismatch.")).ToArray();
            var copied = new List<string>();
            foreach (var entry in assets)
            {
                string target = Path.GetFullPath(Path.Combine(game.WorkingDirectory, entry.FullName[prefix.Length..]));
                if (!target.StartsWith(Path.GetFullPath(game.WorkingDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid asset path.");
                if (File.Exists(target)) continue;
                CopyMissing(zip, prefix + "LICENSE", Path.Combine(game.WorkingDirectory, "DeadeyeThirdParty", "Hypseus-LICENSE.txt"));
                CopyMissing(zip, entry.FullName, target); copied.Add(target);
            }
            if (copied.Count > 0) Add(game, "hypseus-assets", "repaired", "Fehlende Hypseus-Bilder und Tondateien aus der exakt passenden Version ergänzt.", copied.ToArray());
        }
    }
    public static Dictionary<string, string?> Snapshot(IEnumerable<string> files) => files.Distinct(StringComparer.OrdinalIgnoreCase)
        .ToDictionary(file => file, file => File.Exists(file) ? Hash(file) : null, StringComparer.OrdinalIgnoreCase);
    public static void RecordChanges(GameEntry game, string data, string rule, Dictionary<string, string?> before, IEnumerable<string> files)
    {
        var after = Snapshot(files);
        var changes = after.Where(pair => before.GetValueOrDefault(pair.Key) != pair.Value && pair.Value is not null)
            .Select(pair => new { file = pair.Key, before = before.GetValueOrDefault(pair.Key), after = pair.Value }).ToArray();
        if (changes.Length == 0) return;
        AppendHistory(data, new(game.Id, game.Title, rule, "configured", I18n.T("Aktives Spielprofil automatisch angepasst; geänderte Dateien protokolliert."), changes.Select(c => c.file).ToArray()), changes);
    }
    private static void AppendHistory(string data, FixFinding finding, object? changes)
    {
        Directory.CreateDirectory(data);
        lock (historyLock) File.AppendAllText(Path.Combine(data, "fixes-history.jsonl"), JsonSerializer.Serialize(new { Time = DateTimeOffset.Now, CatalogVersion = Version, finding, changes },
            new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = false }) + Environment.NewLine);
    }
    public static FixFinding[] History(string data)
    {
        string path = Path.Combine(data, "fixes-history.jsonl"); if (!File.Exists(path)) return [];
        lock (historyLock) return File.ReadLines(path).TakeLast(50).Select(line => {
            try { using var entry = JsonDocument.Parse(line); return entry.RootElement.GetProperty("finding").Deserialize<FixFinding>(JsonDefaults.Options); }
            catch (Exception e) when (e is JsonException or KeyNotFoundException) { return null; }
        }).OfType<FixFinding>().Reverse().ToArray();
    }
    public static string[] InputFiles(GameEntry game, string data)
    {
        var files = new List<string>();
        if (RetroArchSetup.IsRetroArch(game)) { files.Add(RetroArchSetup.ProfilePath(game, data)); files.Add(Path.ChangeExtension(files[^1], ".opt")); }
        if (SupermodelSetup.IsSupermodel(game)) files.Add(Path.Combine(game.WorkingDirectory,"Config","Supermodel.ini"));
        if (FlycastSetup.IsFlycast(game))
        {
            files.Add(Path.Combine(game.WorkingDirectory, "emu.cfg"));
            string mappings = Path.Combine(game.WorkingDirectory, "mappings");
            if (Directory.Exists(mappings)) files.AddRange(Directory.EnumerateFiles(mappings, "RAW_*.cfg"));
        }
        if (Rpcs3Setup.IsRpcs3(game))
        {
            string root = Rpcs3Setup.Root(game); files.Add(Path.Combine(root, "config.yml"));
            files.Add(Path.Combine(Rpcs3Setup.InputRoot(game), "raw_mouse.yml")); files.Add(Path.Combine(Rpcs3Setup.InputRoot(game), "gem_mouse.yml"));
            if (Rpcs3Setup.TitleId(game) is string id) files.Add(Path.Combine(root, "config", "custom_configs", "config_" + id + ".yml"));
        }
        if (DolphinSetup.IsDolphin(game))
        {
            string user = DolphinSetup.UserDirectory(game);
            foreach (string name in new[] { "Dolphin.ini", "GFX.ini", "DSUClient.ini" }) files.Add(Path.Combine(user, "Config", name));
            if (DolphinSetup.DiscId(game) is string id)
            {
                files.Add(Path.Combine(user, "GameSettings", id + ".ini"));
                foreach (string suffix in new[] { "", "_P2" }) files.Add(Path.Combine(user, "Config", "Profiles", "Wiimote", "Deadeye_RS3_" + id + suffix + ".ini"));
            }
        }
        return files.ToArray();
    }
    public static string[] MultiplayerFiles(GameEntry game) => (game.Helpers ?? []).Where(h => MultiplayerSetup.IsDemulShooter(h.Executable))
        .Select(h => Path.Combine(h.WorkingDirectory, "config.ini"))
        .Concat(File.Exists(Path.Combine(game.WorkingDirectory, "BlueEstate_Fix.dll")) ? [Path.Combine(game.WorkingDirectory, "LightGun_Patch.ini")] : [])
        .Concat(MultiplayerSetup.SupportsHouseDead2Remake(game) ? [MultiplayerSetup.HouseDead2RemakeConfigPath()] : [])
        .Concat(File.Exists(Path.Combine(game.WorkingDirectory, "EMULATOR.INI")) ? [Path.Combine(game.WorkingDirectory, "EMULATOR.INI")] : []).ToArray();
    public static string? Target(GameEntry game)
    {
        if (game.Source != "teknoparrot") return game.Executable;
        if (!File.Exists(game.SourcePath)) return null;
        string? path = XDocument.Load(game.SourcePath).Descendants().FirstOrDefault(e => e.Name.LocalName == "GamePath")?.Value;
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path, game.WorkingDirectory);
    }
    public static bool SharedPcsxPointer(string text)
    {
        var ports = new[] { "USB1", "USB2" }.Select(section => Regex.Match(text, @"(?ims)^\[" + section + @"\][^\r\n]*\r?\n(.*?)(?=^\[|\z)").Groups[1].Value).ToArray();
        if (ports.Any(p => !Regex.IsMatch(p, @"(?im)^Type\s*=\s*guncon2\s*$"))) return false;
        var pointers = ports.Select(p => Regex.Match(p, @"(?im)^guncon2_Pointer\s*=\s*([^\r\n]+)").Groups[1].Value.Trim()).ToArray();
        return pointers[0].Length > 0 && pointers[0].Equals(pointers[1], StringComparison.OrdinalIgnoreCase);
    }
    public static void CopyMissing(ZipArchive zip, string entryName, string destination)
    {
        if (File.Exists(destination)) return;
        var entry = zip.GetEntry(entryName) ?? throw new InvalidDataException("Reviewed asset missing: " + entryName);
        if (entry.Length > 15_000_000 || (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000) throw new InvalidDataException("Invalid repair asset.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + ".deadeye-new";
        try { entry.ExtractToFile(temporary, true); File.Move(temporary, destination, false); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
