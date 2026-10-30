namespace Reaper.Core;

public record PlayerControlAudit(int Player, string Status, string[] Issues, StartupControlHint[] Controls);
public record GameControlReview(string GameId, string Title, string System, string Status, PlayerControlAudit[] Players, string Note, bool RequiredActionsChecked = false);
public record ControlAuditReport(DateTimeOffset Time, GameControlReview[] Games);

// Read the active profile, not a list of assumed defaults. Mapping coverage never
// asserts that a game has been played or that a physical gun has been tested.
public static class GameControlAudit
{
    public static bool GameplayAction(string name) => !name.Contains("NOT USED",StringComparison.OrdinalIgnoreCase)
        && !System.Text.RegularExpressions.Regex.IsMatch(name,@"^(Service|Test|Volume|Xtreme Audio|Rear Cover Eye|Enter Switch|Up Select|Down Select|Select Up|Select Down|Menu Up|Menu Down)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    public static string[] RequiredDolphin(string id)
    {
        string[] common = ["Buttons/A", "Buttons/B", "Buttons/+"];
        string[] extra = id[..Math.Min(3,id.Length)] switch
        {
            "RZJ" => ["Nunchuk/Buttons/Z", "Nunchuk/Buttons/C", "Tilt/Left", "Shake/X", "Nunchuk/Shake/X", "Nunchuk/Stick/Up", "Nunchuk/Stick/Down", "Nunchuk/Stick/Left", "Nunchuk/Stick/Right"],
            "RBU" => ["Shake/X", "Shake/Y", "Shake/Z", "D-Pad/Up", "Nunchuk/Stick/Up", "Nunchuk/Stick/Down", "Nunchuk/Stick/Left", "Nunchuk/Stick/Right"],
            "SBD" => ["Buttons/1", "Buttons/-", "Shake/X", "Shake/Y", "Shake/Z", "D-Pad/Up", "D-Pad/Down", "D-Pad/Left", "D-Pad/Right"],
            "RHO" => ["Buttons/1", "Buttons/2", "Shake/X", "Shake/Y", "Shake/Z"],
            "RGS" => ["Swing/Down"],
            "RZP" => ["Nunchuk/Buttons/Z", "Nunchuk/Stick/Up", "Nunchuk/Stick/Down", "Nunchuk/Stick/Left", "Nunchuk/Stick/Right"],
            _ => []
        };
        return extra.Length == 0 ? [] : common.Concat(extra).ToArray();
    }
    public static GameControlReview Read(GameEntry game, string data, IEnumerable<GunBinding> bindings, IEnumerable<InputDevice>? devices = null)
    {
        var guns = bindings.Where(b=>b.Player is 1 or 2).OrderBy(b=>b.Player).ToArray();
        if(TimeCrisis5Controls.Supports(game))guns=guns.Where(b=>b.Player==1).ToArray();
        var mame=game.Source=="mame"?MameControls.Metadata(game,data):null;
        if(mame is not null && MameControls.Players(mame)>0)guns=guns.Where(b=>b.Player<=MameControls.Players(mame)).ToArray();
        var controls = OverlayControls.Read(game,data,guns,devices);
        bool dolphin = DolphinSetup.IsDolphin(game);
        string? id = dolphin ? DolphinSetup.DiscId(game) : null;
        string[] required = id is null ? [] : RequiredDolphin(id);
        bool requiredActionsChecked = dolphin ? required.Length > 0 : game.Source == "teknoparrot" || mame is not null || TimeCrisis5Controls.Supports(game);
        var players = new List<PlayerControlAudit>();
        foreach(var gun in guns)
        {
            var issues = new List<string>();
            var hints = StartupControls.ForPlayer(controls,gun);
            bool review=!requiredActionsChecked, missing=false;
            if(!requiredActionsChecked) issues.Add(I18n.T("Die vollständige Liste benötigter Spielaktionen ist noch nicht geprüft."));
            if(dolphin && required.Length>0)
            {
                string? profile = DolphinSetup.ProfilePath(game,gun.Player);
                if(profile is null || !File.Exists(profile)) issues.Add(I18n.T("Aktives Spielerprofil fehlt."));
                else
                {
                    string text = File.ReadAllText(profile);
                    string settings = Path.Combine(DolphinSetup.UserDirectory(game),"GameSettings",id+".ini");
                    if(DolphinSetup.Value(File.ReadAllText(settings),"Controls","WiimoteSource"+(gun.Player-1))!="1") issues.Add(I18n.T("Spielereingang ist deaktiviert."));
                    string device = DolphinSetup.Value(text,"Profile","Device")??"";
                    if(guns.Length>1 && device!="DSUClient/"+(gun.Player-1)+"/"+DolphinGunBridge.DeviceName) issues.Add(I18n.T("Getrennte Gun-Eingänge sind nicht eingerichtet."));
                    foreach(string key in required)
                    {
                        string? expression = DolphinSetup.Value(text,"Profile",key);
                        if(string.IsNullOrWhiteSpace(expression) || !StartupControls.Resolve(new(gun.Player,key,expression,"Dolphin",expression),gun).Resolved)
                            issues.Add(I18n.T("Aktion nicht an der Gun erreichbar: ")+OverlayControls.DolphinLabel(key,id));
                    }
                }
            }
            else
            {
                var rows = controls.Rows.Where(r=>r.Player==gun.Player && GameplayAction(r.Function)).ToArray();
                if(rows.Length==0) {issues.Add(I18n.T("Spielbelegung für diesen Spieler ist nicht auslesbar."));review=true;}
                if(game.Source=="teknoparrot" && rows.Length==0 && !guns.Any(g=>g.Player==1) && controls.Rows.Any(r=>r.Player==1 && GameplayAction(r.Function)))
                {
                    missing=true;
                    issues.Add(I18n.T("Dieses Spielprofil benötigt eine Gun auf P1. Weise die gewünschte Gun P1 zu."));
                }
                if(game.Source=="teknoparrot")
                    foreach(var aim in rows.Where(r=>r.Function.Contains("Light Gun",StringComparison.OrdinalIgnoreCase)))
                        if(!aim.Input.StartsWith("P"+gun.Player+" · ",StringComparison.Ordinal))
                        {missing=true;issues.Add(I18n.T("Zieleingang gehört nicht zur physischen Gun dieses Spielers: ")+aim.Function);}
                foreach(var row in rows.Where(r=>r.Function is not null && !r.Function.Contains("Light Gun",StringComparison.OrdinalIgnoreCase)))
                    if(row.Input==I18n.T("Nicht belegt") || row.Input.StartsWith(I18n.T("Nicht belegt /")))
                    {issues.Add(I18n.T("Aktion nicht belegt: ")+row.Function);if(game.Source=="teknoparrot"||mame is not null)missing=true;else review=true;}
                review=review || rows.Any(row=>!row.Function.Contains("Light Gun",StringComparison.OrdinalIgnoreCase) && !row.Function.Contains("Service",StringComparison.OrdinalIgnoreCase) && !StartupControls.Resolve(row,gun).Resolved);
                if(review && issues.Count==0) issues.Add(I18n.T("Einige Spielaktionen sind keiner physischen Gun-Taste bestätigt zugeordnet."));
                if(TimeCrisis5Controls.Supports(game))
                    foreach(string function in new[]{"Abzug","Münze","Waffenwechsel","Linkes Pedal","Rechtes Pedal"}.Select(I18n.T))
                        if(!rows.Any(r=>r.Function==function && StartupControls.Resolve(r,gun).Resolved))
                        {missing=true;issues.Add(I18n.T("Aktion nicht an der Gun erreichbar: ")+function);}
                if(mame is not null)
                {
                    var effective=MameControls.Effective(game,data);
                    string controller=Path.Combine(data,"controllers","reaper.cfg");
                    var maps=System.Xml.Linq.XDocument.Load(controller).Descendants("mapdevice").ToArray();
                    bool Matches(string? device,string target)=>!string.IsNullOrWhiteSpace(device) && maps.Any(m=>(string?)m.Attribute("controller")==target && string.Equals((string?)m.Attribute("device"),device,StringComparison.OrdinalIgnoreCase));
                    if(!Matches(gun.MouseId,"GUNCODE_"+gun.Player)) {missing=true;issues.Add(I18n.T("Die MAME-Gun-Nummer ist nicht an dieses physische USB-Gerät gebunden."));}
                    if(rows.Any(r=>r.Expression?.Contains("KEYCODE_"+gun.Player+"_")==true) && !Matches(gun.KeyboardId,"KEYCODE_"+gun.Player)) {missing=true;issues.Add(I18n.T("Die MAME-Tastaturnummer ist nicht an den Tastatureingang dieser Gun gebunden."));}
                    foreach(string axis in MameControls.RequiredPorts(mame,gun.Player).Where(p=>p.Contains("_LIGHTGUN_")||p.Contains("_AD_STICK_")))
                        if(effective.GetValueOrDefault(axis)!=$"GUNCODE_{gun.Player}_{axis[^1]}AXIS")
                        {missing=true;issues.Add(I18n.T("Zielachse ist nicht der zugeordneten Gun zugewiesen: ")+axis);}
                }
                // WinRaw keyboard binds in our generated RetroArch profiles are global.
                // A different mouse index does not isolate equal keyboard key codes.
                if(RetroArchSetup.IsRetroArch(game) && controls.Rows.Where(r=>r.Player==gun.Player && r.Expression?.StartsWith("P"+gun.Player+"|key:")==true)
                    .Any(row=>controls.Rows.Any(other=>other.Player!=gun.Player && guns.Any(g=>g.Player==other.Player) && other.Expression=="P"+other.Player+"|"+row.Expression!.Split('|')[1])))
                {
                    missing=true;
                    issues.Add(I18n.T("RetroArch empfängt gleiche Tastaturcodes beider Guns gemeinsam. Für getrennte Spieler sind unterschiedliche Tastencodes oder gerätegetrennte Tastatureingänge erforderlich."));
                }
            }
            string status = missing || dolphin && required.Length>0 && issues.Count>0 ? "needs-action" : review || dolphin && required.Length==0 ? "needs-review" : "mapping-checked";
            players.Add(new(gun.Player,status,issues.Distinct().ToArray(),hints));
        }
        string state=players.Count==0?"needs-action":players.Any(p=>p.Status=="needs-action")?"needs-action":players.Any(p=>p.Status=="needs-review")?"needs-review":"mapping-checked";
        return new(game.Id,game.Title,dolphin?"Dolphin":game.Source,state,players.ToArray(),controls.Note,requiredActionsChecked);
    }
    public static ControlAuditReport Save(IEnumerable<GameEntry> games,string data,IEnumerable<GunBinding> bindings,IEnumerable<InputDevice>? devices=null,bool merge=false)
    {
        var guns=bindings.ToArray();var inputs=devices?.ToArray();
        var report=new ControlAuditReport(DateTimeOffset.UtcNow,games.Select(game=> {
            try { return Read(game,data,guns,inputs); }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException) {
                return new GameControlReview(game.Id,game.Title,game.Source,"needs-action",[],e.Message);
            }
        }).ToArray());
        Directory.CreateDirectory(data);
        string file=Path.Combine(data,"controls-audit.json");
        if(merge && File.Exists(file))
        {
            try {
                var old=System.Text.Json.JsonSerializer.Deserialize<ControlAuditReport>(File.ReadAllText(file),JsonDefaults.Options);
                if(old is not null) report=report with {Games=old.Games.Where(g=>!report.Games.Any(updated=>updated.GameId==g.GameId)).Concat(report.Games).ToArray()};
            } catch(System.Text.Json.JsonException) { /* Regenerate from the active library next time. */ }
        }
        File.WriteAllText(Path.Combine(data,"controls-audit.json"),System.Text.Json.JsonSerializer.Serialize(report,JsonDefaults.Options));
        return report;
    }
}
