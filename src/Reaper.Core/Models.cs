using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reaper.Core;

public record GameEntry(string Id, string Title, string Platform, string Executable, string[] Arguments,
    string WorkingDirectory, string Source, string SourcePath, string Status = "unverified", string? Cover = null,
    bool Favorite = false, string Aspect = "16:9", DateTimeOffset? LastPlayed = null);
public record GunBinding(int Player, string MouseId, string? KeyboardId = null, string? SerialPort = null);
public record InputDevice(string Id, string Name, string Kind, bool RetroShooter);
public record AppSettings(bool StartWithWindows = false, bool Fullscreen = true, string? CoverKey = null);
public record LibraryState(List<GameEntry> Games, List<GunBinding> Bindings, AppSettings Settings)
{
    public static LibraryState Empty => new([], [], new());
}
public record ImportResult(List<GameEntry> Games, List<string> Warnings);

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
public static class Identity
{
    public static string For(string platform, string path) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(platform + "|" + Path.GetFullPath(path).ToUpperInvariant())))[..20].ToLowerInvariant();
}
public sealed class LibraryStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    private string FilePath => Path.Combine(DirectoryPath, "library.json");
    public LibraryState Load()
    {
        if (!File.Exists(FilePath)) return LibraryState.Empty;
        // A damaged library must never silently become an empty library and overwrite the user's data.
        return JsonSerializer.Deserialize<LibraryState>(File.ReadAllText(FilePath), JsonDefaults.Options)
               ?? throw new InvalidDataException("Die gespeicherte Bibliothek ist ungültig.");
    }
    public void Save(LibraryState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".new";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonDefaults.Options));
        if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
        File.Move(temporary, FilePath, true);
    }
    public static void Merge(LibraryState state, IEnumerable<GameEntry> incoming)
    {
        foreach (var game in incoming)
        {
            int existing = state.Games.FindIndex(g => g.Id == game.Id);
            if (existing < 0) state.Games.Add(game);
            else
            {
                var old = state.Games[existing];
                bool sameLaunch = old.Executable == game.Executable && old.Arguments.SequenceEqual(game.Arguments);
                state.Games[existing] = game with
                {
                    Favorite = old.Favorite,
                    Cover = old.Cover ?? game.Cover,
                    LastPlayed = old.LastPlayed,
                    Aspect = old.Aspect,
                    Status = sameLaunch ? old.Status : "unverified"
                };
            }
        }
    }
}
