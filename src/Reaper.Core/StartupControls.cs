using System.Text.RegularExpressions;

namespace Reaper.Core;

public record GunControl(string Id, string Label, double X, double Y, string? Token);
public record StartupControlHint(string Function, string Input, string[] ControlIds, bool Resolved);

// Physical positions match Gun Studio. Unpublished vendor keyboard defaults are learned.
public static class StartupControls
{
    public const int DurationMilliseconds = 10_000;
    public static GunControl[] Hardware(GunBinding binding)
    {
        int p=binding.DevicePlayer;
        var rows = new List<GunControl>();
        void Add(string id,string label,double x,double y,string? token=null) => rows.Add(new(id,I18n.T(label),x,y,binding.ControlMap?.GetValueOrDefault(id)??token));
        void Pad(double x,double y,bool rs3=false) {
            int[] keys=p==1?[38,40,37,39]:[85,86,87,88];
            string[] ids=["up","down","left","right"],labels=["Steuerkreuz oben","Steuerkreuz unten","Steuerkreuz links","Steuerkreuz rechts"];
            (double X,double Y)[] offsets=[(0,-16),(0,16),(-16,0),(16,0)];
            for(int i=0;i<4;i++) Add(ids[i],labels[i],x+offsets[i].X,y+offsets[i].Y,rs3?"key:"+keys[i]:null);
        }
        switch(binding.SystemId) {
            case "rs3":
                Add("trigger","Abzug",307,165,"mouse:1"); Add("reload","Grifftasten links / rechts",368,169,"mouse:2");
                Add("magazine","Magazinboden",398,258,"mouse:3"); Add("start","Start",142,154,"key:"+(48+p));
                Add("coin","Münztaste",179,154,"key:"+(52+p)); Add("side","Seitentaste M / N",353,135,"key:"+(p==1?77:78));
                Pad(228,136,true); Add("stick","Stick drücken",228,136,"key:"+(p==1?81:83)); break;
            case "sinden":
                Add("trigger","Abzug",338,172,"mouse:1"); Add("pump","Pumpgriff",170,203);
                Add("front-left","Vorne links",139,159); Add("back-left","Hinten links",187,159);
                Add("front-right","Vorne rechts · Rückseite",139,45); Add("back-right","Hinten rechts · Rückseite",187,45); Pad(267,159); break;
            case "xgunner":
                Add("trigger","Abzug",330,180,"mouse:1"); Add("reload","Rechte Maustaste",368,164,"mouse:2");
                Add("magazine","Mittlere Maustaste",350,135,"mouse:3"); Add("stick","Stick drücken",423,142,"key:13");
                Add("start","Start",235,155,"key:49"); Add("coin","Münztaste",200,155,"key:53");
                Add("side","Leertaste",280,164,"key:32"); Add("side-a","Taste Q · Rückseite",200,65,"key:81"); Add("side-b","Taste E · Rückseite",235,65,"key:69");
                Add("up","Steuerkreuz oben",150,124,"key:87"); Add("down","Steuerkreuz unten",150,156,"key:83");
                Add("left","Steuerkreuz links",134,140,"key:65"); Add("right","Steuerkreuz rechts",166,140,"key:68"); break;
            case "blamcon":
                Add("trigger","Abzug",342,157,"mouse:1"); Add("magazine","Magazin ziehen",246,239, binding.Blamcon?.PedalMode switch { "assignable" => "key:82", "right" => "mouse:2", "middle" => "mouse:3", _ => null });
                Add("a","Taste A · Vordergriff",118,103,"mouse:2"); Add("b","Taste B · Vordergriff",154,103,"mouse:3");
                Pad(273,96); Add("select","Select",249,65); Add("start","Start",296,65);
                Add("pedal","Optionales Pedal",480,249,binding.Blamcon?.PedalMode switch { "assignable" => "key:82", "right" => "mouse:2", "middle" => "mouse:3", _ => null });
                Add("aux","Feuerwahl als Taste",376,134); break;
        }
        return rows.ToArray();
    }
    public static GunControl[] GameHardware(GunBinding binding) => Hardware(binding).Select(h => binding.SystemId == "blamcon" ? h with { Id = BlamconSetup.Canonical(h.Id) } : h).ToArray();
    public static string? KeyToken(string key)
    {
        key=key.ToUpperInvariant();
        if(Regex.IsMatch(key,@"^NUM[0-9]$")) key=key[3..];
        if(Regex.IsMatch(key,@"^D[0-9]$")) key=key[1..];
        if(key.Length==1 && (key[0] is >= '0' and <= '9' or >= 'A' and <= 'Z'))return "key:"+(int)key[0];
        int code=key switch {"RETURN" or "ENTER"=>13,"ESCAPE" or "ESC"=>27,"SPACE"=>32,"LEFT"=>37,"UP"=>38,"RIGHT"=>39,"DOWN"=>40,_=>0};
        return code==0?null:"key:"+code;
    }
    public static StartupControlHint[] ForPlayer(GameControls controls,GunBinding binding)
        {
        var rows=controls.Rows.Where(r=>r.Player==binding.Player||r.Player==0).ToArray();
        // A title profile can expose the same physical input twice: Wii D-pad and
        // Nunchuk weapon selection, for example. Prefer its named game function.
        bool titleFunctions=rows.Any(r=>r.Function==I18n.T("Kinesis / Benutzen (A)"));
        if(titleFunctions) rows=rows.Where(r=>!r.Function.StartsWith("Buttons/") && !r.Function.StartsWith("D-Pad/")).ToArray();
        return rows.Select(row=>Resolve(row,binding)).DistinctBy(r=>(r.Function,r.Input))
            .OrderByDescending(r=>r.Resolved)
            .ThenBy(r=>r.ControlIds.Contains("trigger")?0:r.ControlIds.Contains("reload")?1:r.ControlIds.Contains("magazine")?2:r.ControlIds.Contains("start")?3:r.ControlIds.Contains("coin")?4:5)
            .ToArray();
    }
    public static StartupControlHint Resolve(ControlRow row,GunBinding binding)
    {
        string? expression=row.Expression;
        if(string.IsNullOrWhiteSpace(expression)) return new(row.Function,row.Input,[],false);
        var scoped=Regex.Match(expression,@"^P([12])\|(.+)$");
        if(scoped.Success) {
            if(int.Parse(scoped.Groups[1].Value)!=binding.Player) return new(row.Function,row.Input,[],false);
            expression=scoped.Groups[2].Value;
        }
        var hardware=Hardware(binding); var ids=new List<string>(); bool known=true;
        string Replace(string? token,string original) {
            var buttons=hardware.Where(c=>c.Token==token && token is not null).ToArray();
            if(buttons.Length==0) {known=false;return original;}
            ids.AddRange(buttons.Select(c=>c.Id)); return string.Join(" / ",buttons.Select(c=>c.Label));
        }
        string label;
        if(GunSystems.ValidToken(expression)) label=Replace(expression,expression);
        else if(row.Evidence.StartsWith("Dolphin")) {
            const string bridgePattern=@"`(L2|Cross|Square|Circle|R3|Options|Share|Pad [NSEW])`";
            if(Regex.IsMatch(expression,bridgePattern))
            {
                string unknown=Regex.Replace(Regex.Replace(expression,bridgePattern,""),@"[\s&!|()]","");
                if(unknown.Length>0) return new(row.Function,row.Input,[],false);
                label=Regex.Replace(expression,bridgePattern,m=>{
                    var control=hardware.FirstOrDefault(h=>DolphinGunBridge.Input(binding.SystemId=="blamcon"?BlamconSetup.Canonical(h.Id):h.Id)==m.Groups[1].Value);
                    return Replace(control?.Token,m.Value);
                }).Replace(" & !",I18n.T(" ohne ")).Replace(" & "," + ").Replace(" | ",I18n.T(" oder "));
                return known && ids.Count>0?new(row.Function,label,ids.Distinct().ToArray(),true):new(row.Function,row.Input,[],false);
            }
            // Match a whole supported expression. A foreign device qualifier or unknown
            // operator invalidates the mapping; do not turn it into a guessed button.
            const string pattern=@"`Click ([0-4])`|`(D[0-9]|[A-Za-z0-9])`|\b(LEFT|UP|RIGHT|DOWN|RETURN|SPACE)\b";
            string remainder=Regex.Replace(expression,pattern,"");
            if(Regex.Replace(remainder,@"[\s&!|()]","").Length>0) return new(row.Function,row.Input,[],false);
            label=Regex.Replace(expression,pattern,m=>Replace(m.Groups[1].Success?"mouse:"+(int.Parse(m.Groups[1].Value)+1):KeyToken(m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Value),m.Value));
            label=label.Replace(" & !",I18n.T(" ohne ")).Replace(" & "," + ").Replace(" | ",I18n.T(" oder "));
        }
        else if(row.Evidence=="Reaper-MAME-Controller") {
            const string pattern=@"GUNCODE_([12])_BUTTON([1-5])|KEYCODE_([A-Z0-9]+)";
            string remainder=Regex.Replace(Regex.Replace(expression,pattern,""),@"\bOR\b|\s","");
            if(remainder.Length>0)return new(row.Function,row.Input,[],false);
            label=Regex.Replace(expression,pattern,m=>Replace(m.Groups[1].Success?(int.Parse(m.Groups[1].Value)==binding.Player?"mouse:"+m.Groups[2].Value:null):KeyToken(m.Groups[3].Value),m.Value)).Replace(" OR ",I18n.T(" oder "));
        }
        else return new(row.Function,row.Input,[],false);
        return known && ids.Count>0 ? new(row.Function,label,ids.Distinct().ToArray(),true) : new(row.Function,row.Input,[],false);
    }
}
