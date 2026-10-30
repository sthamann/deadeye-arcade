using System.Security.Cryptography;
using System.Text;

namespace Reaper.Core;

public static class TimeCrisis5Controls
{
    private const string LegacyHash = "9308B9910066EAAD5EF48BE30EED10F8D9C222A9E916BE5B3F1F3D2A58FE30F3";
    public static string ManagedPath(GameEntry game) => Path.Combine(game.WorkingDirectory,"Deadeye-Gun-Controls.ahk");
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n","\n").Trim())));
    private static string? LegacyHelper(GameEntry game)
    {
        var helpers=(game.Helpers??[]).Where(h=>new[]{"AutoHotkeyU64.exe","AutoHotkeyU32.exe","AutoHotkey.exe"}.Contains(Path.GetFileName(h.Executable),StringComparer.OrdinalIgnoreCase))
            .SelectMany(h=>h.Arguments).Where(a=>Path.GetFileName(a).Equals("Deadeye-RS3-Controls.ahk",StringComparison.OrdinalIgnoreCase)).ToArray();
        return helpers.Length==1 && File.Exists(helpers[0]) && Hash(File.ReadAllText(helpers[0]))==LegacyHash ? helpers[0] : null;
    }
    private sealed record Route(string Function,string Token,string Target,bool Hold=false);
    private static Route[] Routes(GunBinding gun)
    {
        var hardware=StartupControls.PhysicalGameHardware(gun);
        string? Physical(string id)=>hardware.FirstOrDefault(h=>h.Id==id)?.Token;
        string Required(string? token)=>token is not null && GunSystems.ValidToken(token)?token:throw new ArgumentException(I18n.T("Time Crisis 5 benötigt erfasste Tasten für die ausgewählte Gun."));
        if(StartupControls.ActionToken(gun,"shoot")!="mouse:1") throw new ArgumentException(I18n.T("Time Crisis 5 benötigt den linken Mausabzug für DemulShooter."));
        Route[] routes=[
            new("Münze",Required(StartupControls.ActionToken(gun,"coin")),"+t"),
            new("Münze",Required(StartupControls.ActionToken(gun,"start")),"+t"),
            new("Waffenwechsel",Required(Physical("side") is string side && gun.SystemId is "rs3" or "xgunner" ? side : Physical("down")),"o"),
            new("Cursor ein-/ausblenden",Required(Physical("stick")??Physical("left")),"+h"),
            new("Linkes Pedal",Required(StartupControls.ActionToken(gun,"secondary")),"t",true),
            new("Rechtes Pedal",Required(StartupControls.ActionToken(gun,"reload")),"y",true)
        ];
        if(routes.GroupBy(r=>r.Token).Any(g=>g.Select(r=>r.Target).Distinct().Count()>1))
            throw new ArgumentException(I18n.T("Time Crisis 5 benötigt getrennte Tasten für Münze, Pedale, Waffenwechsel und Fadenkreuz."));
        return routes.DistinctBy(r=>r.Token).ToArray();
    }
    private static string Hotkey(string token)
    {
        if(token.StartsWith("mouse:")) return token switch {"mouse:1"=>"LButton","mouse:2"=>"RButton","mouse:3"=>"MButton","mouse:4"=>"XButton1","mouse:5"=>"XButton2",_=>throw new ArgumentException("Unsupported mouse button.")};
        int key=int.Parse(token[4..]);
        return key switch {>=48 and <=57 or >=65 and <=90=>((char)key).ToString(),13=>"Enter",32=>"Space",37=>"Left",38=>"Up",39=>"Right",40=>"Down",_=>throw new ArgumentException("Unsupported Time Crisis 5 helper key.")};
    }
    private static string Script(Route[] routes) => """
#NoEnv
#SingleInstance Ignore
#Persistent
SendMode Input
SetWorkingDir %A_ScriptDir%
WinWait, ahk_exe TimeCrisisGame-Win64-Shipping.exe,,90
if ErrorLevel
    ExitApp
SetTimer, GameEnded, 500
return
GameEnded:
Process, Exist, TimeCrisisGame-Win64-Shipping.exe
if !ErrorLevel
    ExitApp
return
#IfWinActive ahk_exe TimeCrisisGame-Win64-Shipping.exe
""" + "\n" + string.Join('\n',routes.Select(r=>Hotkey(r.Token)+"::"+(r.Hold?"":"SendInput, ")+r.Target)) + "\n#IfWinActive\n";
    public static GameEntry Configure(GameEntry game,IEnumerable<GunBinding> bindings)
    {
        if(!Supports(game)||!game.Arguments.Contains("-playside=1",StringComparer.OrdinalIgnoreCase))return game;
        string? legacy=LegacyHelper(game);if(legacy is null)return game; // Preserve unreviewed custom helpers.
        var gun=bindings.SingleOrDefault(b=>b.Player==1);if(gun is null)throw new ArgumentException(I18n.T("Time Crisis 5 benötigt eine Gun auf P1."));
        string path=ManagedPath(game),text=Script(Routes(gun));
        if(!File.Exists(path)||File.ReadAllText(path)!=text){File.WriteAllText(path+".new",text);File.Move(path+".new",path,true);}
        return game with {Helpers=game.Helpers!.Select(h=>h with {Arguments=h.Arguments.Select(a=>a==legacy?path:a).ToArray()}).ToArray()};
    }
    public static bool Supports(GameEntry game) => Path.GetFileName(game.Executable)
        .Equals("TimeCrisisGame-Win64-Shipping.exe", StringComparison.OrdinalIgnoreCase);

