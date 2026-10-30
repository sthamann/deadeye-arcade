using System.Text.Json;

namespace Reaper.Core;

public static class LibraryTestProgress
{
    public static HashSet<string> Attempted(string json)
    {
        var ids=new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var document=JsonDocument.Parse(json);
            var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object)return ids;
            if(root.TryGetProperty("attemptedIds",out var previous) && previous.ValueKind==JsonValueKind.Array)
                foreach(var id in previous.EnumerateArray())Add(id);
            if(root.TryGetProperty("results",out var results) && results.ValueKind==JsonValueKind.Array)
                foreach(var row in results.EnumerateArray())
                    if(row.ValueKind==JsonValueKind.Object && row.TryGetProperty("id",out var id))Add(id);
            void Add(JsonElement id){if(id.ValueKind==JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))ids.Add(id.GetString()!);}
        }
        catch(JsonException){ }
        return ids;
    }
}
