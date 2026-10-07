namespace Reaper.Core;

// Manufacturer's Windows calibration protocol. These commands request calibration;
// the vendor transport provides no acknowledgement of saved accuracy.
public sealed class ReaperCalibration
{
    public static readonly (double X, double Y)[] Targets = [(0.5,0.5),(0.1,0.1),(0.9,0.9),(0.9,0.1)];
    public int Step { get; private set; } = -1;
    public bool Saved => Step == 4;
    private bool held;
    public byte? Trigger(bool down)
    {
        if (!down) { held = false; return null; }
        if (held || Saved) return null;
        held = true;
        Step++;
        return Step switch { 0 => 0xB0, 1 => 0xB2, 2 => 0xB4, 3 => 0xB6, 4 => 0xB8, _ => null };
    }
    public byte? Cancel() => Saved ? null : (byte)0xBA;
}
