namespace Reaper.Core;

public record GameEnrichment(string Id, string Title, string Platform, string Description, int? ReleaseYear,
    string Hardware, string[]? MetadataSources = null, string? Cover = null, string? PreviewVideo = null,
    string? Screenshot = null, string? Logo = null, string? ReleaseInfo = null);
public record EnrichmentManifest(GameEnrichment[] Games);

/// <summary>Updates presentation data only; launch routes, controls and verification status stay intact.</summary>
public static class LibraryEnrichment
{
    public static void Apply(LibraryState state, EnrichmentManifest manifest)
    {
        var updated = new Dictionary<string, GameEntry>();
        foreach (var row in manifest.Games)
        {
            var game = state.Games.SingleOrDefault(g => g.Id == row.Id)
                ?? throw new InvalidDataException($"Unknown game: {row.Id}");
            if (game.Title != row.Title || game.Platform != row.Platform || updated.ContainsKey(row.Id))
                throw new InvalidDataException($"Ambiguous game identity: {row.Id}");
            if (string.IsNullOrWhiteSpace(row.Description) || row.Description.Length > 600 || string.IsNullOrWhiteSpace(row.Hardware)
                || row.ReleaseYear is < 1970 || row.ReleaseYear > DateTime.Now.Year + 1)
                throw new InvalidDataException($"Invalid game information: {row.Id}");
            foreach (var source in row.MetadataSources ?? [])
                if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                    throw new InvalidDataException($"Invalid information source: {row.Id}");
            CheckMedia(row.Cover, false); CheckMedia(row.Screenshot, false); CheckMedia(row.Logo, false); CheckMedia(row.PreviewVideo, true);
            updated.Add(row.Id, game with { Description = row.Description.Trim(), ReleaseYear = row.ReleaseYear,
                Hardware = row.Hardware.Trim(), MetadataSources = row.MetadataSources,
                ReleaseInfo = row.ReleaseInfo,
                Cover = row.Cover ?? game.Cover, PreviewVideo = row.PreviewVideo ?? game.PreviewVideo,
                Screenshot = row.Screenshot ?? game.Screenshot, Logo = row.Logo ?? game.Logo });
        }
        // Validate the entire manifest before modifying any game.
        for (int i = 0; i < state.Games.Count; i++)
            if (updated.TryGetValue(state.Games[i].Id, out var game)) state.Games[i] = game;
    }
    private static void CheckMedia(string? path, bool video)
    {
        if (path is null) return;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (!(video ? new[] { ".mp4", ".webm", ".m4v" } : new[] { ".jpg", ".jpeg", ".png", ".webp" }).Contains(extension)
            || !File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidDataException($"Missing or unsupported media: {path}");
    }
    public static object Audit(LibraryState state) => new
    {
        time = DateTimeOffset.Now, games = state.Games.Count,
        descriptions = state.Games.Count(g => !string.IsNullOrWhiteSpace(g.Description)),
        years = state.Games.Count(g => g.ReleaseYear is not null),
        hardware = state.Games.Count(g => !string.IsNullOrWhiteSpace(g.Hardware)),
        covers = state.Games.Count(g => Present(g.Cover)), videos = state.Games.Count(g => Present(g.PreviewVideo)),
        screenshots = state.Games.Count(g => Present(g.Screenshot)), logos = state.Games.Count(g => Present(g.Logo)),
        rows = state.Games.Select(g => new { g.Id, g.Title, g.Platform, g.Description, g.ReleaseYear, g.ReleaseInfo, g.Hardware,
            cover = Present(g.Cover), previewVideo = Present(g.PreviewVideo), screenshot = Present(g.Screenshot), logo = Present(g.Logo) })
    };
    private static bool Present(string? path) => path is not null && File.Exists(path) && new FileInfo(path).Length > 0;
}
