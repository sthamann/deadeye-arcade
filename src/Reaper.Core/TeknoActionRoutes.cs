using System.Text.RegularExpressions;

namespace Reaper.Core;

public record TeknoActionRoute(string Profile,string Function,string InputMapping,string Control,string Source);

// Exact vendor profile/action identifiers keep game-specific defaults reviewable.
// Unknown actions retain their existing routes and remain pending in the audit.
public static class TeknoActionRoutes
{
    public static readonly TeknoActionRoute[] Rules = Build();
    private static TeknoActionRoute[] Build()
    {
        var rows=new List<TeknoActionRoute>();
        void Both(string profile,string function,string suffix,string control,string source)
        {foreach(int p in new[]{1,2})rows.Add(new(profile,function,"P"+p+suffix,control,source));}
        const string konami="https://github.com/argonlefou/DemulShooter/wiki/KONAMI-Arcade";
        const string island="https://github.com/argonlefou/DemulShooter/wiki/RingWide-%7C-Let%27s-go-Island-3D";
        Both("Castlevania","Whip Red","Button1","magazine",konami);
        Both("Castlevania","Whip Yellow","Button3","trigger",konami);
        foreach(string function in new[]{"Gun Left Trigger","Left Gun Trigger"})Both("SegaJvsLetsGoIsland",function,"Button1","trigger",island);
        foreach(string function in new[]{"Gun Right Trigger","Right Gun Trigger"})Both("SegaJvsLetsGoIsland",function,"Button2","reload",island);
        rows.Add(new("InfinityBlade","TOUCH 1","P1Button1","trigger","TeknoParrot installed InfinityBlade vendor template"));
        rows.Add(new("LuigisMansion","Select Switch","P1Button4","side","TeknoParrot installed LuigisMansion vendor template"));
        Both("WartranTroopers","Weapon Change","Button2","magazine","https://github.com/argonlefou/DemulShooter/wiki/Arcade-PC-%7C-Mars-Sortie");
        return rows.ToArray();
    }
    public static TeknoActionRoute? Find(string profile,string function,string inputMapping)
    {
        string name=Regex.Replace(Regex.Replace(function,@"^(?:Player [1-4] |P[1-4] )","",RegexOptions.IgnoreCase),@" P[1-4]$","",RegexOptions.IgnoreCase);
        return Rules.SingleOrDefault(r=>r.Profile==profile && r.Function==name && r.InputMapping==inputMapping);
    }
}
