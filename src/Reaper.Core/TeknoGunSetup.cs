using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace Reaper.Core;
public static class TeknoGunSetup
{
    public static bool IsAimControl(string name)
    {
        string control=Regex.Replace(Regex.Replace(name,@"^(?:Player [1-4] |P[1-4] )","",RegexOptions.IgnoreCase),@" P[1-4]$","",RegexOptions.IgnoreCase);
        return control is "Light Gun" or "Gun" or "Spray Controller" or "Proton Pack" or "Touch" or "Light Whip";
    }
    // Only controls explicitly named by the selected profile are changed.
    // Special controls such as pedals, weapon selectors or two-stage triggers retain their vendor bindings.
    public static int Configure(GameEntry game,IEnumerable<GunBinding> players)
    {
        if(game.Source!="teknoparrot"||!File.Exists(game.SourcePath)) return 0;
        var document=XDocument.Load(game.SourcePath);
        var api=document.Descendants("FieldInformation").FirstOrDefault(f=>f.Element("FieldName")?.Value=="Input API");
        if(api is null||!api.Descendants("string").Any(x=>x.Value=="RawInput"))return 0;
        var changes=TeknoRequirements.Read(game)?.RestoreMissing(document)??0;
        void Set(XElement node,string name,string value){var child=node.Element(name);if(child?.Value==value)return;if(child is null)node.Add(new XElement(name,value));else child.Value=value;changes++;}
        void Clear(XElement row)
        {
            if(row.Element("RawInputButton") is not { } input) return;
            Set(input,"DevicePath","");Set(input,"MouseButton","None");Set(input,"KeyboardKey","None");
            Set(row,"BindNameRi","");Set(row,"BindName","");
        }
        var active=players.Where(p=>p.Player is 1 or 2).ToArray();
        foreach(var row in document.Descendants("JoystickButtons").ToArray())
        {
            var name=row.Element("ButtonName")?.Value??"";
            var prefix=Regex.Match(name,@"^(?:Player ([12]) |P([12]) )(.+)$",RegexOptions.IgnoreCase);
            var suffix=Regex.Match(name,@"^(.+) P([12])$",RegexOptions.IgnoreCase);
            int number;string control;
            if(prefix.Success){number=int.Parse(prefix.Groups[1].Success?prefix.Groups[1].Value:prefix.Groups[2].Value);control=prefix.Groups[3].Value;}
            else if(suffix.Success){number=int.Parse(suffix.Groups[2].Value);control=suffix.Groups[1].Value;}
            else if(Regex.IsMatch(name,@"^(Coin|Coin Chute|Credits?) ?[12]$",RegexOptions.IgnoreCase)){number=name[^1]-'0';control="Coin";}
            else if(name is "Coin" or "Coins" or "COINS" or "COIN" or "Credit" or "Coin Chute"){number=1;control="Coin";}
            else if(name is "Gun Trigger" or "Trigger" or "Start" or "Start Button" or "Reload" or "Action" or "Hyperenergy Tech Lever Forward (Attack)" or "Hyperenergy Tech Lever Backwards (Defense)"){number=1;control=name;}
            else
            {
                number=OverlayControls.TeknoPlayer(row.Element("InputMapping")?.Value??"",name);
                if(number is not (1 or 2))continue;
                control=name;
            }
            var player=active.SingleOrDefault(p=>p.Player==number);
            if(player is null)
            {
                // A gun moved to another slot must not keep driving its old player.
                Clear(row);
                continue;
            }
            var map=player.ButtonMap??GunSystems.DefaultMap(player.DevicePlayer,player.SystemId);
            bool aim=OverlayControls.TeknoAim(row);
            string? action=control switch {"Start" or "Start Button"=>"start","Coin" or "Credits"=>"coin","Gun Trigger" or "Gun Trigger 1" or "Trigger" or "Right Trigger" or "Trigger Left" or "Shoot" or "Vacuum Button" or "Touch/Click"=>"shoot","Reload" or "Start/Reload" or "Gun Trigger 2" or "Trigger Right" or "Pump Reload" or "Pump" or "Vacuum Lever" or "Reload/Guard"=>"reload","Rage" or "Grenade" or "Grenades" or "Action" or "Action Button" or "Gun Button" or "Special" or "Vacuum Screen Out"=>"secondary",_=>null};
            // Extermination uses the reload/right-mouse control for its flame weapon.
            if(control=="Flame" && document.Root?.Element("EmulationProfile")?.Value=="AliensExtermination") action="reload";
            string? token=aim?null:map.Where(p=>p.Value==action).Select(p=>p.Key).FirstOrDefault();
            // Use the same action projection as the other managed emulators.
            // Hardware identification and the independent exit chord stay physical.
            if(player.SystemId is "rs3" or "xgunner" or "blamcon" or "sinden" && action is not null)
            {
                string physical=action switch {"shoot"=>"trigger","reload"=>"reload","secondary"=>"magazine",_=>action};
                token=StartupControls.GameHardware(player).FirstOrDefault(h=>h.Id==physical)?.Token;
            }
            // These are distinct vendor actions, not aliases of grenades/action.
            // Keep weapon change and Option reachable on separate RS3 controls.
            if(control is "Footpedal" or "Foot Pedal" or "Pedal" &&
                (!string.IsNullOrWhiteSpace(row.Element("BindName")?.Value) || !string.IsNullOrWhiteSpace(row.Element("RawInputButton")?.Element("DevicePath")?.Value))) continue;
            bool extra=player.SystemId is "rs3" or "xgunner" or "blamcon" or "sinden" && control is "Change" or "Option" or "Sub Trigger" or "Controller Select" or "Option L" or "Option R" or "Footpedal" or "Foot Pedal" or "Pedal" or "Hyperenergy Tech Lever Forward (Attack)" or "Hyperenergy Tech Lever Backwards (Defense)";
            if(extra)
            {
                bool change=control is "Change" or "Controller Select" or "Option R" or "Hyperenergy Tech Lever Backwards (Defense)";
                var hardware=StartupControls.GameHardware(player);
                token=hardware.FirstOrDefault(h=>h.Id==(change?"stick":"side"))?.Token;
                // Vyper's selector can be reserved for hardware recoil. Its learned
                // D-pad Down remains available as a separate game weapon control.
                if(change && token is null) token=hardware.FirstOrDefault(h=>h.Id=="down")?.Token;
            }
            if(!aim&&(token is null||!GunSystems.ValidToken(token))) {if(action is not null||extra)Clear(row);continue;}
            bool keyboard=token?.StartsWith("key:")==true;
            string? device=keyboard?player.KeyboardId:player.MouseId;
            if(string.IsNullOrWhiteSpace(device)){if(aim||action is not null||extra)Clear(row);continue;}
            var input=row.Element("RawInputButton"); if(input is null){input=new XElement("RawInputButton");row.Add(input);}
            string mouse=aim||keyboard?"None":token switch {"mouse:1"=>"LeftButton","mouse:2"=>"RightButton","mouse:3"=>"MiddleButton","mouse:4"=>"Button4","mouse:5"=>"Button5",_=>"None"};
            string key=keyboard?KeyName(int.Parse(token![4..])):"None";if(key=="None"&&keyboard)continue;
            Set(input,"DevicePath",device);Set(input,"DeviceType",keyboard?"Keyboard":"Mouse");Set(input,"MouseButton",mouse);Set(input,"KeyboardKey",key);
            Set(row,"BindNameRi",$"Reaper P{player.Player} · {(aim?"Aim":keyboard?key:mouse)}");Set(row,"BindName",row.Element("BindNameRi")!.Value);
        }
        Set(api,"FieldValue","RawInput");
        if(changes==0)return 0;
        string backup=game.SourcePath+".before-reaper-input-fix";if(!File.Exists(backup))File.Copy(game.SourcePath,backup);
        string temporary=game.SourcePath+".reaper-new";document.Save(temporary);File.Move(temporary,game.SourcePath,true);
        return changes;
    }
    private static string KeyName(int key)=>key switch {>=48 and <=57=>"D"+(char)key,>=65 and <=90=>((char)key).ToString(),13=>"Return",27=>"Escape",32=>"Space",37=>"Left",38=>"Up",39=>"Right",40=>"Down",_=>"None"};
}
