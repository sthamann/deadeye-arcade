using Reaper.Core;

namespace Reaper.Windows;

// The frontend and launch checks must use the same connected devices and setup.
internal sealed class GameInputPreparation : IAsyncDisposable
{
    private readonly GunSerial serial = new();
    private readonly List<GunBinding> changed = [];
    private readonly List<(PhysicalGun Gun, BlamconSettings Settings)> blamcons = [];
    private readonly string data;
    public GunBinding[] Players { get; private set; } = [];
    public GameEntry Game { get; private set; } = null!;
    public DolphinGunBridge? Bridge { get; private set; }
    private GameInputPreparation(string data) => this.data = data;

    public static async Task<GameInputPreparation> Prepare(GameEntry game, string data, IEnumerable<GunBinding> bindings)
    {
        var setup = new GameInputPreparation(data);
        try
        {
            var before = KnownFixes.Snapshot(KnownFixes.InputFiles(game, data));
            using var raw = new RawInput(); raw.Refresh();
            setup.Players = GunConnections.ForConfiguration(bindings, raw.Devices, GunDiscovery.Scan(raw.Devices));
            if(game.Source=="mame")
            {
                if(MameControls.Metadata(game,data) is null)await MameMetadata.Prepare(game,data);
                MameControls.Configure(game,data,setup.Players);
            }
            bool indexAvailable = GunConnections.CanIndexMice(setup.Players, raw.Devices);
            setup.Game = indexAvailable ? RetroArchSetup.Configure(game, data, setup.Players, raw.MouseOrder) : game;
            setup.Game = FlycastSetup.ResolveContent(setup.Game,validateArchive:true);
            setup.Game = Rpcs3Setup.Configure(setup.Game, setup.Players);
            setup.Game = TimeCrisis5Controls.Configure(setup.Game, setup.Players);
            if (indexAvailable) SupermodelSetup.Configure(game, setup.Players, raw.Devices);
            else setup.Log("Physical USB guns are present but their RawInput mice are hidden in this session. Device-name assignments retained; mouse-index profiles require a local launch.");
            if (indexAvailable && FlycastSetup.IsFlycast(game))
                FlycastSetup.Configure(game, setup.Players, raw.Devices,
                    raw.Devices.ToDictionary(d => d.Id, d => InputDeviceNames.Name(d.Id) ?? (d.Kind == "mouse" ? "Mouse" : "Keyboard"), StringComparer.OrdinalIgnoreCase));
            if (DolphinSetup.IsDolphin(game) && setup.Players.Any(b => b.Player == 1 && DolphinSetup.Supports(b)))
            {
                string pack = await DolphinAccuracyPackage.Prepare(data);
                if(DolphinSetup.RequiresBridge(setup.Players)) setup.Bridge=new(setup.Players);
                if (!DolphinSetup.Configure(game, pack, setup.Players,setup.Bridge?.Port))
                {
                    setup.Log("No accuracy profile for this Dolphin disc region; existing controls retained.");
                    if(setup.Bridge is not null) {await setup.Bridge.DisposeAsync();setup.Bridge=null;}
                }
            }
            foreach (var binding in setup.Players.Where(b => b.SystemId == "rs3" && b.SerialPort is not null))
            {
                try
                {
                    await setup.serial.Command(binding.SerialPort!, binding.DevicePlayer,
                        GunSystems.ReaperConfiguration((binding.Feedback ?? new()) with { Aspect = game.Aspect }));
                    setup.changed.Add(binding);
                }
                catch(Exception error) when(error is IOException or UnauthorizedAccessException or TimeoutException)
                {
                    setup.Log($"P{binding.Player} serial settings unavailable; retaining current gun settings: {error.Message}");
                }
                // Keep mouse/keyboard mode until an independent joystick profile is
                // actually installed. Switching an unconfigured P2 loses its input.
            }
            foreach (var binding in setup.Players.Where(b => b.SystemId == "blamcon" && b.SerialPort is not null))
            {
                var gun = GunDiscovery.Scan(raw.Devices).Single(g => GunAssignments.Matches(binding,g));
                setup.blamcons.Add((gun,binding.Blamcon??new()));
                try { await BlamconSerial.Send(gun,BlamconSetup.Configure(GameGunProfiles.Vyper(game,binding))); }
                catch(Exception error) when(error is IOException or UnauthorizedAccessException or TimeoutException)
                { if(GameGunProfiles.FixedMouseReload(game)) throw new IOException(I18n.T("Vyper-Magazin für dieses Spiel nicht eingerichtet: ")+error.Message,error); setup.Log($"P{binding.Player} Blamcon settings unavailable: {error.Message}"); }
            }
            KnownFixes.RecordChanges(game, data, DolphinSetup.IsDolphin(game) ? "dolphin-accuracy" : "usb-identity", before, KnownFixes.InputFiles(game, data));
            return setup;
        }
        catch { await setup.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if(Bridge is not null)
        {
            Log("Dolphin bridge: "+System.Text.Json.JsonSerializer.Serialize(Bridge.Snapshot()));
            await Bridge.DisposeAsync();Bridge=null;
        }
        foreach (var binding in changed)
            try { await serial.Command(binding.SerialPort!, binding.DevicePlayer, GunSystems.ReaperConfiguration(binding.Feedback ?? new())); }
            catch (Exception error) { Log($"Menu input restore failed for P{binding.Player}: {error.Message}"); }
        changed.Clear();
        foreach(var (gun,settings) in blamcons)
            try { await BlamconSerial.Send(gun,BlamconSetup.Configure(settings)); }
            catch(Exception error) { Log("Blamcon menu settings restore failed: "+error.Message); }
        blamcons.Clear();
    }
    internal static async Task<string[]> RestoreMenu(IEnumerable<GunBinding> bindings)
    {
        var issues = new List<string>();
        using var raw = new RawInput(); raw.Refresh();
        var guns = GunDiscovery.Scan(raw.Devices);
        foreach (var binding in GunConnections.ForConfiguration(bindings, raw.Devices, guns).Where(b => b.SerialPort is not null))
            try
            {
                if (binding.SystemId == "rs3") await new GunSerial().Command(binding.SerialPort!, binding.DevicePlayer, GunSystems.ReaperConfiguration(binding.Feedback ?? new()));
                else if (binding.SystemId == "blamcon")
                {
                    var gun = guns.Single(g => GunAssignments.Matches(binding, g));
                    await BlamconSerial.Send(gun, BlamconSetup.Configure(binding.Blamcon ?? new()));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException or InvalidOperationException)
            { issues.Add($"P{binding.Player} menu input restore failed: {e.Message}"); }
        return issues.ToArray();
    }
    private void Log(string message)
    {
        Directory.CreateDirectory(data);
        File.AppendAllText(Path.Combine(data, "game-input.log"), $"{DateTimeOffset.Now:o} {message}{Environment.NewLine}");
    }
}
