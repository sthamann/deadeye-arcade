using System.Diagnostics;
using System.Security;
using System.Xml.Linq;

namespace Reaper.Core;

public static class LaunchRules
{
    public static ProcessStartInfo Prepare(GameEntry game)
    {
        if (game.Source == "demo") throw new InvalidOperationException("Vorschauspiele können nicht gestartet werden.");
        if (game.Status == "needs-setup") throw new InvalidOperationException("Die Spieleinrichtung ist noch unvollständig.");
        var issues = Issues(game);
        if (issues.Length > 0) throw new IOException(string.Join("\n", issues));
        var info = new ProcessStartInfo(game.Executable) { WorkingDirectory = game.WorkingDirectory, UseShellExecute = false };
        foreach (var arg in game.Arguments) info.ArgumentList.Add(arg);
        return info;
    }
    public static string[] Issues(GameEntry game)
    {
        var issues = new List<string>();
        if (!File.Exists(game.Executable)) issues.Add("Starter fehlt: " + game.Executable);
        if (!Directory.Exists(game.WorkingDirectory)) issues.Add("Arbeitsordner fehlt: " + game.WorkingDirectory);
        foreach (var path in game.RequiredFiles ?? [])
            if (!File.Exists(path) && !Directory.Exists(path)) issues.Add("Benötigte Datei fehlt: " + path);
        foreach (var helper in game.Helpers ?? [])
        {
            if (!File.Exists(helper.Executable)) issues.Add("Helfer fehlt: " + helper.Executable);
            if (!Directory.Exists(helper.WorkingDirectory)) issues.Add("Helferordner fehlt: " + helper.WorkingDirectory);
        }
        if (game.Source == "mame" && !File.Exists(game.SourcePath)) issues.Add("ROM-Datei fehlt: " + game.SourcePath);
        if (game.Source == "teknoparrot")
        {
            try
            {
                var xml = XDocument.Load(game.SourcePath);
                string? path = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "GamePath")?.Value;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(Path.GetFullPath(path, game.WorkingDirectory)))
                    issues.Add("TeknoParrot-GamePath fehlt oder zeigt nicht auf die vorhandene Spielanwendung.");
            }
            catch (Exception e) when (e is IOException or System.Xml.XmlException or ArgumentException)
            { issues.Add("TeknoParrot-Profil nicht lesbar: " + e.Message); }
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
        var maps = bindings.Where(b => !string.IsNullOrWhiteSpace(b.MouseId)).Select(b => new XElement("mapdevice",
            new XAttribute("device", b.MouseId), new XAttribute("controller", "GUNCODE_" + b.Player)));
        return new XDocument(new XElement("mameconfig", new XAttribute("version", "10"),
            new XElement("system", new XAttribute("name", "default"), new XElement("input", maps)))).ToString();
    }
}

public sealed class ExitGesture
{
    private readonly HashSet<int> down = [];
    private DateTimeOffset? since;
    public void Key(int code, bool pressed, DateTimeOffset now)
    {
        if (pressed) down.Add(code); else down.Remove(code);
        bool chord = down.Contains(0x31) && down.Contains(0x35) || down.Contains(0x32) && down.Contains(0x36);
        if (chord) since ??= now; else since = null;
    }
    public bool Ready(DateTimeOffset now) => since.HasValue && now - since.Value >= TimeSpan.FromSeconds(1.8);
    public void Reset() { down.Clear(); since = null; }
}
