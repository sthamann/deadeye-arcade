using System.Text.RegularExpressions;

namespace Reaper.Core;

public record GunSystem(string Id, string Name, string Connection, string Detection, string Software, string Guide);
public record GunFeedback(bool Recoil = true, bool Rumble = true, bool OffscreenReload = true, string Aspect = "16:9");
public record PhysicalGun(string Id, string Name, string SystemId, string IdentityEvidence, string[] InputIds,
    string? MouseId, string? KeyboardId, string? Port, bool DriverHealthy, string[] Issues, bool LiveInputAvailable = false);
public static class GunSystems
{
    public static readonly GunSystem[] Catalog = [
        new("rs3", "RS3 Reaper Pro", "USB · 4 IR-Punkte", "Retro-Shooter-Produktfamilie + COM-ID", "Windows HID – kein Sondertreiber", "https://retroshooter.com/wp-content/uploads/2026/02/Retro-Shooter-Reaper-User-Manual-2026.pdf"),
        new("sinden", "Sinden Lightgun", "USB · Kamera / Bildschirmrand", "Sinden-Produktname", "Sinden Lightgun Software", "https://sindenlightgun.com/drivers/"),
        new("xgunner", "X-Gunner Wireless", "2,4 GHz · USB-Empfänger · IR", "X-Gunner-Produktname", "X-Gunner Konfiguration", "https://hwhxg.com/downloads/"),
        new("blamcon", "Blamcon Vyper", "USB · IR · 12 V für Feedback", "Blamcon-Produktfamilie; Vyper separat bestätigen", "Blamcon ARC", "https://blamcon.com/get-started-with-blamcon/blamcon-arc-gui/")
    ];
    public static string? Identify(string product) => product.Contains("Retro Shooter", StringComparison.OrdinalIgnoreCase) || product.Contains("3AGAME", StringComparison.OrdinalIgnoreCase) ? "rs3"
        : product.Contains("Sinden", StringComparison.OrdinalIgnoreCase) ? "sinden"
        : Regex.IsMatch(product, @"x[ -]?gunner", RegexOptions.IgnoreCase) ? "xgunner"
        : product.Contains("Blamcon", StringComparison.OrdinalIgnoreCase) ? "blamcon" : null;
    public static Dictionary<string, string> DefaultMap(int player) => new()
    {
        ["mouse:1"] = "shoot", ["mouse:2"] = "reload", ["mouse:3"] = "secondary",
        ["key:" + (player == 1 ? 49 : 50)] = "start", ["key:" + (player == 1 ? 53 : 54)] = "coin",
        ["key:85"] = "up", ["key:86"] = "down", ["key:87"] = "left", ["key:88"] = "right"
    };
    public static readonly string[] Actions = ["shoot", "reload", "secondary", "start", "coin", "up", "down", "left", "right", "none"];
    public static bool ValidToken(string token) => Regex.IsMatch(token, @"^(key:([1-9]\d{0,2})|mouse:[1-5])$") && (!token.StartsWith("key:") || int.Parse(token[4..]) <= 255);
    public static Dictionary<string, string> ValidateMap(Dictionary<string, string> map)
    {
        if (map.Count > 32 || map.Any(p => !ValidToken(p.Key) || !Actions.Contains(p.Value))) throw new ArgumentException("Ungültige Gun-Belegung.");
        return new(map);
    }
    public static string[] ReaperConfiguration(GunFeedback settings)
    {
        if (settings.Aspect is not ("16:9" or "4:3")) throw new ArgumentException("Ungültiges Bildformat.");
        return ["ZS", "ZM", settings.Aspect == "4:3" ? "ZN" : "ZW", settings.OffscreenReload ? "ZA" : "ZB", "ZX"];
    }
}
