namespace Reaper.Core;

// Monotonic time and an edge latch: autorepeat never restarts the hold and a
// held combination can request only one exit until both buttons are released.
public sealed class EmergencyChord
{
    public const int HoldMilliseconds = 2000;
    private long? since;
    private bool consumed;
    public bool Sample(bool start, bool coin, long now)
    {
        if (consumed) { if (!start && !coin) consumed = false; return false; }
        if (!start || !coin) { since = null; return false; }
        since ??= now;
        if (now - since.Value < HoldMilliseconds) return false;
        consumed = true; since = null; return true;
    }
}

public static class DesktopAim
{
    public static (int X, int Y)? Position(int x, int y, int left, int top, int width, int height)
    {
        if (x < 0 || x > 65535 || y < 0 || y > 65535 || width <= 0 || height <= 0) return null;
        return (left + (int)Math.Round(x * (width - 1) / 65535d), top + (int)Math.Round(y * (height - 1) / 65535d));
    }
    public static GunBinding? Binding(IEnumerable<GunBinding> bindings, string deviceId)
    {
        var matches = bindings.Where(b => b.Player is 1 or 2 && b.MouseId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        // Ambiguous assignments must not produce two fake independent markers.
        return matches.Length == 1 ? matches[0] : null;
    }
}
