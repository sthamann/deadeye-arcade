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
        DolphinSetup.WriteMerged(Path.Combine(game.WorkingDirectory,"Config","Supermodel.ini"),"Global",values);return true;
    }
}
