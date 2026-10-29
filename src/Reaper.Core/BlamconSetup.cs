namespace Reaper.Core;

public record BlamconSettings(string Aspect = "16:9", string RecoilMode = "semi", string ReloadMode = "shoot", int CalibrationProfile = 1, string PedalMode = "assignable");

// Manufacturer protocol: https://blamcon.com/get-started-with-blamcon/serial-commands/
// CM selects existing settings; it neither calibrates nor rewrites key bindings.
public static class BlamconSetup
{
    public static string[] Configure(BlamconSettings settings)
    {
        int aspect = settings.Aspect switch { "16:9" => 0, "4:3" => 1, _ => throw new ArgumentException("Invalid Blamcon aspect ratio.") };
        int recoil = settings.RecoilMode switch { "off" => 0, "semi" => 1, "auto" => 2, _ => throw new ArgumentException("Invalid Blamcon recoil mode.") };
        int reload = settings.ReloadMode switch { "off" => 0, "move" => 1, "shoot" => 2, _ => throw new ArgumentException("Invalid Blamcon reload mode.") };
        int pedal = settings.PedalMode switch { "assignable" => 0, "right" => 1, "middle" => 2, _ => throw new ArgumentException("Invalid Blamcon pedal mode.") };
        if (settings.CalibrationProfile is < 1 or > 4) throw new ArgumentException("Invalid Blamcon calibration profile.");
        return ["CM.0.0", $"CM.1.{reload}", $"CM.2.{pedal}", $"CM.3.{aspect}", $"CM.8.{recoil}", $"CM.12.{settings.CalibrationProfile - 1}", "ES"];
    }
    public static string[] Pulse(string effect) => effect switch
    {
        "recoil" => ["SM.0.1", "FB.0.1", "FB.0.0", "ES"],
        "rumble" => ["SM.1.1", "FB.1.1", "FB.1.0", "ES"],
        _ => throw new ArgumentException("Invalid Blamcon feedback test.")
    };
    public static bool ValidCommand(string command) => command is "ES" or "CM.0.0" or "CM.1.0" or "CM.1.1" or "CM.1.2"
        or "CM.2.0" or "CM.2.1" or "CM.2.2" or "CM.3.0" or "CM.3.1" or "CM.8.0" or "CM.8.1" or "CM.8.2"
        or "CM.12.0" or "CM.12.1" or "CM.12.2" or "CM.12.3" or "SM.0.1" or "SM.1.1" or "FB.0.1" or "FB.0.0" or "FB.1.1" or "FB.1.0";
    public static string Canonical(string control) => GunSystems.CanonicalControl("blamcon",control);
    public static string? Action(string control) => Canonical(control) switch
    {
        "trigger" => "shoot", "reload" => "reload", "magazine"=>"secondary", "side"=>"auxiliary", "stick"=>"mode", "start" => "start", "coin" => "coin",
        "up" or "down" or "left" or "right" => control, _ => null
    };
    public static GunBinding Learn(GunBinding binding, string control, string token)
    {
        if (binding.SystemId != "blamcon" || !GunSystems.ValidControl("blamcon", control) || !GunSystems.ValidToken(token)) throw new ArgumentException("Invalid Blamcon control.");
        var controls = new Dictionary<string,string>(binding.ControlMap ?? []) { [control] = token };
        var map = GunSystems.UpgradeMap(binding);
        var hardware = StartupControls.Hardware(binding);
        string? old = hardware.FirstOrDefault(h => h.Id == control)?.Token;
        if (Action(control) is { } action)
        {
            if (old is not null && map.GetValueOrDefault(old) == action && !hardware.Any(h => h.Id != control && h.Token == old)) map.Remove(old);
            map[token] = action;
        }
        return binding with { ControlMap = controls, ButtonMap = map };
    }
}
