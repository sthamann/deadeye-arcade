using System.Numerics;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reaper.Core;

public record SimulatedAction(int Player,string Function,string Expression,string Scope,string Status,string[] Buttons,bool? Pressed,bool? Held,bool? Released,bool Isolated,string? Error=null);
public record SimulatedGame(string GameId,string Title,string Status,GameControlReview Audit,SimulatedAction[] Actions,string? Error=null);
public record ControlSimulationReport(DateTimeOffset Time,SimulatedGame[] Games,GunBinding[] TestedAssignments,bool HardwareVerified=false,bool GameplayVerified=false);

// Replay physical down/hold/up events. DSU profiles use the production bridge's
// actual wire frames; other profiles are expression checks, not game injection.
public static class ControlSimulation
{
    public static async Task<ControlSimulationReport> Run(IEnumerable<GameEntry> games,string data,IEnumerable<GunBinding> bindings,IEnumerable<InputDevice>? devices=null)
    {
        var guns=bindings.Where(b=>b.Player is 1 or 2).ToArray();var results=new List<SimulatedGame>();
        foreach(var game in games)
        {
            try
            {
                var audit=GameControlAudit.Read(game,data,guns,devices);
                var rows=OverlayControls.Read(game,data,guns,devices).Rows.Where(r=>!r.IsAim && GameControlAudit.GameplayAction(r.Function) && audit.Players.Any(p=>p.Player==r.Player)).ToList();
                if(DolphinSetup.IsDolphin(game))
                {
                    rows.Clear();string? id=DolphinSetup.DiscId(game);
                    foreach(var gun in guns.Where(g=>audit.Players.Any(p=>p.Player==g.Player)))
                    {
                        string? path=DolphinSetup.ProfilePath(game,gun.Player);
                        string text=path is not null && File.Exists(path)?File.ReadAllText(path):"";
                        string device=DolphinSetup.Value(text,"Profile","Device")??"";
                        foreach(string key in id is null?[]:GameControlAudit.RequiredDolphin(id))
                            rows.Add(new(gun.Player,key,"",device=="DSUClient/"+(gun.Player-1)+"/"+DolphinGunBridge.DeviceName?"Dolphin DSU replay":"Unsupported Dolphin device",DolphinSetup.Value(text,"Profile",key)));
                    }
                }
                var actions=await Replay(rows,guns,devices);
                string status=audit.Status=="needs-action" || actions.Any(a=>a.Status=="failed")?"needs-action"
                    : audit.Status!="mapping-checked" || actions.Length==0 || actions.Any(a=>a.Status is not ("simulated" or "profile-routed"))?"needs-review"
                    : actions.All(a=>a.Status=="simulated")?"simulation-passed":"profile-routed";
                results.Add(new(game.Id,game.Title,status,audit,actions));
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException)
            {results.Add(new(game.Id,game.Title,"needs-action",new(game.Id,game.Title,game.Source,"needs-action",[],e.Message),[],e.Message));}
        }
        return new(DateTimeOffset.UtcNow,results.ToArray(),guns);
    }
    public static async Task<SimulatedAction[]> Replay(IEnumerable<ControlRow> rows,IEnumerable<GunBinding> bindings,IEnumerable<InputDevice>? devices=null)
    {
        var guns=bindings.Where(b=>b.Player is 1 or 2).ToArray();var result=new List<SimulatedAction>();
        var connected=devices?.ToArray();
        if(guns.GroupBy(g=>g.Player).Any(g=>g.Count()>1))
            return rows.Select(r=>new SimulatedAction(r.Player,r.Function,r.Expression??"","unavailable","failed",[],false,false,false,false,"Multiple guns are assigned to the same player slot.")).ToArray();
        await using var bridge=new DolphinGunBridge(guns);
        var elapsed=Stopwatch.StartNew();
        foreach(var row in rows)
        {
            var gun=guns.SingleOrDefault(b=>b.Player==row.Player);
            bool dsu=row.Evidence=="Dolphin DSU replay";
            string scope=dsu?"production-dsu-frames":"profile-expression";
            try
            {
                void CheckDeadline() {if(elapsed.Elapsed>TimeSpan.FromSeconds(20))throw new NotSupportedException("Simulation time limit reached; this action remains unverified.");}
                CheckDeadline();
                if(gun is null || string.IsNullOrWhiteSpace(row.Expression))throw new ArgumentException("Missing player or input expression.");
                if(row.Evidence=="Unsupported Dolphin device")throw new NotSupportedException("This profile does not consume the production DSU bridge.");
                var expression=new Expression(row.Expression);
                GunControl[] Physical(GunBinding b)=>StartupControls.Hardware(b).Where(h=>h.Token is not null && GunSystems.ValidToken(h.Token)
                    && (h.Token.StartsWith("mouse:")?!string.IsNullOrWhiteSpace(b.MouseId):!string.IsNullOrWhiteSpace(b.KeyboardId))
                    && (connected is null || connected.Any(d=>d.Kind==(h.Token.StartsWith("mouse:")?"mouse":"keyboard") && d.Id.Equals(h.Token.StartsWith("mouse:")?b.MouseId:b.KeyboardId,StringComparison.OrdinalIgnoreCase)))).DistinctBy(h=>h.Token).ToArray();
                var hardware=Physical(gun);
                if(hardware.Length>16)throw new NotSupportedException("More than sixteen distinct physical controls.");
                bool Evaluate(GunBinding source,ISet<string> down)
                {
                    byte[]? frame=dsu?bridge.Frame(gun.Player):null;
                    return expression.Evaluate(atom=>frame is not null?Channel(frame,atom):ProfileAtom(atom,row,gun,source,down));
                }
                void Send(GunBinding source,IEnumerable<string> tokens,bool down)
                {
                    foreach(string token in tokens)
                        if(token.StartsWith("mouse:"))bridge.Mouse(source.MouseId,false,0,0,1<<(2*(int.Parse(token[6..])-1)+(down?0:1)));
                        else if(source.KeyboardId is not null)bridge.Key(source.KeyboardId,int.Parse(token[4..]),down);
                }
                string[]? found=null;bool pressed=false,held=false,released=false,isolated=true;
                var empty=new HashSet<string>();bridge.ResetInput();
                if(Evaluate(gun,empty))throw new ArgumentException("The action is already active with every button released.");
                foreach(int mask in Enumerable.Range(1,(1<<hardware.Length)-1).OrderBy(m=>BitOperations.PopCount((uint)m)))
                {
                    CheckDeadline();
                    var tokens=hardware.Where((_,i)=>(mask&(1<<i))!=0).Select(h=>h.Token!).ToHashSet();
                    bridge.ResetInput();Send(gun,tokens,true);
                    if(!Evaluate(gun,tokens))continue;
                    found=hardware.Where((_,i)=>(mask&(1<<i))!=0).Select(h=>h.Id).ToArray();pressed=true;
                    Send(gun,tokens,true);held=Evaluate(gun,tokens);
                    Send(gun,tokens,false);released=!Evaluate(gun,empty);
                    foreach(var other in guns.Where(b=>b.Player!=gun.Player))
                    {
                        var otherHardware=Physical(other);
                        if(otherHardware.Length>16)throw new NotSupportedException("More than sixteen distinct physical controls.");
                        // Check all other-gun combinations, including chords with
                        // negated buttons. Inventing a key absent on that gun would
                        // produce a false cross-player collision.
                        foreach(int otherMask in Enumerable.Range(1,(1<<otherHardware.Length)-1))
                        {
                            CheckDeadline();
                            var otherTokens=otherHardware.Where((_,i)=>(otherMask&(1<<i))!=0).Select(h=>h.Token!).ToHashSet();
                            bridge.ResetInput();Send(other,otherTokens,true);
                            if(Evaluate(other,otherTokens)){isolated=false;break;}
                        }
                    }
                    break;
                }
                bool pass=pressed&&held&&released&&isolated;
                result.Add(new(row.Player,row.Function,row.Expression,scope,pass?dsu?"simulated":"profile-routed":"failed",found??[],dsu?pressed:null,dsu?held:null,dsu?released:null,isolated,pass?null:!pressed?"No physical button combination activates this action.":!isolated?"The other player's inputs activate this action.":"Hold or release did not match the profile."));
            }
            catch(NotSupportedException e) {result.Add(new(row.Player,row.Function,row.Expression??"",scope,"unsupported",[],false,false,false,false,e.Message));}
            catch(ArgumentException e) {result.Add(new(row.Player,row.Function,row.Expression??"",scope,"failed",[],false,false,false,false,e.Message));}
            finally {bridge.ResetInput();}
        }
        return result.ToArray();
    }
    private static bool ProfileAtom(string atom,ControlRow row,GunBinding target,GunBinding source,ISet<string> down)
    {
        var scoped=Regex.Match(atom,@"^P([12])\|(mouse:[1-5]|key:[0-9]+)$");
        if(scoped.Success)
        {
            bool globalKeyboard=row.Evidence=="RetroArch-Konfiguration" && scoped.Groups[2].Value.StartsWith("key:");
            string token=scoped.Groups[2].Value;
            bool sameDevice=token.StartsWith("mouse:")?source.MouseId.Equals(target.MouseId,StringComparison.OrdinalIgnoreCase)
                :source.KeyboardId?.Equals(target.KeyboardId,StringComparison.OrdinalIgnoreCase)==true;
            return int.Parse(scoped.Groups[1].Value)==target.Player && (globalKeyboard || sameDevice) && down.Contains(token);
        }
        bool DeviceMatches(string token)=>token.StartsWith("mouse:")?source.MouseId.Equals(target.MouseId,StringComparison.OrdinalIgnoreCase):source.KeyboardId?.Equals(target.KeyboardId,StringComparison.OrdinalIgnoreCase)==true;
        if(GunSystems.ValidToken(atom))return DeviceMatches(atom) && down.Contains(atom);
        var mouse=Regex.Match(atom,@"^GUNCODE_([12])_BUTTON([1-5])$");
        if(mouse.Success)return int.Parse(mouse.Groups[1].Value)==target.Player && DeviceMatches("mouse:1") && down.Contains("mouse:"+mouse.Groups[2].Value);
        var key=Regex.Match(atom,@"^KEYCODE_(?:([12])_)?([A-Z0-9]+)$");
        if(key.Success)return (!key.Groups[1].Success || int.Parse(key.Groups[1].Value)==target.Player && DeviceMatches("key:1")) && StartupControls.KeyToken(key.Groups[2].Value) is {} token && down.Contains(token);
        throw new NotSupportedException("Unsupported profile atom: "+atom);
    }
    private static bool Channel(byte[] frame,string atom) => atom switch
    {
        "Share"=>(frame[36]&1)!=0,"R3"=>(frame[36]&4)!=0,"Options"=>(frame[36]&8)!=0,
        "Pad W"=>frame[44]!=0,"Pad S"=>frame[45]!=0,"Pad E"=>frame[46]!=0,"Pad N"=>frame[47]!=0,
        "Square"=>frame[48]!=0,"Cross"=>frame[49]!=0,"Circle"=>frame[50]!=0,"L2"=>frame[55]!=0,
        _=>throw new NotSupportedException("Unsupported DSU channel: "+atom)
    };
    // Strict boolean subset emitted by our profiles. Unknown syntax cannot pass.
    private sealed class Expression
    {
        private readonly Func<Func<string,bool>,bool> root;
        private readonly string[] tokens;private int position;
        public Expression(string text)
        {
            const string pattern=@"`[^`]+`|P[12]\|(?:mouse:[1-5]|key:[0-9]+)|(?:mouse:[1-5]|key:[0-9]+)|(?:GUNCODE|KEYCODE)_[A-Z0-9_]+|\bOR\b|[!&|()]";
            if(Regex.Replace(Regex.Replace(text,pattern,""),@"\s","")!="")throw new NotSupportedException("Unsupported input expression: "+text);
            tokens=Regex.Matches(text,pattern).Select(m=>m.Value).ToArray();root=Or();
            if(position!=tokens.Length)throw new NotSupportedException("Unexpected expression suffix.");
        }
        public bool Evaluate(Func<string,bool> input)=>root(input);
        private bool Take(string token) {if(position<tokens.Length && tokens[position]==token){position++;return true;}return false;}
        private Func<Func<string,bool>,bool> Or()
        {var node=And();while(Take("|")||Take("OR")){var left=node;var right=And();node=f=>left(f)|right(f);}return node;}
        private Func<Func<string,bool>,bool> And()
        {var node=Unary();while(Take("&")){var left=node;var right=Unary();node=f=>left(f)&right(f);}return node;}
        private Func<Func<string,bool>,bool> Unary()
        {
            if(Take("!")){var inner=Unary();return f=>!inner(f);}
            if(Take("(")){var inner=Or();if(!Take(")"))throw new NotSupportedException("Unclosed expression.");return inner;}
            if(position>=tokens.Length || tokens[position] is ")" or "&" or "|" or "OR")throw new NotSupportedException("Missing expression operand.");
            string atom=tokens[position++].Trim('`');return f=>f(atom);
        }
    }
    public static void Save(ControlSimulationReport report,string data)
    {
        string path=Path.Combine(data,"controls-simulation.json");
        File.WriteAllText(path+".new",JsonSerializer.Serialize(report,JsonDefaults.Options));File.Move(path+".new",path,true);
    }
}
