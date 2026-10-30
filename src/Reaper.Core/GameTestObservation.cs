namespace Reaper.Core;

// Preparation/loading is not part of the observation interval. A replaced or
// missing renderer must become stable again before the interval can pass.
public sealed class GameTestObservation(int seconds, int startupSeconds = 90)
{
    private long? readySince;
    public long? FirstReadyAt { get; private set; }
    public long ObservedMilliseconds { get; private set; }
    public string State { get; private set; } = "waiting-for-window";
    public bool Finished => State is "passed" or "startup-timeout" or "exited-early";
    public string Update(long elapsed, bool windowAvailable, bool exited)
    {
        if (Finished) return State;
        if (exited) return State = "exited-early";
        if (windowAvailable)
        {
            FirstReadyAt ??= elapsed;
            readySince ??= elapsed;
            ObservedMilliseconds = elapsed - readySince.Value;
            if (ObservedMilliseconds >= seconds * 1000L) return State = "passed";
            State = "observing";
        }
        else { readySince = null; ObservedMilliseconds = 0; State = "waiting-for-window"; }
        if (elapsed >= (startupSeconds + seconds) * 1000L || FirstReadyAt is null && elapsed >= startupSeconds * 1000L)
            State = "startup-timeout";
        return State;
    }
}
