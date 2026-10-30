namespace Reaper.Core;

// ARC 2.x USB settings protocol. Only scalar settings and HID key identities are read;
// no vendor code or calibration data is included in the application.
public sealed record BlamconDeviceSettings(int Firmware, IReadOnlyDictionary<int,int> Values)
{
    public static readonly int[] Indices = [0,1,2,3,4,5,6,10,11,20,21,22,23,24,25,26,27,28,29,30];
    public int Get(int index) => Values.TryGetValue(index,out int value) ? value : throw new InvalidDataException("Incomplete Vyper settings.");
    public void Validate()
    {
        if(Firmware is < 0x20100 or >= 0x30000 || Get(0) is < 1 or > 4 || Get(3)!=0)
            throw new InvalidDataException("Vyper requires supported firmware 2.x and mouse/keyboard mode.");
        foreach(int index in Indices) _=Get(index);
        if(Get(1) is <0 or >1 || Get(2) is <0 or >3 || Get(10) is <0 or >1 || Get(6) is <0 or >2)
            throw new InvalidDataException("Unsupported Vyper configuration values.");
    }
    public static string? Token(int code) => code switch {
        >=0 and <=2 => "mouse:"+(code+1), >=3 and <=12 => "key:"+(48+code-3),
        >=13 and <=38 => "key:"+(65+code-13),
        39=>"key:8",40=>"key:13",41=>"key:32",42=>"key:27",43=>"key:46",
        44=>"key:38",45=>"key:40",46=>"key:37",47=>"key:39", _=>null
    };
    public GunBinding Apply(GunBinding binding)
    {
        Validate();
        if(binding.SystemId!="blamcon" || binding.DevicePlayer!=Get(0)) throw new InvalidDataException("Vyper player identity changed during configuration.");
        string pedal=Get(6) switch {0=>"assignable",1=>"right",2=>"middle",_=>throw new InvalidDataException("Unsupported Vyper magazine mode.")};
        var settings=new BlamconSettings(Get(10)==1?"4:3":"16:9",Get(4) switch {0=>"off",1=>"semi",2=>"auto",3=>"burst",_=>throw new InvalidDataException("Unsupported Vyper recoil mode.")},Get(5) switch {0=>"off",1=>"move",2=>"shoot",_=>throw new InvalidDataException("Unsupported Vyper reload mode.")},Get(2)+1,pedal);
        var result=binding with {Blamcon=settings};
        // All actions must come from the same original snapshot. ARC can swap
        // several inputs at once; importing A must not overwrite B's old action.
        var originalActions=GunSystems.UpgradeMap(binding);
        var originalHardware=StartupControls.Hardware(binding);
        void Import(string id,string token)
        {
            if(result.ControlMap?.GetValueOrDefault(id)==token)return;
            string? old=originalHardware.FirstOrDefault(h=>h.Id==id)?.Token;
            string? action=old is null?null:originalActions.GetValueOrDefault(old);
            result=BlamconSetup.Learn(result,id,token);
            if(action is not null)result=result with {ButtonMap=new(result.ButtonMap!){[token]=action}};
        }
        (string Id,int Index)[] controls=[("trigger",20),("a",21),("b",22),("select",24),("start",25),("up",26),("down",27),("left",28),("right",29)];
        foreach(var control in controls)
        {
            var token=Token(Get(control.Index)) ?? throw new InvalidDataException("Unsupported Vyper key combination. Learn this button in Gun Studio.");
            Import(control.Id,token);
        }
        var magazine=pedal=="assignable"?Token(Get(23)):Token(Get(pedal=="right"?21:22));
        if(magazine is null)throw new InvalidDataException("Unsupported Vyper magazine key.");
        Import("magazine",magazine);
        if(Get(11)==2) {
            string toggle=Token(Get(30)) ?? throw new InvalidDataException("Unsupported Vyper selector key.");
            if(result.ControlMap!.Values.Contains(toggle))result=result with {ControlMap=new(result.ControlMap){["aux"]=toggle}};
            else Import("aux",toggle);
        }
        else {
            var controlsWithoutSelector=new Dictionary<string,string>(result.ControlMap!);
            controlsWithoutSelector.Remove("aux");result=result with {ControlMap=controlsWithoutSelector};
        }
        result=result with {ControlMap=new(result.ControlMap!){["pedal"]=magazine}};
        var currentTokens=StartupControls.Hardware(result).Where(h=>h.Token is not null).Select(h=>h.Token!).ToHashSet();
        var cleaned=new Dictionary<string,string>(result.ButtonMap!);
        foreach(var old in originalHardware.Where(h=>h.Token is not null&&!currentTokens.Contains(h.Token)))cleaned.Remove(old.Token!);
        return result with {ButtonMap=cleaned};
    }
    public static string[] Commands(string[] commands)
    {
        if(commands.Any(c=>!BlamconSetup.ValidCommand(c)))throw new ArgumentException("Invalid Vyper command.");
        // Pulse counts are bounded in firmware and need no held serial output mode.
        if(commands.Contains("SM.0.1"))return ["F.0.1.1"];
        if(commands.Contains("SM.1.1"))return ["F.1.1.1"];
        var result=new List<string>();
        foreach(string command in commands.Where(c=>c.StartsWith("CM."))) {
            var parts=command.Split('.');int index=int.Parse(parts[1]),value=int.Parse(parts[2]);
            int address=index switch {0=>3,1=>5,2=>6,3=>10,8=>4,12=>2,_=>throw new ArgumentException("Unknown Vyper setting.")};
            result.Add($"ZY.{address}.{value}.0");
        }
        if(result.Count>0)result.Add("ZY");
        return result.ToArray();
    }
}
