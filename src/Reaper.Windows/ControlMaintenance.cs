using Reaper.Core;
namespace Reaper.Windows;

internal static class ControlMaintenance
{
    public static async Task<ControlAuditReport> Run(IEnumerable<GameEntry> entries,string data)
    {
        var games=entries.ToArray();var library=new LibraryStore(data).Load();
        using var raw=new RawInput();raw.Refresh();
        var players=GunConnections.ForConfiguration(library.Bindings,raw.Devices,GunDiscovery.Scan(raw.Devices));
        string? pack=null;
        if(games.Any(DolphinSetup.IsDolphin) && players.Any(p=>p.Player==1 && p.SystemId=="rs3"))
            try {pack=await DolphinAccuracyPackage.Prepare(data);}
            catch(Exception e) when(e is IOException or HttpRequestException or TaskCanceledException) {
                File.AppendAllText(Path.Combine(data,"game-input.log"),$"{DateTimeOffset.Now:o} Accuracy package unavailable: {e.Message}\n");
            }
        foreach(var game in games)
        {
            try
            {
                var files=KnownFixes.InputFiles(game,data);var before=KnownFixes.Snapshot(files);
                if(game.Source=="teknoparrot") TeknoGunSetup.Configure(game,players);
                if(pack is not null && DolphinSetup.IsDolphin(game))
                {
                    // No controller is started during a library audit. The launch
                    // path replaces this port with the running bridge's real port.
                    DolphinSetup.Configure(game,pack,players,players.Any(p=>p.Player==2 && p.SystemId=="rs3")?26761:null);
                }
                KnownFixes.RecordChanges(game,data,"control-completeness",before,KnownFixes.InputFiles(game,data));
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException) {
                File.AppendAllText(Path.Combine(data,"game-input.log"),$"{DateTimeOffset.Now:o} Control repair for {game.Title}: {e.Message}\n");
            }
        }
        return GameControlAudit.Save(games,data,library.Bindings,raw.Devices,merge:true);
    }
}
