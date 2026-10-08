using System.Security.Cryptography;
using System.Text;

namespace Reaper.Core;

public static class TimeCrisis5Controls
{
    public static bool Supports(GameEntry game) => Path.GetFileName(game.Executable)
        .Equals("TimeCrisisGame-Win64-Shipping.exe", StringComparison.OrdinalIgnoreCase);

    public static GameControls Read(GameEntry game, IEnumerable<GunBinding> bindings)
    {
        if (!game.Arguments.Contains("-playside=1", StringComparer.OrdinalIgnoreCase)) return Unknown();
        var helpers = (game.Helpers ?? []).Where(h =>
            new[] { "AutoHotkeyU64.exe", "AutoHotkeyU32.exe", "AutoHotkey.exe" }
                .Contains(Path.GetFileName(h.Executable), StringComparer.OrdinalIgnoreCase) &&
            h.Arguments.Any(a => Path.GetFileName(a)
                .Equals("Deadeye-RS3-Controls.ahk", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (helpers.Length != 1) return Unknown();
        string? script = helpers[0].Arguments.FirstOrDefault(a => Path.GetFileName(a)
            .Equals("Deadeye-RS3-Controls.ahk", StringComparison.OrdinalIgnoreCase));
        if (script is null || !File.Exists(script)) return Unknown();
        // Only this reviewed, game-scoped helper has the pedal/credit semantics
        // shown below. A modified script must not inherit a plausible legend.
        string normalized = File.ReadAllText(script).Replace("\r\n", "\n").Trim();
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            != "9308B9910066EAAD5EF48BE30EED10F8D9C222A9E916BE5B3F1F3D2A58FE30F3") return Unknown();
        var rows = new List<ControlRow>();
        void Add(string function, string input, string token) => rows.Add(new(1, I18n.T(function), input, "Time Crisis 5 · Deadeye-RS3-Controls.ahk", "P1|" + token));
        Add("Münze", "1 / Shift+T", "key:49");
        Add("Münze", "5 / Shift+T", "key:53");
        Add("Waffenwechsel", "M / O", "key:77");
        Add("Cursor ein-/ausblenden", "Q / Shift+H", "key:81");
        Add("Linkes Pedal", "Middle mouse / T", "mouse:3");
        Add("Rechtes Pedal", "Right mouse / Y", "mouse:2");
        var demul = (game.Helpers ?? []).FirstOrDefault(h => Path.GetFileName(h.Executable)
            .Equals("DemulShooterX64.exe", StringComparison.OrdinalIgnoreCase) && h.Arguments.Contains("-target=es3") && h.Arguments.Contains("-rom=tc5"));
        string? config = demul is null ? null : Path.Combine(demul.WorkingDirectory, "config.ini");
        if (config is not null && File.Exists(config))
        {
            var values = File.ReadLines(config).Where(l => !l.TrimStart().StartsWith(';'))
                .Select(l => l.Split('=', 2)).Where(p => p.Length == 2)
                .GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last()[1].Trim(), StringComparer.OrdinalIgnoreCase);
            var playerOne = bindings.Where(b => b.Player == 1).ToArray();
            var gun = playerOne.Length == 1 ? playerOne[0] : null;
            if (gun is not null && values.GetValueOrDefault("P1Mode") == "RAWINPUT" &&
                string.Equals(values.GetValueOrDefault("P1DeviceName"), gun.MouseId, StringComparison.OrdinalIgnoreCase))
                rows.Insert(0, new(1, I18n.T("Abzug"), "Left mouse", "Time Crisis 5 · DemulShooter", "P1|mouse:1"));
        }
        return new(rows.ToArray(), I18n.T("Time Crisis 5: Belegung aus dem geprüften Spielhelfer. Start und Pedale sind spielspezifisch; zwei Spieler benötigen verlinkte Automaten. Physische Eingaben sind noch zu prüfen."));
    }

    private static GameControls Unknown() => new([], I18n.T("Time Crisis 5: Spielhelfer fehlt oder wurde verändert. Seine Tastenbelegung kann nicht bestätigt werden."));
}
