using System.Text.RegularExpressions;

namespace Reaper.Core;

public record GunSystem(string Id, string Name, string Connection, string Detection, string Software, string Guide);
public record GunFeedback(bool Recoil = true, bool Rumble = true, bool OffscreenReload = true, string Aspect = "16:9");
public record PhysicalGun(string Id, string Name, string SystemId, string IdentityEvidence, string[] InputIds,
    string? MouseId, string? KeyboardId, string? Port, bool DriverHealthy, string[] Issues, bool LiveInputAvailable = false);
public static class GunSystems
{
    public static GunSystem[] Catalog => [
        new("rs3", "RS3 Reaper Pro", I18n.T("USB · 4 IR-Punkte"), I18n.T("Retro-Shooter-Produktfamilie + COM-ID"), I18n.T("Windows HID – kein Sondertreiber"), "https://retroshooter.com/wp-content/uploads/2026/02/Retro-Shooter-Reaper-User-Manual-2026.pdf"),
        new("sinden", "Sinden Lightgun", I18n.T("USB · Kamera / Bildschirmrand"), I18n.T("Sinden-Produktname"), "Sinden Lightgun Software", "https://sindenlightgun.com/drivers/"),
        new("xgunner", "X-Gunner Wireless", I18n.T("2,4 GHz · USB-Empfänger · IR"), I18n.T("X-Gunner-Produktname"), I18n.T("X-Gunner Konfiguration"), "https://hwhxg.com/downloads/"),
        new("blamcon", "Blamcon Vyper", I18n.T("USB · IR · 12 V für Feedback"), I18n.T("Blamcon-Produktfamilie; Vyper separat bestätigen"), "Blamcon ARC", "https://blamcon.com/get-started-with-blamcon/blamcon-arc-gui/")
    ];
    public static string? Identify(string product) => product.Contains("Retro Shooter", StringComparison.OrdinalIgnoreCase) || product.Contains("3AGAME", StringComparison.OrdinalIgnoreCase) ? "rs3"
        : product.Contains("Sinden", StringComparison.OrdinalIgnoreCase) ? "sinden"
        : Regex.IsMatch(product, @"x[ -]?gunner", RegexOptions.IgnoreCase) ? "xgunner"
        : product.Contains("Blamcon", StringComparison.OrdinalIgnoreCase) ? "blamcon" : null;
    public static string? Identify(string product, string instance) => Identify(product)
        ?? (Regex.IsMatch(instance, @"VID_1209&PID_000[1-4](?:&[^\\]*)?\\XGUNNER-P[1-4]$", RegexOptions.IgnoreCase) ? "xgunner" : null);
    public static int HardwarePlayer(string system, string mouseId, int fallback)
    {
        var match = Regex.Match(mouseId, system == "rs3" ? @"VID_0483&PID_575([0-3])" : @"VID_1209&PID_000([1-4])", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) + (system == "rs3" ? 1 : 0) : fallback;
    }
    public static Dictionary<string, string> DefaultMap(int player, string system = "rs3")
    {
        var map = new Dictionary<string,string> { ["mouse:1"]="shoot", ["mouse:2"]="reload" };
        if (system == "blamcon") return new(map) { ["mouse:3"] = "secondary" };
        if (system == "xgunner") return new(map) { ["mouse:3"]="secondary", ["key:49"]="start", ["key:13"]="start", ["key:53"]="coin", ["key:32"]="reload", ["key:81"]="secondary", ["key:69"]="secondary", ["key:87"]="up", ["key:83"]="down", ["key:65"]="left", ["key:68"]="right" };
        if (system != "rs3") return map;
        map["mouse:3"]="secondary"; map["key:"+(48+player)]="start"; map["key:"+(52+player)]="coin";
        map["key:"+(player==1?81:83)]="start"; map["key:"+(player==1?77:78)]="secondary";
        int[] keys = player==1 ? [38,40,37,39] : [85,86,87,88];
        string[] directions = ["up","down","left","right"];
        for(int i=0;i<4;i++) map["key:"+keys[i]]=directions[i];
        return map;
    }
    public static Dictionary<string,string> UpgradeMap(GunBinding binding)
    {
        if(binding.ButtonMap is null) return DefaultMap(binding.DevicePlayer,binding.SystemId);
        if (binding.SystemId == "blamcon" && binding.ButtonMap.Count == 2 && binding.ButtonMap.GetValueOrDefault("mouse:1") == "shoot" && binding.ButtonMap.GetValueOrDefault("mouse:2") == "reload") return DefaultMap(binding.DevicePlayer, "blamcon");
        // Upgrade only the exact factory map from <=0.3.3. Preserve every custom map.
        var old = new Dictionary<string,string> { ["mouse:1"]="shoot", ["mouse:2"]="reload", ["mouse:3"]="secondary", ["key:"+(binding.DevicePlayer==1?49:50)]="start", ["key:"+(binding.DevicePlayer==1?53:54)]="coin", ["key:85"]="up", ["key:86"]="down", ["key:87"]="left", ["key:88"]="right" };
        return binding.SystemId=="rs3" && binding.ButtonMap.Count==old.Count && old.All(p=>binding.ButtonMap.GetValueOrDefault(p.Key)==p.Value) ? DefaultMap(binding.DevicePlayer) : new(binding.ButtonMap);
    }
    public static bool ValidControl(string system,string control) => (system switch {
        "rs3" => new[]{"trigger","reload","magazine","start","coin","side","up","down","left","right","stick"},
        "sinden" => new[]{"trigger","pump","front-left","back-left","front-right","back-right","up","down","left","right"},
        "xgunner" => new[]{"trigger","reload","magazine","start","coin","side","side-a","side-b","stick","up","down","left","right"},
        "blamcon" => new[]{"trigger","magazine","pedal","aux","a","b","up","down","left","right","select","start"}, _ => Array.Empty<string>()
    }).Contains(control);
    public static readonly string[] Actions = ["shoot", "reload", "secondary", "start", "coin", "up", "down", "left", "right", "none"];
    public static bool ValidToken(string token) => Regex.IsMatch(token, @"^(key:([1-9]\d{0,2})|mouse:[1-5])$") && (!token.StartsWith("key:") || int.Parse(token[4..]) <= 255);
    public static Dictionary<string, string> ValidateMap(Dictionary<string, string> map)
    {
        if (map.Count > 32 || map.Any(p => !ValidToken(p.Key) || !Actions.Contains(p.Value))) throw new ArgumentException(I18n.T("Ungültige Gun-Belegung."));
        return new(map);
    }
    public static string[] ReaperConfiguration(GunFeedback settings)
    {
        if (settings.Aspect is not ("16:9" or "4:3")) throw new ArgumentException(I18n.T("Ungültiges Bildformat."));
        return ["ZS", "ZM", settings.Aspect == "4:3" ? "ZN" : "ZW", settings.OffscreenReload ? "ZA" : "ZB", "ZX"];
    }
}
