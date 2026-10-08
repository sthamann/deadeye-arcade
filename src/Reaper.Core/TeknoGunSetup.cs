using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace Reaper.Core;
public static class TeknoGunSetup
{
    // Only controls explicitly named by the selected profile are changed.
    // Special controls such as pedals, weapon selectors or two-stage triggers retain their vendor bindings.
    public static int Configure(GameEntry game,IEnumerable<GunBinding> players)
    {
        if(game.Source!="teknoparrot"||!File.Exists(game.SourcePath)) return 0;
        var document=XDocument.Load(game.SourcePath);
        var api=document.Descendants("FieldInformation").FirstOrDefault(f=>f.Element("FieldName")?.Value=="Input API");
        if(api is null||!api.Descendants("string").Any(x=>x.Value=="RawInput"))return 0;
        var changes=0;
        void Set(XElement node,string name,string value){var child=node.Element(name);if(child?.Value==value)return;if(child is null)node.Add(new XElement(name,value));else child.Value=value;changes++;}
        foreach(var player in players.Where(p=>p.Player is 1 or 2))
        {
            var map=player.ButtonMap??GunSystems.DefaultMap(player.Player,player.SystemId);
            foreach(var row in document.Descendants("JoystickButtons"))
            {
                var name=row.Element("ButtonName")?.Value??"";
                var prefix=Regex.Match(name,@"^(?:Player ([12]) |P([12]) )(.+)$",RegexOptions.IgnoreCase);
                var suffix=Regex.Match(name,@"^(.+) P([12])$",RegexOptions.IgnoreCase);
                int number;string control;
                if(prefix.Success){number=int.Parse(prefix.Groups[1].Success?prefix.Groups[1].Value:prefix.Groups[2].Value);control=prefix.Groups[3].Value;}
                else if(suffix.Success){number=int.Parse(suffix.Groups[2].Value);control=suffix.Groups[1].Value;}
                else if(Regex.IsMatch(name,@"^(Coin|Coin Chute|Credits?) ?[12]$",RegexOptions.IgnoreCase)){number=name[^1]-'0';control="Coin";}
                else if(name is "Coin" or "Coins" or "COINS" or "COIN" or "Credit" or "Coin Chute"){number=1;control="Coin";}
                else if(name is "Gun Trigger" or "Trigger" or "Start" or "Start Button" or "Reload"){number=1;control=name;}
                else continue;
                if(number!=player.Player)continue;
                bool aim=control is "Light Gun" or "Gun" or "Spray Controller" or "Proton Pack" or "Touch" or "Light Whip";
                string? action=control switch {"Start" or "Start Button"=>"start","Coin" or "Credits"=>"coin","Gun Trigger" or "Trigger" or "Trigger Left" or "Shoot" or "Vacuum Button" or "Touch/Click"=>"shoot","Reload" or "Trigger Right" or "Pump Reload" or "Pump"=>"reload","Grenade" or "Grenades" or "Action" or "Action Button" or "Gun Button" or "Special"=>"secondary",_=>null};
                // Extermination uses the reload/right-mouse control for its flame weapon.
                if(control=="Flame" && document.Root?.Element("EmulationProfile")?.Value=="AliensExtermination") action="reload";
                string? token=aim?null:map.Where(p=>p.Value==action).Select(p=>p.Key).FirstOrDefault();
                if(!aim&&(token is null||!GunSystems.ValidToken(token)))continue;
                bool keyboard=token?.StartsWith("key:")==true;
                string? device=keyboard?player.KeyboardId:player.MouseId;
                if(string.IsNullOrWhiteSpace(device))continue;
                var input=row.Element("RawInputButton"); if(input is null){input=new XElement("RawInputButton");row.Add(input);}
                string mouse=aim||keyboard?"None":token switch {"mouse:1"=>"LeftButton","mouse:2"=>"RightButton","mouse:3"=>"MiddleButton","mouse:4"=>"Button4","mouse:5"=>"Button5",_=>"None"};
                string key=keyboard?KeyName(int.Parse(token![4..])):"None";if(key=="None"&&keyboard)continue;
                Set(input,"DevicePath",device);Set(input,"DeviceType",keyboard?"Keyboard":"Mouse");Set(input,"MouseButton",mouse);Set(input,"KeyboardKey",key);
                Set(row,"BindNameRi",$"Reaper P{player.Player} · {(aim?"Aim":keyboard?key:mouse)}");Set(row,"BindName",row.Element("BindNameRi")!.Value);
            }
        }
        if(changes==0)return 0;
        Set(api,"FieldValue","RawInput");
        string backup=game.SourcePath+".before-reaper-input-fix";if(!File.Exists(backup))File.Copy(game.SourcePath,backup);
        string temporary=game.SourcePath+".reaper-new";document.Save(temporary);File.Move(temporary,game.SourcePath,true);
        return changes;
    }
    private static string KeyName(int key)=>key switch {>=48 and <=57=>"D"+(char)key,>=65 and <=90=>((char)key).ToString(),13=>"Return",27=>"Escape",32=>"Space",37=>"Left",38=>"Up",39=>"Right",40=>"Down",_=>"None"};
}
