using System.Text.Json;

namespace Reaper.Core;

public static class CollectionImporter
{
    public static ImportResult Read(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("games", out var games) || games.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Die Übergabe enthält keine Spieleliste.");
        var result = new List<GameEntry>(); var warnings = new List<string>();
        foreach (var row in games.EnumerateArray())
        {
            if (!row.TryGetProperty("launch_options", out var options))
                throw new InvalidDataException("Bitte spiele.json aus dem Übergabepaket auswählen, nicht den Bibliotheksentwurf.");
            string Str(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            string[] Strings(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
            string id = row.GetProperty("id").ToString();
            var candidates = new List<GameEntry>();
            foreach (var option in options.EnumerateArray())
            {
                string exe = Str(option, "target"), cwd = Str(option, "cwd");
                var args = Strings(option, "arguments_array"); var required = Strings(option, "requires");
                string name = exe.Replace('\\', '/').Split('/').Last();
                string source = name.Equals("TeknoParrotUi.exe", StringComparison.OrdinalIgnoreCase) ? "teknoparrot" :
                    Path.GetFileNameWithoutExtension(name).StartsWith("mame", StringComparison.OrdinalIgnoreCase) ? "mame" : "custom";
                // The handoff sometimes lists a CHD/data folder first and omits its sibling ROM archive.
                // Preserve that folder dependency and add only its matching archive, not other clones.
                if (source == "mame")
                {
                    var destinations = Strings(row, "c_paths");
                    required = required.Concat(required.Select(p => p + ".zip")
                        .Where(p => destinations.Contains(p, StringComparer.OrdinalIgnoreCase))).Distinct().ToArray();
                }
                string sourcePath = source == "teknoparrot" ? required.FirstOrDefault(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) ?? "" : required.FirstOrDefault() ?? exe;
                if (source == "mame") sourcePath = required.FirstOrDefault(p => p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) ?? sourcePath;
                string? Media(string role)
                {
                    return row.TryGetProperty("media", out var media) && media.TryGetProperty(role, out var m)
                        && m.TryGetProperty("preferred", out var preferred) && preferred.ValueKind == JsonValueKind.Object ? Str(preferred, "target") : null;
                }
                int priority = row.TryGetProperty("priority", out var pri) && pri.TryGetInt32(out int n) ? n : 0;
                var game = new GameEntry("inventory-" + id, Str(row, "title"), Str(row, "system"), exe, args, cwd,
                    source, sourcePath, Cover: Media("cover"), Favorite: priority == 1,
                    PreviewVideo: Media("video"), Screenshot: Media("screenshot"), Logo: Media("logo"),
                    RequiredFiles: required, Priority: priority, Players: Str(row, "two_player"),
                    SetupNotes: Str(row, "dual_gun") + "\n" + Str(option, "note") + "\n" + Str(row, "notes"));
                candidates.Add(LaunchRules.Validate(game));
            }
            if (candidates.Count == 0)
            {
                candidates.Add(new("inventory-" + id, Str(row, "title"), Str(row, "system"), "", [], "", "custom", "",
                    "needs-setup", SetupIssues: ["Kein Startweg in der Übergabe vorhanden."]));
            }
            var chosen = candidates.FirstOrDefault(g => g.Status != "needs-setup") ?? candidates[0];
            result.Add(chosen);
            if (chosen.Status == "needs-setup") warnings.Add(chosen.Title + ": " + string.Join("; ", chosen.SetupIssues ?? []));
        }
        return new(result, warnings);
    }
}
