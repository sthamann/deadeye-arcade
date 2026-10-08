using System.Windows;
using System.Text.Json;
using Reaper.Core;

namespace Reaper.Windows;

public static class App
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--stop-desktop-guns") { DesktopGuns.Stop(); return; }
        if (args.Length == 1 && args[0] == "--desktop-guns")
        {
            using var companionMutex = new Mutex(true, "Local\\DeadeyeDesktopGuns-v1", out bool companionFirst);
            if (!companionFirst) return;
            DesktopGuns.Run(); return;
        }
        using var mutex = new Mutex(true, "Local\\ReaperArcade-v1", out bool first);
        if (!first) { Environment.ExitCode = 1; if (args.Length == 0) MessageBox.Show(I18n.T("Deadeye Arcade ist bereits geöffnet."), "Deadeye Arcade"); return; }
        if (args.Length > 0)
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReaperArcade");
            Directory.CreateDirectory(data);
            try
            {
                var store = new LibraryStore(data); var state = store.Load();
                if (args.Length == 1 && args[0] == "--inspect-display")
                {
                    DisplayDiagnostics.Capture(data);
                    return;
                }
                if (args.Length == 1 && args[0] == "--repair-library")
                {
                    AutomaticFixes.Run(state.Games, data).GetAwaiter().GetResult();
                    return;
                }
                if (args.Length == 1 && args[0] == "--inspect-gamepads")
                {
                    using var raw = new RawInput(); raw.Refresh();
                    File.WriteAllText(Path.Combine(data,"gamepads-report.json"), JsonSerializer.Serialize(new {
                        time=DateTimeOffset.Now, devices=DirectInputDevices.Scan(), guns=GunDiscovery.Scan(raw.Devices), bindings=state.Bindings
                    },JsonDefaults.Options));
                    return;
                }
                if(args.Length==3 && args[0]=="--test-game")
                {
                    int seconds=int.Parse(args[2]); if(seconds<5 || seconds>120) throw new ArgumentException("Test duration must be between 5 and 120 seconds.");
                    var game=state.Games.Single(g=>g.Id==args[1]);
                    var session=new GameSession(); bool windowObserved=false;string? error=null;
                    var elapsed=System.Diagnostics.Stopwatch.StartNew();
                    session.GameWindowReady+=_=>windowObserved=true;
                    Task run;
                    try {run=session.Run(game,data,state.Bindings);} catch(Exception e) {run=Task.FromException(e);}
                    Task.WhenAny(run,Task.Delay(seconds*1000)).GetAwaiter().GetResult();
                    bool exitedEarly=run.IsCompleted;
                    var before=session.LiveProcesses(); var processDetails=session.InspectProcesses(); bool foreground=session.HasGameForeground();
                    bool gameWindowAvailable=session.GameWindowAvailable;
                    double observedSeconds=elapsed.Elapsed.TotalSeconds;
                    session.End().GetAwaiter().GetResult();
                    try {run.GetAwaiter().GetResult();} catch(Exception e) {error=e.Message;}
                    if(error is null && (exitedEarly || before.Length==0)) error="The game exited before the observation period ended.";
                    var after=session.LiveProcesses();
                    using var controlDevices=new RawInput(); controlDevices.Refresh();
                    var controls=OverlayControls.Read(game,data,state.Bindings,controlDevices.Devices);
                    var playerControls=state.Bindings.Select(binding=>new {binding.Player,binding.SystemId,
                        hints=StartupControls.ForPlayer(controls,binding)}).ToArray();
                    File.WriteAllText(Path.Combine(data,"game-test-"+game.Id+".json"),JsonSerializer.Serialize(new {
                        time=DateTimeOffset.Now,game.Id,game.Title,seconds,observedSeconds,exitedEarly,windowObserved,gameWindowAvailable,foreground,error,
                        processDetails,processesBeforeExit=before,processesAfterExit=after,ended=!session.Active && after.Length==0,
                        controls,playerControls,game.Helpers,game.Players,
                        player1Verified=false,player2Verified=false,recoilVerified=false
                    },JsonDefaults.Options));
                    if(error is not null || !windowObserved || !gameWindowAvailable || after.Length>0) Environment.ExitCode=1;
                    return;
                }
                if (args.Length == 1 && args[0] == "--inspect-guns")
                {
                    using var inputs = new RawInput(); inputs.Refresh();
                    var guns = GunDiscovery.Scan(inputs.Devices);
                    var ids = new List<object>(); var serial = new GunSerial();
                    foreach (var gun in guns.Where(g => g.SystemId == "rs3" && g.Port is not null))
                        try { ids.Add(new { gun.Id, gun.Port, player = serial.Probe(gun.Port!).GetAwaiter().GetResult(), error = (string?)null }); }
                        catch (Exception error) { ids.Add(new { gun.Id, gun.Port, player = 0, error = error.Message }); }
                    File.WriteAllText(Path.Combine(data, "guns-report.json"), JsonSerializer.Serialize(new { time = DateTimeOffset.Now, guns, ids, inputs = inputs.Devices, bindings = state.Bindings }, JsonDefaults.Options));
                    return;
                }
                if (args.Length == 1 && args[0] == "--check-dependencies")
                {
                    File.WriteAllText(Path.Combine(data, "dependencies.json"), JsonSerializer.Serialize(RuntimeInstaller.Scan(state.Games), JsonDefaults.Options));
                    return;
                }
                if ((args.Length == 2 && args[0] == "--enrich-library") || (args.Length == 1 && args[0] == "--audit-media"))
                {
                    if (args[0] == "--enrich-library")
                    {
                        var manifest = JsonSerializer.Deserialize<EnrichmentManifest>(File.ReadAllText(args[1]), JsonDefaults.Options)
                            ?? throw new InvalidDataException("Invalid enrichment manifest");
                        LibraryEnrichment.Apply(state, manifest); store.Save(state);
                    }
                    File.WriteAllText(Path.Combine(data, "media-audit.json"), JsonSerializer.Serialize(LibraryEnrichment.Audit(state), JsonDefaults.Options));
                    return;
                }
                if (args.Length == 2 && args[0] == "--import-collection") LibraryStore.Merge(state, CollectionImporter.Read(args[1]).Games);
                else if (args.Length != 1 || args[0] != "--validate-library") throw new ArgumentException(I18n.T("Unbekannter Einrichtungsaufruf."));
                for (int i = 0; i < state.Games.Count; i++) { LaunchRules.RepairTeknoParrotPath(state.Games[i]); state.Games[i] = LaunchRules.Validate(state.Games[i]); }
                store.Save(state);
                File.WriteAllText(Path.Combine(data, "library-import-report.json"), JsonSerializer.Serialize(new
                {
                    time = DateTimeOffset.Now, games = state.Games.Count, available = state.Games.Count(g => g.Status != "needs-setup"),
                    rows = state.Games.Select(g => new { g.Id, g.Title, g.Platform, g.Players, g.Status, g.SetupIssues, g.SetupNotes, g.Executable, g.Arguments,
                        filesPresent = g.Status != "needs-setup", launchObserved = false, player1Verified = false, player2Verified = false, recoilVerified = false, returnVerified = false })
                }, JsonDefaults.Options));
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(data, "library-import-error.txt"), e.Message); Environment.ExitCode = 1; }
            return;
        }
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "Deadeye Arcade"); e.Handled = true; };
        try { app.Run(new ArcadeWindow()); }
        catch (Exception e) { MessageBox.Show(e.Message, I18n.T("Deadeye Arcade konnte nicht starten")); }
    }
}
