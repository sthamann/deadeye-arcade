namespace Reaper.Core;

// A game slot is independent of the receiver/DIP number. Keep a profile for
// every gun, including guns which are currently sitting out.
public static class GunAssignments
{
    public static bool Matches(GunBinding binding, PhysicalGun gun) =>
        binding.PhysicalId == gun.Id || string.Equals(binding.MouseId, gun.MouseId, StringComparison.OrdinalIgnoreCase);

    public static GunBinding Profile(LibraryState state, PhysicalGun gun, int player)
    {
        if (gun.MouseId is null || !gun.DriverHealthy) throw new InvalidOperationException(I18n.T("Keine nutzbare Maus-Schnittstelle der Gun gefunden."));
        var saved = state.Bindings.Concat(state.GunProfiles).FirstOrDefault(b => Matches(b, gun) && b.SystemId == gun.SystemId);
        int hardware = GunSystems.HardwarePlayer(gun.SystemId, gun.MouseId, saved?.DevicePlayer ?? player);
        var blamconPlayer = System.Text.RegularExpressions.Regex.Match(gun.Name, @"\bBlamcon\s*Lightgun\s*-\s*P([1-4])\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (gun.SystemId == "blamcon" && blamconPlayer.Success) hardware = int.Parse(blamconPlayer.Groups[1].Value);
        return saved is null ? new(player, gun.MouseId, gun.KeyboardId, gun.Port, gun.SystemId, gun.Id,
            GunSystems.DefaultMap(hardware, gun.SystemId), SoftwareConfigured: gun.SystemId == "xgunner" && gun.KeyboardId is not null, HardwarePlayer: hardware)
            : saved with { Player = player, MouseId = gun.MouseId, KeyboardId = gun.KeyboardId, SerialPort = gun.Port, PhysicalId = gun.Id, HardwarePlayer = hardware };
    }

    public static LibraryState Remember(LibraryState state, GunBinding binding)
    {
        var profiles = state.GunProfiles.Where(b => b.PhysicalId != binding.PhysicalId || binding.PhysicalId is null)
            .Where(b => !b.MouseId.Equals(binding.MouseId, StringComparison.OrdinalIgnoreCase)).ToList();
        profiles.Add(binding);
        return state with { GunProfiles = profiles };
    }

    public static LibraryState Assign(LibraryState state, PhysicalGun gun, int player)
    {
        if (player is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(player));
        var selected = Profile(state, gun, player);
        foreach (var binding in state.Bindings) state = Remember(state, binding);
        var oldSlot = state.Bindings.FirstOrDefault(b => Matches(b, gun));
        var displaced = state.Bindings.FirstOrDefault(b => b.Player == player && !Matches(b, gun));
        var active = state.Bindings.Where(b => b.Player != player && !Matches(b, gun)).ToList();
        if (oldSlot is not null && oldSlot.Player != player && displaced is not null)
            active.Add(displaced with { Player = oldSlot.Player, HardwarePlayer = displaced.DevicePlayer });
        active.Add(selected);
        state = state with { Bindings = active, Settings = state.Settings with { ManualGunAssignments = true } };
        return Remember(state, selected);
    }
}
