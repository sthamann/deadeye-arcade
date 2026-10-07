using System.Diagnostics;
using System.Security;
using System.Xml.Linq;

namespace Reaper.Core;

public static class LaunchRules
{
    public static ProcessStartInfo Prepare(GameEntry game)
    {
        if (game.Source == "demo") throw new InvalidOperationException(I18n.T("Vorschauspiele können nicht gestartet werden."));
        if (game.Status == "needs-setup") throw new InvalidOperationException(I18n.T("Die Spieleinrichtung ist noch unvollständig."));
        var issues = Issues(game);
        if (issues.Length > 0) throw new IOException(string.Join("\n", issues));
        var info = new ProcessStartInfo(game.Executable) { WorkingDirectory = game.WorkingDirectory, UseShellExecute = false };
        foreach (var arg in game.Arguments) info.ArgumentList.Add(arg);
        return info;
    }
    public static string[] Issues(GameEntry game)
    {
        var issues = new List<string>();
        if (!File.Exists(game.Executable)) issues.Add(I18n.T("Starter fehlt: ") + game.Executable);
        if (!Directory.Exists(game.WorkingDirectory)) issues.Add(I18n.T("Arbeitsordner fehlt: ") + game.WorkingDirectory);
        foreach (var path in game.RequiredFiles ?? [])
            if (!File.Exists(path) && !Directory.Exists(path)) issues.Add(I18n.T("Benötigte Datei fehlt: ") + path);
        foreach (var helper in game.Helpers ?? [])
        {
            if (!File.Exists(helper.Executable)) issues.Add(I18n.T("Helfer fehlt: ") + helper.Executable);
            if (!Directory.Exists(helper.WorkingDirectory)) issues.Add(I18n.T("Helferordner fehlt: ") + helper.WorkingDirectory);
        }
        if (game.Source == "mame" && !File.Exists(game.SourcePath)) issues.Add(I18n.T("ROM-Datei fehlt: ") + game.SourcePath);
        if (game.Source == "teknoparrot")
        {
            try
            {
                var xml = XDocument.Load(game.SourcePath);
                string? path = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "GamePath")?.Value;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(Path.GetFullPath(path, game.WorkingDirectory)))
                    issues.Add(I18n.T("TeknoParrot-GamePath fehlt oder zeigt nicht auf die vorhandene Spielanwendung."));
            }
            catch (Exception e) when (e is IOException or System.Xml.XmlException or ArgumentException)
            { issues.Add(I18n.T("TeknoParrot-Profil nicht lesbar: ") + e.Message); }
        }
        return issues.Distinct().ToArray();
    }
    public static GameEntry Validate(GameEntry game)
    {
        var issues = Issues(game);
        return game with { SetupIssues = issues, Status = issues.Length > 0 ? "needs-setup" : game.Status == "needs-setup" ? "unverified" : game.Status };
    }
    public static string MameController(IEnumerable<GunBinding> bindings)
    {
        var devices = bindings.Where(b => !string.IsNullOrWhiteSpace(b.MouseId)).ToArray();
        var input = new XElement("input", devices.Select(b => new XElement("mapdevice", new XAttribute("device", b.MouseId), new XAttribute("controller", "GUNCODE_" + b.Player))));
        foreach (var binding in devices)
        {
            var map = binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player);
            foreach (var action in new[] { "shoot", "reload", "secondary", "start", "coin", "up", "down", "left", "right" })
            {
                string type = action switch
                {
                    "start" => "START" + binding.Player, "coin" => "COIN" + binding.Player,
                    "shoot" => $"P{binding.Player}_BUTTON1", "reload" => $"P{binding.Player}_BUTTON2", "secondary" => $"P{binding.Player}_BUTTON3",
                    _ => $"P{binding.Player}_JOYSTICK_" + action.ToUpperInvariant()
                };
                string[] tokens = map.Where(pair => pair.Value == action && GunSystems.ValidToken(pair.Key)).Select(pair => MameToken(pair.Key, binding.Player)).OfType<string>().ToArray();
                if (tokens.Length > 0) input.Add(new XElement("port", new XAttribute("type", type), new XElement("newseq", new XAttribute("type", "standard"), string.Join(" OR ", tokens))));
            }
        }
        return new XDocument(new XElement("mameconfig", new XAttribute("version", "10"), new XElement("system", new XAttribute("name", "default"), input))).ToString();
    }
    private static string? MameToken(string token, int player)
    {
        if (token.StartsWith("mouse:")) return $"GUNCODE_{player}_BUTTON{token[6..]}";
        int key = int.Parse(token[4..]);
        if (key is >= 48 and <= 57 or >= 65 and <= 90) return "KEYCODE_" + (char)key;
        string? name = key switch { 13 => "ENTER", 27 => "ESC", 32 => "SPACE", 37 => "LEFT", 38 => "UP", 39 => "RIGHT", 40 => "DOWN", _ => null };
        return name is null ? null : "KEYCODE_" + name;
    }
}

public sealed class ExitGesture
{
    private readonly HashSet<int> down = [];
    private DateTimeOffset? since;
    private bool consumed;
    public void Key(int code, bool pressed, DateTimeOffset now)
    {
        if (pressed) down.Add(code); else down.Remove(code);
        if (consumed) { if (down.Count == 0) consumed = false; else return; }
        bool chord = down.Contains(0x31) && down.Contains(0x35) || down.Contains(0x32) && down.Contains(0x36);
        if (chord) since ??= now; else since = null;
    }
    public bool Ready(DateTimeOffset now) => since.HasValue && now - since.Value >= TimeSpan.FromSeconds(1.8);
    public void Consume() { consumed = true; since = null; }
    public void Reset() { down.Clear(); since = null; consumed = false; }
}
