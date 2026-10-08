using System.Text;
using System.Text.RegularExpressions;
namespace Reaper.Core;

public static class DolphinSetup
{
    public static bool IsDolphin(GameEntry game) => Path.GetFileName(game.Executable).Equals("Dolphin.exe",StringComparison.OrdinalIgnoreCase);
    public static string UserDirectory(GameEntry game)
    {
        for(int i=0;i<game.Arguments.Length;i++)
            if(game.Arguments[i] is "-u" or "--user" && i+1<game.Arguments.Length) return Path.GetFullPath(game.Arguments[i+1],game.WorkingDirectory);
            else if(game.Arguments[i].StartsWith("--user=")) return Path.GetFullPath(game.Arguments[i][7..],game.WorkingDirectory);
        string folder=Path.GetDirectoryName(game.Executable)!;
        return File.Exists(Path.Combine(folder,"portable.txt"))?Path.Combine(folder,"User"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Dolphin Emulator");
    }
    public static string? DiscId(GameEntry game)
    {
        string? path=null;
        for(int i=0;i+1<game.Arguments.Length;i++) if(game.Arguments[i] is "-e" or "--exec") path=Path.GetFullPath(game.Arguments[i+1],game.WorkingDirectory);
        if(path is null || !File.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is not (".iso" or ".wbfs")) return null;
        using var stream=File.OpenRead(path); byte[] header=new byte[13];if(stream.Read(header)!=header.Length) return null;
        if(header.AsSpan(0,4).SequenceEqual("WBFS"u8))
        {
            if(header[8] is <9 or >20 || header[12]==0) return null;
            stream.Position=1L<<header[8];if(stream.Read(header,0,6)!=6) return null;
        }
        string id=Encoding.ASCII.GetString(header,0,6);
        return Regex.IsMatch(id,"^[A-Z0-9]{6}$")?id:null;
    }
    public static string? ProfilePath(GameEntry game)
    {
        string? id=DiscId(game); if(id is null) return null;
        string ini=Path.Combine(UserDirectory(game),"GameSettings",id+".ini");if(!File.Exists(ini)) return null;
        string? name=Value(File.ReadAllText(ini),"Controls","WiimoteProfile1");
        return name is not null && Regex.IsMatch(name,"^[A-Za-z0-9_-]+$")?Path.Combine(UserDirectory(game),"Config","Profiles","Wiimote",name+".ini"):null;
    }
    public static string? Value(string text,string section,string key)
    {
        bool active=false; string? result=null;
        foreach(string line in text.Split('\n'))
        {
            string item=line.Trim();if(item.StartsWith('[')) active=SectionName(item)?.Equals(section,StringComparison.OrdinalIgnoreCase)==true;
            else if(active && item.Split('=',2) is {Length:2} pair && pair[0].Trim().Equals(key,StringComparison.OrdinalIgnoreCase)) result=pair[1].Trim();
        }
        return result;
    }
    public static string Merge(string text,string section,IReadOnlyDictionary<string,string> values)
    {
        string newline=text.Contains("\r\n")?"\r\n":"\n";
        var lines=text.Replace("\r\n","\n").Split('\n').ToList();
        int start=lines.FindIndex(l=>SectionName(l)?.Equals(section,StringComparison.OrdinalIgnoreCase)==true);
        if(start<0) { if(lines.Count>0 && lines[^1]!="") lines.Add("");lines.Add("["+section+"]");start=lines.Count-1; }
        int end=start+1;while(end<lines.Count && !lines[end].TrimStart().StartsWith('[')) end++;
        foreach(var pair in values)
        {
            var indices=Enumerable.Range(start+1,end-start-1).Where(i=>lines[i].Split('=',2) is {Length:2} p && p[0].Trim().Equals(pair.Key,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(indices.Length>0)
            {
                lines[indices[0]]=pair.Key+" = "+pair.Value;
                foreach(int i in indices.Skip(1).Reverse()) {lines.RemoveAt(i);end--;}
            }
            else
            {
                int insert=end;while(insert>start+1 && string.IsNullOrWhiteSpace(lines[insert-1]))insert--;
                lines.Insert(insert,pair.Key+" = "+pair.Value);end++;
            }
        }
        return string.Join(newline,lines);
    }
    private static string? SectionName(string line) => Regex.Match(line,@"^\s*\[\s*([^\]]+?)\s*\]") is {Success:true} m?m.Groups[1].Value.Trim():null;
    public static void WriteMerged(string file,string section,IReadOnlyDictionary<string,string> values)
    {
        string old=File.Exists(file)?File.ReadAllText(file):"";
        string text=Merge(old,section,values);if(old==text)return;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if(File.Exists(file)&&!File.Exists(file+".before-deadeye-input")) File.Copy(file,file+".before-deadeye-input");
        string temporary=file+".deadeye-new";File.WriteAllText(temporary,text);File.Move(temporary,file,true);
    }
    public static bool Configure(GameEntry game,string pack,IEnumerable<GunBinding> bindings)
    {
        if(!IsDolphin(game) || !bindings.Any(b=>b.Player==1&&b.SystemId=="rs3"))return false;
        string? id=DiscId(game);if(id is null)return false;
        string original=Path.Combine(pack,"GameSettings",id+".ini");if(!File.Exists(original))return false;
        string settings=File.ReadAllText(original);
        string? name=Value(settings,"Controls","WiimoteProfile1");
        if(name is null || !Regex.IsMatch(name,"^[A-Za-z0-9_-]+$"))return false;
        string source=Path.Combine(pack,"Config","Profiles","Wiimote",name+".ini");if(!File.Exists(source))return false;
        string user=UserDirectory(game), managed="Deadeye_RS3_"+id;
        string profile=Path.Combine(user,"Config","Profiles","Wiimote",managed+".ini");
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        // Accuracy pack remains a separately installed GPL-3.0 asset, including its license.
        string license=Path.Combine(pack,"LICENSE");if(File.Exists(license))File.Copy(license,Path.Combine(Path.GetDirectoryName(profile)!,"Deadeye-accuracy-pack-LICENSE.txt"),true);
        File.WriteAllText(profile,Merge(File.ReadAllText(source),"Profile",ReaperButtons(id,bindings.First(b=>b.Player==1 && b.SystemId=="rs3"))));
        string perGame=Path.Combine(user,"GameSettings",id+".ini");
        WriteMerged(perGame,"Controls",new Dictionary<string,string> { ["WiimoteSource0"]="1",["WiimoteSource1"]="0",["WiimoteSource2"]="0",["WiimoteSource3"]="0",["WiimoteProfile1"]=managed,["PadType0"]="0",["PadType1"]="0",["PadType2"]="0",["PadType3"]="0" });
        if (id.StartsWith("RZJ"))
        {
            // Keep this latency preset local to Extraction, not every Dolphin game.
            WriteMerged(perGame,"Video_Hardware",new Dictionary<string,string>{["VSync"]="False"});
            WriteMerged(perGame,"Video_Settings",new Dictionary<string,string>{["InternalResolution"]="3",["MSAA"]="1",["ShaderCompilationMode"]="1",["WaitForShadersBeforeStarting"]="True"});
        }
        WriteMerged(Path.Combine(user,"Config","Dolphin.ini"),"Display",new Dictionary<string,string>{["Fullscreen"]="True",["RenderToMain"]="True"});
        WriteMerged(Path.Combine(user,"Config","GFX.ini"),"Settings",new Dictionary<string,string>{["AspectRatio"]="3"});
        return true;
    }
    public static Dictionary<string,string> ReaperButtons(string id,GunBinding? binding=null)
    {
        string Input(string control,string fallback)
        {
            string token=binding?.ControlMap?.GetValueOrDefault(control)??fallback;
            if(!GunSystems.ValidToken(token)) throw new ArgumentException("Invalid RS3 physical control mapping.");
            if(token.StartsWith("mouse:")) return "`Click "+(int.Parse(token[6..])-1)+"`";
            int key=int.Parse(token[4..]);return key switch {>=48 and <=90=>"`"+(char)key+"`",37=>"LEFT",38=>"UP",39=>"RIGHT",40=>"DOWN",13=>"RETURN",32=>"SPACE",_=>throw new ArgumentException("Dolphin cannot map this key automatically.")};
        }
        string trigger=Input("trigger","mouse:1"),reload=Input("reload","mouse:2"),magazine=Input("magazine","mouse:3"),start=Input("start","key:49"),coin=Input("coin","key:53"),side=Input("side","key:77"),stick=Input("stick","key:81");
        var values=new Dictionary<string,string> { ["Device"]="DInput/0/Keyboard Mouse",["IR/Relative Input"]="False",["IR/Auto-Hide"]="False",["Buttons/A"]=magazine,["Buttons/B"]=trigger,["Buttons/1"]=side,["Buttons/2"]=stick,["Buttons/+"]=start,["Buttons/-"]=coin,["Buttons/Home"]="",["D-Pad/Up"]=Input("up","key:38"),["D-Pad/Down"]=Input("down","key:40"),["D-Pad/Left"]=Input("left","key:37"),["D-Pad/Right"]=Input("right","key:39") };
        if(id.StartsWith("RZJ"))
        {
            // Standard Wii Remote + Nunchuk layout. A shared desktop mouse cannot isolate P2.
            values["Nunchuk/Buttons/Z"]=reload;values["Nunchuk/Buttons/C"]=side;
            foreach(string direction in new[]{"Up","Down","Left","Right"}) values["Nunchuk/Stick/"+direction]=values["D-Pad/"+direction];
            values["Tilt/Left"]=coin;
            foreach(string axis in new[]{"X","Y","Z"}) {values["Shake/"+axis]=stick;values["Nunchuk/Shake/"+axis]=reload+" & "+side;}
            values["Nunchuk/Buttons/Z"]=reload+" & !"+side; values["Nunchuk/Buttons/C"]=side+" & !"+reload;
        }
        else if(id.StartsWith("RHO")) foreach(string axis in new[]{"X","Y","Z"}) values["Shake/"+axis]=reload;
        return values;
    }
}
