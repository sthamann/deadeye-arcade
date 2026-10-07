using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Reaper.Core;

// Monotonic milliseconds; keyboard repeats do not restart or accumulate a hold.
public sealed class TriggerHold
{
    private readonly HashSet<string> down = new(StringComparer.OrdinalIgnoreCase);
    private long? since;
    private bool consumed;
    public bool Pressed => down.Count > 0;
    public void Button(string token, bool pressed, long now)
    {
        if (pressed) { if (down.Add(token) && down.Count == 1 && !consumed) since = now; }
        else if (down.Remove(token) && down.Count == 0) { since = null; consumed = false; }
    }
    public bool Ready(long now) => !consumed && since is long start && now - start >= 10_000;
    public void Consume() { consumed = true; since = null; }
    public void Reset() { down.Clear(); since = null; consumed = false; }
}

public record ControlRow(int Player, string Function, string Input, string Evidence);
public record GameControls(ControlRow[] Rows, string Note);

public static class OverlayControls
{
    public static string InputName(string token) => token.StartsWith("mouse:") ? "Gun-Taste / Maus " + token[6..]
        : token.StartsWith("key:") && int.TryParse(token[4..], out int key) ? KeyName(key) : token;
    private static string KeyName(int key) => key is >= 48 and <= 90 ? "Taste " + (char)key : key switch
    { 13 => "Enter", 27 => "Escape", 32 => "Leertaste", 37 => "Links", 38 => "Oben", 39 => "Rechts", 40 => "Unten", _ => "Taste " + key };
    public static string ActionName(string action) => action switch
    { "shoot" => "Abzug / Schießen", "reload" => "Nachladen / Zurück", "secondary" => "Zweite Aktion", "start" => "Start", "coin" => "Münze", "up" => "Oben", "down" => "Unten", "left" => "Links", "right" => "Rechts", _ => action };
    public static GameControls Read(GameEntry game, string dataDirectory, IEnumerable<GunBinding> bindings)
    {
        try
        {
            if (game.Source == "mame")
            {
                string path = Path.Combine(dataDirectory, "controllers", "reaper.cfg");
                var doc = XDocument.Load(path);
                var rows = doc.Descendants("port").Select(p => new ControlRow(
                    Player((string?)p.Attribute("type") ?? ""), MameLabel((string?)p.Attribute("type") ?? ""), MameInput(p.Element("newseq")?.Value ?? "Nicht belegt"), "Reaper-MAME-Controller")).ToArray();
                return new(rows, "Startbelegung aus reaper.cfg. Individuelle MAME-Spielbelegungen können sie überschreiben.");
            }
            if (game.Source == "teknoparrot")
            {
                var doc = XDocument.Load(game.SourcePath);
                string api = doc.Descendants().Where(e => e.Name.LocalName == "FieldInformation")
                    .FirstOrDefault(e => Value(e, "FieldName") == "Input API") is { } field ? Value(field, "FieldValue") : "Unbekannt";
                var rows = doc.Descendants().Where(e => e.Name.LocalName == "JoystickButtons" && e.Elements().Any(c => c.Name.LocalName == "ButtonName"))
                    .Where(e => Value(e, "HideWith" + api) != "true")
                    .Select(e => new ControlRow(Player(Value(e, "InputMapping")), Value(e, "ButtonName"), TeknoInput(e, api, bindings), "TeknoParrot · " + api)).ToArray();
                return new(rows, "Aus dem gestarteten TeknoParrot-Profil gelesen. Helfer wie DemulShooter können weitere Zuordnungen vornehmen.");
            }
            if (Path.GetFileName(game.Executable).Contains("dolphin", StringComparison.OrdinalIgnoreCase))
            {
                string? path = DolphinConfig(game);
                if (path is not null)
                {
                    int player = 0; var rows = new List<ControlRow>();
                    foreach (string line in File.ReadLines(path))
                    {
                        var section = Regex.Match(line.Trim(), @"^\[Wiimote([12])\]$");
                        if (line.Trim().StartsWith('[')) player = section.Success ? int.Parse(section.Groups[1].Value) : 0;
                        if (player == 0 || !line.Contains('=')) continue;
                        string[] pair = line.Split('=', 2); string key = pair[0].Trim();
                        if (key.StartsWith("Buttons/") || key.StartsWith("D-Pad/")) rows.Add(new(player, key.Split('/')[1], string.IsNullOrWhiteSpace(pair[1]) ? "Nicht belegt" : pair[1].Trim(), "Dolphin · WiimoteNew.ini"));
                    }
                    return new(rows.ToArray(), "Globale Dolphin-Wiimote-Belegung. Titelprofile und Kommandozeilen-Overrides können abweichen.");
                }
            }
            if (Path.GetFileName(game.Executable).Contains("retroarch", StringComparison.OrdinalIgnoreCase))
            {
                var paths = new List<string> { Path.Combine(Path.GetDirectoryName(game.Executable)!, "retroarch.cfg") };
                for (int i = 0; i + 1 < game.Arguments.Length; i++)
                    if (game.Arguments[i] is "-c" or "--config") { paths.Clear(); paths.Add(Path.GetFullPath(game.Arguments[++i], game.WorkingDirectory)); }
                    else if (game.Arguments[i] is "--appendconfig") paths.AddRange(game.Arguments[++i].Split('|').Select(p => Path.GetFullPath(p, game.WorkingDirectory)));
                var values = new Dictionary<string, string>();
                foreach (var path in paths.Where(File.Exists)) foreach (string line in File.ReadLines(path))
                {
                    var pair = line.Split('=', 2); if (pair.Length == 2 && !pair[0].TrimStart().StartsWith('#')) values[pair[0].Trim()] = pair[1].Trim().Trim('"');
                }
                var rows = new List<ControlRow>();
                foreach (var pair in values)
                {
                    var match = Regex.Match(pair.Key, @"^input_player([12])_(gun_trigger|gun_reload|gun_aux_a|gun_aux_b|gun_start|gun_select|a|b|start|select)(?:_(mbtn|btn|axis))?$");
                    if (!match.Success) continue;
                    string label = match.Groups[2].Value switch { "gun_trigger" => "Abzug", "gun_reload" => "Nachladen", "gun_aux_a" or "a" => "A", "gun_aux_b" or "b" => "B", "gun_start" or "start" => "Start", _ => "Select / Münze (Core abhängig)" };
                    string input = match.Groups[3].Value switch { "mbtn" => "Maustaste ", "btn" => "Controller-Taste ", "axis" => "Achse ", _ => "Taste " };
                    rows.Add(new(int.Parse(match.Groups[1].Value), label, pair.Value == "nul" || pair.Value == "-1" ? "Nicht belegt" : input + pair.Value, "RetroArch-Konfiguration"));
                }
                return new(rows.ToArray(), "RetroArch-Konfiguration mit expliziten Zusatzdateien. Core-/Content-Overrides und Remaps sind nicht bestätigt.");
            }
            return new([], "Für dieses System ist die Spielbelegung noch nicht auslesbar. Unten steht deine gespeicherte Gun-/Menübelegung; sie bestätigt keine Spielzuordnung.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException)
        { return new([], "Spielprofil konnte nicht gelesen werden. Die Gun-/Menübelegung bleibt verfügbar."); }
    }
    private static string Value(XElement e, string name) => e.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value ?? "";
    private static int Player(string name) => Regex.Match(name, @"^(?:P|Player\s*)([12])", RegexOptions.IgnoreCase) is { Success:true } m ? int.Parse(m.Groups[1].Value)
        : Regex.Match(name, @"^(?:COIN|START|Coin|Service)([12])$", RegexOptions.IgnoreCase) is { Success:true } n ? int.Parse(n.Groups[1].Value) : 0;
    private static string MameLabel(string name) => name.StartsWith("COIN") ? "Münze" : name.StartsWith("START") ? "Start" : Regex.Replace(name, @"^P[12]_", "").Replace("BUTTON", "Taste ").Replace("JOYSTICK_", "Steuerkreuz ");
    private static string MameInput(string sequence)
    {
        string result = Regex.Replace(sequence, @"\bGUNCODE_([12])_BUTTON(\d+)\b", m => "P" + m.Groups[1].Value + " · Gun-Taste " + m.Groups[2].Value);
        return Regex.Replace(result, @"\bKEYCODE_([A-Z0-9_]+)\b", m => "Taste " + m.Groups[1].Value);
    }
    private static string TeknoInput(XElement button, string api, IEnumerable<GunBinding> bindings)
    {
        if (api == "RawInput")
        {
            var raw = button.Elements().FirstOrDefault(e => e.Name.LocalName == "RawInputButton");
            if (raw is null || Value(raw, "DeviceType") == "None") return "Nicht belegt";
            string device = Value(raw, "DevicePath"); var binding = bindings.FirstOrDefault(b => string.Equals(b.MouseId, device, StringComparison.OrdinalIgnoreCase) || string.Equals(b.KeyboardId, device, StringComparison.OrdinalIgnoreCase));
            string key = Value(raw, "MouseButton") is { Length:>0 } mouse && mouse != "None" ? mouse : Value(raw, "KeyboardKey");
            return (binding is null ? "Profilgerät (Gun-Zuordnung offen) · " : "P" + binding.Player + " · ") + key;
        }
        string label = Value(button, api == "DirectInput" ? "BindNameDi" : api == "XInput" ? "BindNameXi" : "BindName");
        return string.IsNullOrWhiteSpace(label) ? "Nicht belegt / Eingabemodus ungeklärt" : label;
    }
    private static string? DolphinConfig(GameEntry game)
    {
        string? user = null;
        for (int i = 0; i < game.Arguments.Length; i++)
            if (game.Arguments[i] is "-u" or "--user" && i + 1 < game.Arguments.Length) user = Path.GetFullPath(game.Arguments[i + 1], game.WorkingDirectory);
            else if (game.Arguments[i].StartsWith("--user=")) user = Path.GetFullPath(game.Arguments[i][7..], game.WorkingDirectory);
        string baseDir = Path.GetDirectoryName(game.Executable)!;
        string path = user is not null ? Path.Combine(user, "Config", "WiimoteNew.ini") : File.Exists(Path.Combine(baseDir, "portable.txt"))
            ? Path.Combine(baseDir, "User", "Config", "WiimoteNew.ini") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dolphin Emulator", "Config", "WiimoteNew.ini");
        return File.Exists(path) ? path : null;
    }
}
