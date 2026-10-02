using System.Net.Http.Headers;
using System.Text.Json;

namespace Reaper.Core;

public sealed class CoverService(HttpClient http, string directory)
{
    public async Task<string?> Download(string title, string key, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.steamgriddb.com/api/v2/search/autocomplete/" + Uri.EscapeDataString(title));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
        var matches = doc.RootElement.GetProperty("data").EnumerateArray()
            .Where(e => GameImporter.Normalize(e.GetProperty("name").GetString() ?? "") == GameImporter.Normalize(title)).ToList();
        // Never guess between editions with similar names.
        if (matches.Count != 1) return null;
        int id = matches[0].GetProperty("id").GetInt32();
        using var gridRequest = new HttpRequestMessage(HttpMethod.Get, $"https://www.steamgriddb.com/api/v2/grids/game/{id}?dimensions=600x900&types=static");
        gridRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var gridResponse = await http.SendAsync(gridRequest, cancel);
        gridResponse.EnsureSuccessStatusCode();
        using var grids = JsonDocument.Parse(await gridResponse.Content.ReadAsStringAsync(cancel));
        var list = grids.RootElement.GetProperty("data");
        if (list.GetArrayLength() == 0) return null;
        var uri = new Uri(list[0].GetProperty("url").GetString()!);
        if (uri.Scheme != "https" || uri.Host != "cdn2.steamgriddb.com") return null;
        using var image = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancel);
        image.EnsureSuccessStatusCode();
        string? mime = image.Content.Headers.ContentType?.MediaType;
        string ext = mime switch { "image/png" => ".png", "image/jpeg" => ".jpg", "image/webp" => ".webp", _ => "" };
        if (ext == "" || image.Content.Headers.ContentLength > 8_000_000) return null;
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"sgdb-{id}{ext}");
        using var data = await image.Content.ReadAsStreamAsync(cancel);
        using var memory = new MemoryStream();
        var buffer = new byte[8192]; int length;
        while ((length = await data.ReadAsync(buffer, cancel)) > 0)
        {
            if (memory.Length + length > 8_000_000) return null;
            await memory.WriteAsync(buffer.AsMemory(0, length), cancel);
        }
        await File.WriteAllBytesAsync(file, memory.ToArray(), cancel);
        return file;
    }
}
