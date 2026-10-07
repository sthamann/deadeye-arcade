using System.Text.RegularExpressions;
namespace Reaper.Core;

// Configure already installed helpers from the live, independently assigned devices.
// Downloading a patch never establishes game-build compatibility or physical P2 input.
public static class MultiplayerSetup
{
    public static void Configure(GameEntry game, IEnumerable<GunBinding> connected)
    {
        var players = connected.Where(b => b.Player is 1 or 2 && !string.IsNullOrWhiteSpace(b.MouseId)).ToArray();
        if (players.GroupBy(b => b.Player).Any(g => g.Count() > 1) || players.GroupBy(b => b.MouseId, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidDataException(I18n.T("P1 und P2 benötigen unterschiedliche Gun-Geräte."));
        foreach (var directory in (game.Helpers ?? []).Where(h => IsDemulShooter(h.Executable)).Select(h => h.WorkingDirectory).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var values = new Dictionary<string, string>();
            for (int player = 1; player <= 4; player++)
            {
                var gun = players.SingleOrDefault(b => b.Player == player);
                values[$"P{player}Mode"] = "RAWINPUT";
                // Empty names match nameless RDP mice in DemulShooter. Use an unmatched
                // nonempty identifier for every unassigned channel, including P3/P4.
                values[$"P{player}DeviceName"] = gun?.MouseId ?? DisconnectedDevice(player);
            }
            WriteFlat(Path.Combine(directory, "config.ini"), values);
        }
        if ((game.Helpers ?? []).Any(h => IsDemulShooter(h.Executable) && h.Arguments.Contains("-target=model2")))
        {
            string config = Path.Combine(game.WorkingDirectory, "EMULATOR.INI");
            if (File.Exists(config))
            {
                DolphinSetup.WriteMerged(config, "Input", new Dictionary<string, string> { ["UseRawInput"] = "0" });
                DolphinSetup.WriteMerged(config, "Renderer", new Dictionary<string, string> { ["DrawCross"] = "0" });
            }
        }
        if (game.Title.Contains("Blue Estate", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(game.WorkingDirectory, "BlueEstate_Fix.dll")))
        {
            // This patch identifies mice by VID/PID rather than full RawInput paths.
            // Two devices with the same VID/PID cannot safely be assigned through this file.
            var ids = players.ToDictionary(b => b.Player, b => DevicePair(b.MouseId));
            if (ids.Values.Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Values.Count(v => v.Length > 0))
                throw new InvalidDataException(I18n.T("Dieser Blue-Estate-Patch benötigt unterschiedliche VID/PID für P1 und P2."));
            string patch = Path.Combine(game.WorkingDirectory, "LightGun_Patch.ini");
            DolphinSetup.WriteMerged(patch, "LightGuns", new Dictionary<string, string> { ["Gun1"] = ids.GetValueOrDefault(1, ""), ["Gun2"] = ids.GetValueOrDefault(2, "") });
        }
    }
    public static string DisconnectedDevice(int player) => $"DEADEYE_DISCONNECTED_P{player}";
    public static bool IsDemulShooter(string path) => Path.GetFileName(path).Equals("DemulShooter.exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals("DemulShooterX64.exe", StringComparison.OrdinalIgnoreCase);
    public static string DevicePair(string path) => Regex.Match(path, @"VID_[0-9A-F]{4}&PID_[0-9A-F]{4}", RegexOptions.IgnoreCase).Value.ToUpperInvariant();
    public static void WriteFlat(string file, IReadOnlyDictionary<string, string> values)
    {
        string old = File.Exists(file) ? File.ReadAllText(file) : "";
        string newline = old.Contains("\r\n") ? "\r\n" : "\n";
        var lines = old.Replace("\r\n", "\n").Split('\n').ToList();
        foreach (var pair in values)
        {
            var matches = Enumerable.Range(0, lines.Count).Where(i => lines[i].Split('=', 2) is { Length: 2 } item && item[0].Trim().Equals(pair.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0) lines.Add(pair.Key + " = " + pair.Value);
            else { lines[matches[0]] = pair.Key + " = " + pair.Value; foreach (int i in matches.Skip(1).Reverse()) lines.RemoveAt(i); }
        }
        string text = string.Join(newline, lines);
        if (old == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file) && !File.Exists(file + ".before-deadeye-multiplayer")) File.Copy(file, file + ".before-deadeye-multiplayer");
        string temporary = file + ".deadeye-new"; File.WriteAllText(temporary, text); File.Move(temporary, file, true);
    }
}