    public static GameControls Read(GameEntry game, IEnumerable<GunBinding> bindings)
    {
        if (!game.Arguments.Contains("-playside=1", StringComparer.OrdinalIgnoreCase)) return Unknown();
        var helpers = (game.Helpers ?? []).Where(h =>
            new[] { "AutoHotkeyU64.exe", "AutoHotkeyU32.exe", "AutoHotkey.exe" }
                .Contains(Path.GetFileName(h.Executable), StringComparer.OrdinalIgnoreCase) &&
            h.Arguments.Any(a => Path.GetFileName(a)
                .Equals("Deadeye-RS3-Controls.ahk", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(a).Equals("Deadeye-Gun-Controls.ahk",StringComparison.OrdinalIgnoreCase))).ToArray();
        if (helpers.Length != 1) return Unknown();
        string? script = helpers[0].Arguments.FirstOrDefault(a => Path.GetFileName(a)
            .Equals("Deadeye-RS3-Controls.ahk", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(a).Equals("Deadeye-Gun-Controls.ahk",StringComparison.OrdinalIgnoreCase));
        if (script is null || !File.Exists(script)) return Unknown();
        if(Path.GetFileName(script).Equals("Deadeye-Gun-Controls.ahk",StringComparison.OrdinalIgnoreCase))script=Path.Combine(game.WorkingDirectory,"Deadeye-RS3-Controls.ahk");
        if(!File.Exists(script))return Unknown();
        // Only this reviewed, game-scoped helper has the pedal/credit semantics
        // shown below. A modified script must not inherit a plausible legend.
        string normalized = File.ReadAllText(script).Replace("\r\n", "\n").Trim();
        if (Hash(normalized)!=LegacyHash) return Unknown();
        var rows = new List<ControlRow>();
        void Add(string function, string input, string token) => rows.Add(new(1, I18n.T(function), input, "Time Crisis 5 · Deadeye-RS3-Controls.ahk", "P1|" + token));
        Add("Münze", "1 / Shift+T", "key:49");
        Add("Münze", "5 / Shift+T", "key:53");
        Add("Waffenwechsel", "M / O", "key:77");
        Add("Cursor ein-/ausblenden", "Q / Shift+H", "key:81");
        Add("Linkes Pedal", "Middle mouse / T", "mouse:3");
        Add("Rechtes Pedal", "Right mouse / Y", "mouse:2");
        if(bindings.SingleOrDefault(b=>b.Player==1) is {} active && File.Exists(ManagedPath(game)))
        {
            var routes=Routes(active);
            if(Hash(File.ReadAllText(ManagedPath(game)))!=Hash(Script(routes)))return Unknown();
            rows.Clear();
            foreach(var route in routes)Add(route.Function,OverlayControls.InputName(route.Token)+" / "+route.Target,route.Token);
        }
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
