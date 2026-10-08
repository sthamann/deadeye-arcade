namespace Reaper.Core;

// Raw mouse button packets need not repeat MOUSE_MOVE_ABSOLUTE. A shared OS
// cursor cannot tell us which of two guns fired; retain aim by full device ID.
public sealed class GunAim
{
    public readonly record struct Position(int X, int Y, bool VirtualDesktop);
    private readonly Dictionary<string, Position> positions = new(StringComparer.OrdinalIgnoreCase);
    public void Remember(string device, bool absolute, int x, int y, bool virtualDesktop)
    {
        if (absolute) positions[device] = new(Math.Clamp(x, 0, 65535), Math.Clamp(y, 0, 65535), virtualDesktop);
    }
    public bool TryGet(string device, out Position position) => positions.TryGetValue(device, out position);
    public void RetainDevices(HashSet<string> connected)
    {
        foreach (var device in positions.Keys.Where(id => !connected.Contains(id)).ToArray()) positions.Remove(device);
    }
}
