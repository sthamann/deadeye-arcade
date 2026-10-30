namespace Reaper.Core;

// Emulators can keep their first window alive while creating a new renderer.
// Commit a changed handle only after it stays stable; never re-present it on
// every poll or restart a startup legend for an unchanged window.
public sealed class GameWindowTracker
{
    private long pending, since;
    public long Current { get; private set; }
    public bool Observe(long candidate,long now)
    {
        if(candidate==0 || candidate==Current){pending=0;return false;}
        if(candidate!=pending){pending=candidate;since=now;return false;}
        if(now-since<750)return false;
        Current=candidate;pending=0;return true;
    }
}
