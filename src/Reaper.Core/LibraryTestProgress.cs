using System.Text.Json;

namespace Reaper.Core;

public static class LibraryTestProgress
{
    public static bool DiagnosticsUnavailable(string json,bool targetExists)
    {
        using var document=JsonDocument.Parse(json);var report=document.RootElement;
        if(report.ValueKind!=JsonValueKind.Object)return true;
        if(!report.TryGetProperty("codeIntegrity",out var integrity))return false;
        if(integrity.ValueKind!=JsonValueKind.Object||!integrity.TryGetProperty("checked",out var check))return true;
        if(check.ValueKind==JsonValueKind.True)return false;
        if(check.ValueKind!=JsonValueKind.False)return true;
        // An absent target rejected before a game window exists is already a
        // failed setup, never a successful launch. Its unmapped drive must not
        // stop tests of unrelated games. A genuine scanner failure still stops
        // any attempted/otherwise healthy launch and is never a passing result.
        bool preflightMissing=!targetExists
            && report.TryGetProperty("windowObserved",out var observed)&&observed.ValueKind==JsonValueKind.False
            && report.TryGetProperty("phase",out var phase)&&phase.ValueKind==JsonValueKind.String&&phase.GetString()=="exited-early"
            && report.TryGetProperty("failure",out var failure)&&failure.ValueKind==JsonValueKind.Object
            && failure.TryGetProperty("category",out var category)&&category.ValueKind==JsonValueKind.String&&category.GetString()=="launch-error"
            && integrity.TryGetProperty("blocks",out var blocks)&&blocks.ValueKind==JsonValueKind.Array&&blocks.GetArrayLength()==0;
        return !preflightMissing;
    }
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
