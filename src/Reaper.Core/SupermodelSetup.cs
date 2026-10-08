using System.Text.RegularExpressions;

namespace Reaper.Core;

public static class SupermodelSetup
{
    public static bool IsSupermodel(GameEntry game)=>Path.GetFileName(game.Executable).Equals("Supermodel.exe",StringComparison.OrdinalIgnoreCase);
    public static Dictionary<string,int> DeviceNumbers(IEnumerable<InputDevice> enumeration,string kind) => enumeration.Reverse().Where(d=>d.Kind==kind && !d.Id.Contains("Root#RDP_",StringComparison.OrdinalIgnoreCase)).Select((d,i)=>(d.Id,Number:i+1)).ToDictionary(x=>x.Id,x=>x.Number,StringComparer.OrdinalIgnoreCase);
    public static bool Configure(GameEntry game,IEnumerable<GunBinding> bindings,IEnumerable<InputDevice> enumeration)
    {
        if(!IsSupermodel(game))return false;
        var mice=DeviceNumbers(enumeration,"mouse");var keyboards=DeviceNumbers(enumeration,"keyboard");
        var players=bindings.Where(b=>b.Player is 1 or 2 && mice.ContainsKey(b.MouseId)).ToArray();if(players.Length==0)return false;
        var values=new Dictionary<string,string>{["InputSystem"]="rawinput"};
        foreach(int player in new[]{1,2})
        {
            var binding=players.SingleOrDefault(b=>b.Player==player);string suffix=player==1?"":"2";
            string Axis(string axis)=>binding is null?"\"NONE\"":$"\"MOUSE{mice[binding.MouseId]}_{axis}AXIS\"";
            string Button(string action)
            {
                if(binding is null)return "\"NONE\"";
                var map=binding.ButtonMap??GunSystems.DefaultMap(player,binding.SystemId);
                string? token=map.FirstOrDefault(x=>x.Value==action).Key;
                if(token?.StartsWith("mouse:")==true) return $"\"MOUSE{mice[binding.MouseId]}_"+(token[6..] switch {"1"=>"LEFT_BUTTON","2"=>"RIGHT_BUTTON","3"=>"MIDDLE_BUTTON",_=>"NONE"})+"\"";
                if(token?.StartsWith("key:")==true && binding.KeyboardId is not null && keyboards.TryGetValue(binding.KeyboardId,out int keyboard))
                {
                    int key=int.Parse(token[4..]);string? name=key switch {>=48 and <=90=>((char)key).ToString(),13=>"RETURN",32=>"SPACE",37=>"LEFT",38=>"UP",39=>"RIGHT",40=>"DOWN",_=>null};
                    if(name is not null)return $"\"KEY{keyboard}_{name}\"";
                }
                return "\"NONE\"";
            }
            values["InputGunX"+suffix]=values["InputAnalogGunX"+suffix]=Axis("X");values["InputGunY"+suffix]=values["InputAnalogGunY"+suffix]=Axis("Y");
            values["InputTrigger"+suffix]=values["InputAnalogTriggerLeft"+suffix]=Button("shoot");values["InputOffscreen"+suffix]=values["InputAnalogTriggerRight"+suffix]=Button("reload");
            values["InputStart"+player]=Button("start");values["InputCoin"+player]=Button("coin");
            values["InputAutoTrigger"+suffix]="1";
        }
        string path=Path.Combine(game.WorkingDirectory,"Config","Supermodel.ini");
        DolphinSetup.WriteMerged(path,"Global",values);
        // A title section overrides Global. Replace its input routes as well, while
        // preserving calibration, video, NVRAM and other title-specific settings.
        string set=SetName(game);
        if(set.Length>0) DolphinSetup.WriteMerged(path,set,values);
        return true;
    }

    private static string SetName(GameEntry game) => Path.GetFileNameWithoutExtension(game.SourcePath.Replace('\\','/'));
    public static GameControls Read(GameEntry game,IEnumerable<GunBinding> bindings,IEnumerable<InputDevice> enumeration)
    {
        string path=Path.Combine(game.WorkingDirectory,"Config","Supermodel.ini");
        if(!File.Exists(path)) return new([],I18n.T("Spielprofil konnte nicht gelesen werden. Die Gun-/Menübelegung bleibt verfügbar."));
        string text=File.ReadAllText(path),set=SetName(game);
        var mice=DeviceNumbers(enumeration,"mouse");var keys=DeviceNumbers(enumeration,"keyboard");
        var rows=new List<ControlRow>();
        foreach(var binding in bindings.Where(b=>b.Player is 1 or 2))
        {
            string suffix=binding.Player==1?"":"2";
            foreach(var pair in new[]{("InputTrigger"+suffix,"Trigger"),("InputOffscreen"+suffix,"Offscreen / Reload"),
                ("InputAnalogTriggerLeft"+suffix,"Left trigger (analog gun)"),("InputAnalogTriggerRight"+suffix,"Right trigger (analog gun)"),
                ("InputStart"+binding.Player,"Start"),("InputCoin"+binding.Player,"Coin")})
            {
                string value=(DolphinSetup.Value(text,set,pair.Item1)??DolphinSetup.Value(text,"Global",pair.Item1)??"NONE").Trim('"');
                string? token=null;
                var mouse=Regex.Match(value,@"^MOUSE(\d+)_(LEFT_BUTTON|RIGHT_BUTTON|MIDDLE_BUTTON)$");
                var key=Regex.Match(value,@"^KEY(\d+)_([A-Z0-9]+)$");
                if(mouse.Success && mice.GetValueOrDefault(binding.MouseId)==int.Parse(mouse.Groups[1].Value))
                    token="mouse:"+(mouse.Groups[2].Value switch {"LEFT_BUTTON"=>1,"RIGHT_BUTTON"=>2,_=>3});
                else if(key.Success && binding.KeyboardId is not null && keys.GetValueOrDefault(binding.KeyboardId)==int.Parse(key.Groups[1].Value))
                    token=StartupControls.KeyToken(key.Groups[2].Value);
                rows.Add(new(binding.Player,pair.Item2,value,"Supermodel · "+(set.Length>0?set:"Global"),token is null?null:"P"+binding.Player+"|"+token));
            }
        }
        return new(rows.ToArray(),I18n.T("Aktive Supermodel-Belegung mit titelbezogenen Overrides. Unbekannte Geräte bleiben unbestätigt."));
    }
}
