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
