namespace Reaper.Core;

public static class GunConnections
{
    // RDP can hide a gun's RawInput mouse while its exact USB interface remains
    // present. That is enough to retain a device-name profile, not a mouse index.
    public static GunBinding[] ForConfiguration(IEnumerable<GunBinding> bindings,
        IReadOnlyList<InputDevice> inputs, IEnumerable<PhysicalGun> physical)
    {
        var present = physical.ToArray();
        return bindings.Where(b => b.Player is 1 or 2 && !string.IsNullOrWhiteSpace(b.MouseId) &&
            (inputs.Any(d => d.Kind == "mouse" && d.Id.Equals(b.MouseId, StringComparison.OrdinalIgnoreCase)) ||
             present.Any(g => g.DriverHealthy && g.SystemId == b.SystemId &&
                 string.Equals(g.MouseId, b.MouseId, StringComparison.OrdinalIgnoreCase) &&
                 g.InputIds.Contains(b.MouseId, StringComparer.OrdinalIgnoreCase)))).ToArray();
    }

    public static bool CanIndexMice(IEnumerable<GunBinding> bindings, IReadOnlyList<InputDevice> inputs) =>
        bindings.All(b => inputs.Any(d => d.Kind == "mouse" && d.Id.Equals(b.MouseId, StringComparison.OrdinalIgnoreCase)));
}
