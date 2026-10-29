namespace Reaper.Core;

public static class GameGunProfiles
{
    // These installed plugins consume fixed mouse buttons, not Studio key remaps.
    // Vyper's magazine uses the pedal channel, which can emit right mouse instead.
    public static bool FixedMouseReload(GameEntry game) =>
        game.Title.Contains("Blue Estate",StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(game.WorkingDirectory,"BlueEstate_Fix.dll"))
        || MultiplayerSetup.SupportsHouseDead2Remake(game);
    public static GunBinding ForGame(GameEntry game,GunBinding binding)
    {
        if(binding.SystemId!="blamcon" || !FixedMouseReload(game))return binding;
        return binding with {Blamcon=Vyper(game,binding),ControlMap=new Dictionary<string,string>(binding.ControlMap??[]){["magazine"]="mouse:2",["pedal"]="mouse:2"}};
    }
    public static BlamconSettings Vyper(GameEntry game,GunBinding binding) =>
        (binding.Blamcon ?? new()) with {Aspect=game.Aspect,PedalMode=FixedMouseReload(game)?"right":(binding.Blamcon??new()).PedalMode};
}
