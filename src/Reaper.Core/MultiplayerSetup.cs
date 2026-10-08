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
        ConfigureHouseDead2Remake(game, players);
    }
    public static bool ConfigureHouseDead2Remake(GameEntry game, IReadOnlyList<GunBinding> players)
    {
        if (players.Count == 0 || !SupportsHouseDead2Remake(game)) return false;
        WriteCompactGunAssignments(HouseDead2RemakeConfigPath(), players);
        return true;
    }
    public static string HouseDead2RemakeConfigPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "MegaPixel Studio SA", "THE HOUSE OF THE DEAD 2_ Remake", "lightgun_config.ini");
    public static bool SupportsHouseDead2Remake(GameEntry game)
    {
        if (!game.Title.Contains("The House of the Dead 2", StringComparison.OrdinalIgnoreCase) || !game.Title.Contains("Remake", StringComparison.OrdinalIgnoreCase)) return false;
        // This installed 2.0 plugin uses VID/PID keys from its native mouse library.
        // Unknown plugin versions retain their own assignment workflow.
        bool KnownFile(string relative, string expected)
        {
            string file = Path.Combine(game.WorkingDirectory, relative);
            if (!File.Exists(file) || new FileInfo(file).Length > 1_000_000) return false;
            using var stream = File.OpenRead(file);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        return !string.IsNullOrWhiteSpace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) &&
            KnownFile(Path.Combine("BepInEx", "plugins", "MultiLightgunPlugin.dll"), "98a8d76a7d56843c37f63cfc85c2f91b118bc0f2d218b17f6b65fa9edf980ae1") &&
            KnownFile("MultiMouseLib.dll", "e114e539e7be70c3180357cbaddbb04ec45be5de7be165473cca399f96959fa1");
    }
    public static GameControls ReadCompactGunAssignments(string file, IEnumerable<GunBinding> bindings)
    {
        string[] lines = File.Exists(file) ? File.ReadAllLines(file) : [];
        var rows = new List<ControlRow>();
        foreach (var gun in bindings.Where(b => b.Player is 1 or 2))
        {
            string expected = "Player" + gun.Player + "Gun=" + DevicePair(gun.MouseId);
            if (DevicePair(gun.MouseId).Length == 0 || !lines.Contains(expected, StringComparer.OrdinalIgnoreCase)) continue;
            rows.Add(new(gun.Player, I18n.T("Schießen / Menü bestätigen"), OverlayControls.InputName("mouse:1"), "MultiLightgunPlugin 2.0 · lightgun_config.ini", "P" + gun.Player + "|mouse:1"));
            foreach (int button in new[] { 2, 3 })
                rows.Add(new(gun.Player, I18n.T("Nachladen / Menü zurück"), OverlayControls.InputName("mouse:" + button), "MultiLightgunPlugin 2.0 · lightgun_config.ini", "P" + gun.Player + "|mouse:" + button));
        }
        return new(rows.ToArray(), I18n.T("Belegung des vorhandenen MultiLightgunPlugin 2.0. Mehrspielermodus im Spiel einschalten; unabhängige Treffer und Wiederbeleben mit beiden Guns sind vor Ort zu prüfen."));
    }
    public static void WriteCompactGunAssignments(string file, IReadOnlyList<GunBinding> players)
    {
        var ids = players.Where(b => b.Player is 1 or 2).ToDictionary(b => b.Player, b => DevicePair(b.MouseId));
        if (ids.Values.Any(v => v.Length == 0) || ids.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count)
            throw new InvalidDataException(I18n.T("P1 und P2 benötigen unterschiedliche Gun-Geräte."));
        WriteFlat(file, new Dictionary<string, string> { ["Player1Gun"] = ids.GetValueOrDefault(1, ""), ["Player2Gun"] = ids.GetValueOrDefault(2, "") }, compactSeparator: true);
    }
    public static string DisconnectedDevice(int player) => $"DEADEYE_DISCONNECTED_P{player}";
    public static bool IsDemulShooter(string path) => Path.GetFileName(path).Equals("DemulShooter.exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals("DemulShooterX64.exe", StringComparison.OrdinalIgnoreCase);
    public static string DevicePair(string path) => Regex.Match(path, @"VID_[0-9A-F]{4}&PID_[0-9A-F]{4}", RegexOptions.IgnoreCase).Value.ToUpperInvariant();
    public static void WriteFlat(string file, IReadOnlyDictionary<string, string> values, bool compactSeparator = false)
    {
        string old = File.Exists(file) ? File.ReadAllText(file) : "";
        string newline = old.Contains("\r\n") ? "\r\n" : "\n";
        var lines = old.Replace("\r\n", "\n").Split('\n').ToList();
        foreach (var pair in values)
        {
            var matches = Enumerable.Range(0, lines.Count).Where(i => lines[i].Split('=', 2) is { Length: 2 } item && item[0].Trim().Equals(pair.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            string assignment = pair.Key + (compactSeparator ? "=" : " = ") + pair.Value;
            if (matches.Length == 0) lines.Add(assignment);
            else { lines[matches[0]] = assignment; foreach (int i in matches.Skip(1).Reverse()) lines.RemoveAt(i); }
        }
        string text = string.Join(newline, lines);
        if (old == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file) && !File.Exists(file + ".before-deadeye-multiplayer")) File.Copy(file, file + ".before-deadeye-multiplayer");
        string temporary = file + ".deadeye-new"; File.WriteAllText(temporary, text); File.Move(temporary, file, true);
    }
}
