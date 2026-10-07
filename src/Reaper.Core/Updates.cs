using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reaper.Core;

public sealed record AppRelease(string Version, string Url, string Sha256, long Size, string Notes, string Page);
public static class AppUpdates
{
    public const string Repository = "sthamann/reaper-arcade";
    public const string Endpoint = "https://api.github.com/repos/" + Repository + "/releases/latest";
    public static Version ParseVersion(string text)
    {
        if (!Regex.IsMatch(text, @"^v?\d+\.\d+\.\d+$")) throw new InvalidDataException("Invalid release version.");
        var v = Version.Parse(text.TrimStart('v')); return new(v.Major, v.Minor, v.Build, 0);
    }
    public static AppRelease? Read(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
        if (r.GetProperty("draft").GetBoolean() || r.GetProperty("prerelease").GetBoolean()) return null;
        string tag = r.GetProperty("tag_name").GetString()!;
        var version = ParseVersion(tag);
        if (version <= current) return null;
        string number = version.ToString(3), name = $"Reaper-Arcade-{number}-Setup-x64.exe";
        var assets = r.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) return null;
        var asset = assets[0]; string url = asset.GetProperty("browser_download_url").GetString()!;
        if (url != $"https://github.com/{Repository}/releases/download/{tag}/{name}") throw new InvalidDataException("Unexpected release download address.");
        string digest = asset.GetProperty("digest").GetString() ?? "";
        if (!Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) throw new InvalidDataException("Release has no valid SHA-256 digest.");
        long size = asset.GetProperty("size").GetInt64();
        if (size is < 1024 or > 536870912) throw new InvalidDataException("Unexpected installer size.");
        string page = $"https://github.com/{Repository}/releases/tag/{tag}";
        return new(number, url, digest[7..].ToLowerInvariant(), size, r.GetProperty("body").GetString() ?? "", page);
    }
    public static async Task<AppRelease?> Check(HttpClient client, Version current, CancellationToken cancellation = default)
    {
        using var response = await client.GetAsync(Endpoint, cancellation);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return Read(await response.Content.ReadAsStringAsync(cancellation), current);
    }
    public static async Task<string> Download(HttpClient client, AppRelease release, string directory, IProgress<int>? progress, CancellationToken cancellation = default)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Reaper-Arcade-{release.Version}-Setup-x64.exe"), partial = path + ".partial";
        try
        {
            using var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            var final = response.RequestMessage?.RequestUri ?? throw new InvalidDataException("Missing download address.");
            if (final.Scheme != "https" || !(final.Host == "github.com" || final.Host == "release-assets.githubusercontent.com" || final.Host == "objects.githubusercontent.com")) throw new InvalidDataException("Unexpected download host.");
            if (response.Content.Headers.ContentLength is long length && length != release.Size) throw new InvalidDataException("Installer size differs from release metadata.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[81920]; long total = 0; int last = -1, read;
                while ((read = await input.ReadAsync(buffer, cancellation)) > 0)
                {
                    total += read; if (total > release.Size) throw new InvalidDataException("Installer exceeds expected size.");
                    hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    int percent = (int)(total * 100 / release.Size); if (percent != last) { progress?.Report(percent); last = percent; }
                }
                if (total != release.Size || Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() != release.Sha256)
                    throw new InvalidDataException("Installer checksum verification failed.");
            }
            File.Move(partial, path, true); return path;
        }
        catch { if (File.Exists(partial)) File.Delete(partial); throw; }
    }
}
