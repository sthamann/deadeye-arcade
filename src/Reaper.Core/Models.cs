using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Reaper.Core;

public record GameEntry(string Id, string Title, string Platform, string Executable, string[] Arguments,
    string WorkingDirectory, string Source, string SourcePath, string Status = "unverified", string? Cover = null,
    bool Favorite = false, string Aspect = "16:9", DateTimeOffset? LastPlayed = null,
    string? PreviewVideo = null, string? Screenshot = null, string? Logo = null,
    string[]? RequiredFiles = null, string[]? SetupIssues = null, int Priority = 0,
    string? Players = null, string? SetupNotes = null, HelperLaunch[]? Helpers = null,
    string? Description = null, int? ReleaseYear = null, string? Hardware = null, string[]? MetadataSources = null, string? ReleaseInfo = null);
public record HelperLaunch(string Executable, string[] Arguments, string WorkingDirectory);
public record GunBinding(int Player, string MouseId, string? KeyboardId = null, string? SerialPort = null, string SystemId = "rs3", string? PhysicalId = null, Dictionary<string, string>? ButtonMap = null, GunFeedback? Feedback = null, bool SoftwareConfigured = false, Dictionary<string,string>? ControlMap = null);
public record InputDevice(string Id, string Name, string Kind, bool RetroShooter, string? PhysicalId = null);
public record AppSettings(bool StartWithWindows = false, bool Fullscreen = true, string? CoverKey = null, string? CalibrationTool = null, string Language = "en", bool CheckForUpdates = true, bool DesktopCrosshairs = true);
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
               ?? throw new InvalidDataException(I18n.T("Die gespeicherte Bibliothek ist ungültig."));
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
            if (existing < 0 && !string.IsNullOrWhiteSpace(game.Executable))
                existing = state.Games.FindIndex(g => !g.Id.StartsWith("inventory-", StringComparison.Ordinal)
                    && g.Title.Equals(game.Title, StringComparison.OrdinalIgnoreCase)
                    && g.Executable.Equals(game.Executable, StringComparison.OrdinalIgnoreCase)
                    && g.Arguments.SequenceEqual(game.Arguments));
            if (existing < 0) state.Games.Add(game);
            else
            {
                var old = state.Games[existing];
                bool sameLaunch = string.Equals(old.Executable, game.Executable, StringComparison.OrdinalIgnoreCase)
                    && old.Arguments.SequenceEqual(game.Arguments) && old.WorkingDirectory == game.WorkingDirectory
                    && (old.RequiredFiles ?? []).SequenceEqual(game.RequiredFiles ?? [])
                    && (old.Helpers ?? []).Select(h => h.Executable + "|" + h.WorkingDirectory + "|" + string.Join("|", h.Arguments)).SequenceEqual(
                        (game.Helpers ?? []).Select(h => h.Executable + "|" + h.WorkingDirectory + "|" + string.Join("|", h.Arguments)));
                state.Games[existing] = game with
                {
                    Favorite = old.Favorite,
                    Cover = old.Cover ?? game.Cover,
                    PreviewVideo = game.PreviewVideo ?? old.PreviewVideo,
                    Screenshot = game.Screenshot ?? old.Screenshot,
                    Logo = game.Logo ?? old.Logo,
                    LastPlayed = old.LastPlayed,
                    Aspect = old.Aspect,
                    Description = game.Description ?? old.Description,
                    ReleaseYear = game.ReleaseYear ?? old.ReleaseYear,
                    Hardware = game.Hardware ?? old.Hardware,
                    MetadataSources = game.MetadataSources ?? old.MetadataSources,
                    ReleaseInfo = game.ReleaseInfo ?? old.ReleaseInfo,
                    Status = game.Status == "needs-setup" ? "needs-setup" : sameLaunch && old.Status != "needs-setup" ? old.Status : "unverified"
                };
            }
        }
    }
}
