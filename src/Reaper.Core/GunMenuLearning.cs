namespace Reaper.Core;

public sealed class GunMenuLearning
{
    public string[] Controls { get; }
    public int Index { get; private set; }
    private string? release;
    private readonly HashSet<string> captured = [];
    public bool Duplicate { get; private set; }
    public bool Complete => Index >= Controls.Length;
    public string? Current => Complete ? null : Controls[Index];
    public GunMenuLearning(string system) => Controls = system switch {
        "blamcon" => ["start","select","up","down","left","right"],
        "rs3" or "xgunner" => ["start","coin","up","down","left","right"],
        "sinden" => ["front-left","back-left","pump","back-right","front-right","up","down","left","right"],
        _ => throw new ArgumentException("Unknown lightgun system.")
    };
    public bool Accept(string token,bool down)
    {
        Duplicate = false;
        if(release is not null){if(release==token&&!down)release=null;return false;}
        if(!down||Complete||!GunSystems.ValidToken(token))return false;
        if(!captured.Add(token)){Duplicate=true;release=token;return false;}
        release=token;Index++;return true;
    }
}
