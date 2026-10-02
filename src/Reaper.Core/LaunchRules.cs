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
        if (!File.Exists(game.Executable)) throw new FileNotFoundException("Der gespeicherte Starter fehlt.", game.Executable);
        if (!Directory.Exists(game.WorkingDirectory)) throw new DirectoryNotFoundException("Der Spieleordner fehlt.");
        var info = new ProcessStartInfo(game.Executable) { WorkingDirectory = game.WorkingDirectory, UseShellExecute = false };
        foreach (var arg in game.Arguments) info.ArgumentList.Add(arg);
        return info;
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
