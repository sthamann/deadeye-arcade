using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reaper.Core;

public record GameLaunchCheckInfo(string Title, string[] Notes);
public static class GameLaunchCheck
{
    public static GameLaunchCheckInfo? Read(GameEntry game,string data)
    {
        if(!Regex.IsMatch(game.Id,"^[a-zA-Z0-9_-]+$"))return null;
        string path=Path.Combine(data,"game-test-"+game.Id+".json");
        if(!File.Exists(path)||new FileInfo(path).Length>1_048_576)return null;
        try
        {
            using var document=JsonDocument.Parse(File.ReadAllText(path));var value=document.RootElement;
            if(!value.TryGetProperty("id",out var id)||id.GetString()!=game.Id)return null;
            bool Flag(string name)=>value.TryGetProperty(name,out var flag)&&flag.ValueKind==JsonValueKind.True;
            var notes=new List<string>();
            if(value.TryGetProperty("time",out var time)&&DateTimeOffset.TryParse(time.GetString(),out var at))notes.Add(I18n.T("Letzte Startprüfung: ")+at.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            string? error=value.TryGetProperty("error",out var problem)&&problem.ValueKind==JsonValueKind.String?problem.GetString():null;
            bool window=Flag("windowObserved")&&Flag("gameWindowAvailable"),ended=Flag("ended");
            bool leftovers=value.TryGetProperty("processesAfterExit",out var processes)&&processes.ValueKind==JsonValueKind.Array&&processes.GetArrayLength()>0;
            if(!string.IsNullOrWhiteSpace(error))notes.Add(error);
            else if(!window)notes.Add(I18n.T("Kein Spielfenster in der Beobachtungszeit erkannt."));
            else notes.Add(I18n.T("Spielfenster beobachtet. Das ist noch kein vollständiger Spieltest."));
            notes.Add(I18n.T(ended&&!leftovers?"Spielsitzung und zugehörige Prozesse sauber beendet.":"Das vollständige Beenden ist noch nicht bestätigt."));
            notes.Add(I18n.T("Treffer, Rückstoß und gleichzeitiges Spielen mit beiden Guns benötigen weiterhin einen Test am angeschlossenen Bildschirm."));
            return new(I18n.T(error is null&&window&&ended&&!leftovers?"Start und Beenden geprüft":"Startprüfung benötigt Aufmerksamkeit"),notes.ToArray());
        }
        catch(Exception error) when(error is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException){return null;}
    }
}
