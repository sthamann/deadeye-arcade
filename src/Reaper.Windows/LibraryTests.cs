using System.Diagnostics;
using System.Text.Json;
using Reaper.Core;

namespace Reaper.Windows;

// Each existing --test-game invocation is isolated. A hung test cannot hold the
// rest of the library indefinitely; its Windows job owns only its own game tree.
internal static class LibraryTests
{
    public const string MutexName = "Local\\DeadeyeLibraryTests-v1";
    private const string StopName = "Local\\DeadeyeLibraryTests-stop-v1";
    public static bool Running
    {
        get { if (!Mutex.TryOpenExisting(MutexName, out var mutex)) return false; mutex.Dispose(); return true; }
    }
    public static void Stop()
    {
        if (EventWaitHandle.TryOpenExisting(StopName, out var stop)) { using (stop) stop.Set(); }
    }
    public static async Task Run(string data, bool resume)
    {
        Directory.CreateDirectory(data);
        var store = new LibraryStore(data); var library = store.Load();
        using (var frontend = new Mutex(false, "Local\\ReaperArcade-v1"))
        {
            bool acquired;
            try { acquired = frontend.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Close Deadeye Arcade before starting the library test.");
            frontend.ReleaseMutex();
        }
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName);
        using var emergency = new EmergencyExit(() => { try { stop.Set(); } catch (ObjectDisposedException) { } }, bindings: library.Bindings);
        string report = Path.Combine(data, "library-test.json");
        var previouslyAttempted=resume?Attempted(report):[];
        var games = library.Games.OrderBy(g=>previouslyAttempted.Contains(g.Id)?1:0).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        var results = new List<object>();
        bool cancelled = false, needsAttention = false;GameBlockingDialog? blockingDialog=null;
        void Save(string status, GameEntry? current = null) => Atomic(report, JsonSerializer.Serialize(new {
            schemaVersion = 1, time = DateTimeOffset.Now, status,
            total = games.Length, completed = results.Count,
            current = current is null ? null : new { current.Id, current.Title }, results,
            attemptedIds=previouslyAttempted.Order(StringComparer.Ordinal).ToArray(),
            blockingDialog,
            physicalGameplayVerified = false
        }, JsonDefaults.Options));
        Save("running");
        try
        {
            if(GameSession.CurrentBlockingDialog() is {} existingDialog) {blockingDialog=existingDialog;needsAttention=true;return;}
            foreach (var game in games)
            {
                if (stop.WaitOne(0)) { cancelled = true; break; }
                // Never migrate assignments or overwrite changes made by the user.
                library = store.Load();
                string resultPath = Path.Combine(data, "game-test-" + game.Id + ".json");
                string receipt = resultPath + ".fingerprint";
                string? fingerprint = SafeFingerprint(game, library.Bindings, data);
                if (resume && fingerprint is not null && Passed(resultPath) && File.Exists(receipt) && File.ReadAllText(receipt) == fingerprint)
                {
                    previouslyAttempted.Add(game.Id);results.Add(new { game.Id, game.Title, status = "retained", controlStatus = ControlStatus(resultPath), resultPath }); Save("running"); continue;
                }
                Save("running", game);
                bool timedOut = false, forced = false; int exitCode = -1; string? testError = null;
                nint childJob = 0;
                try
                {
                    if (File.Exists(resultPath)) File.Move(resultPath, resultPath + ".previous", true);
                    var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                    info.ArgumentList.Add("--test-game"); info.ArgumentList.Add(game.Id); info.ArgumentList.Add("15");
                    using var child = NativeOwnedLaunch.Start(info, handle => childJob = NativeOwnedLaunch.CreateJob(handle));
                    var deadline = Stopwatch.StartNew();
                    while (!child.HasExited)
                    {
                        if (stop.WaitOne(0) || deadline.Elapsed > TimeSpan.FromSeconds(165))
                        {
                            timedOut = !stop.WaitOne(0); cancelled = !timedOut;
                            // The child may still be booting before it creates its stop event.
                            var grace = Stopwatch.StartNew();
                            while (!child.HasExited && grace.Elapsed < TimeSpan.FromSeconds(15))
                            {
                                if (EventWaitHandle.TryOpenExisting(GameTestRunner.StopName(child.Id), out var childStop)) { using (childStop) childStop.Set(); }
                                await Task.Delay(100);
                            }
                            if (!child.HasExited) { forced = true; child.Kill(true); await child.WaitForExitAsync(); }
                            break;
                        }
                        await Task.Delay(250);
                    }
                    exitCode = child.ExitCode;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
                { testError = e.Message; forced = true; }
                finally { if (childJob != 0) NativeOwnedLaunch.CloseHandle(childJob); }
                // Closing the outer job also catches a crashed child's helpers and launchers.
                if (forced)
                {
                    await Task.Delay(800);
                    var restoreIssues = await GameInputPreparation.RestoreMenu(library.Bindings);
                    if (restoreIssues.Length > 0)
                    {
                        await Task.Delay(1200); restoreIssues = await GameInputPreparation.RestoreMenu(library.Bindings);
                        if (restoreIssues.Length > 0) { testError = string.Join("; ", restoreIssues); needsAttention = true; }
                    }
                }
                string status = timedOut ? "watchdog-timeout" : cancelled ? "cancelled" : exitCode == 0 && Passed(resultPath) ? "launch-passed" : "needs-action";
                if (!File.Exists(resultPath) || timedOut || testError is not null)
                    GameTestRunner.WriteFailure(game, data, status, testError ?? (timedOut ? "The game test exceeded its deadline." : "The test process exited without a result."));
                previouslyAttempted.Add(game.Id);results.Add(new { game.Id, game.Title, status, controlStatus = ControlStatus(resultPath), exitCode, resultPath });
                // Failed and interrupted games are retried; only successful launches can be retained.
                // Use the worker's actual snapshot, never settings reloaded after
                // the test: those may already belong to a different gun selection.
                string? finalFingerprint = ResultFingerprint(resultPath);
                // Keep evidence for other gun selections and app/profile versions.
                // The latest report alone must not erase a previously tested pair.
                if(finalFingerprint is not null && File.Exists(resultPath))
                {
                    string history=Path.Combine(data,"test-history",System.Text.RegularExpressions.Regex.Replace(game.Id,"[^a-zA-Z0-9_-]","_"));
                    Directory.CreateDirectory(history);Atomic(Path.Combine(history,finalFingerprint+".json"),File.ReadAllText(resultPath));
                }
                if (Passed(resultPath) && !timedOut && !cancelled && finalFingerprint is not null) Atomic(receipt, finalFingerprint);
                else if (File.Exists(receipt)) File.Delete(receipt);
                if(GameSession.CurrentBlockingDialog() is {} remainingDialog) {blockingDialog=remainingDialog;needsAttention=true;}
                if(DiagnosticsUnavailable(resultPath))needsAttention=true;
                Save(cancelled ? "cancelled" : "running");
                if (cancelled || needsAttention) break;
                await Task.Delay(750);
            }
        }
        finally { Save(needsAttention ? "needs-attention" : cancelled ? "cancelled" : results.Count == games.Length ? "complete" : "interrupted");if(needsAttention)Environment.ExitCode=2; }
    }
    private static bool Passed(string path)
    {
        try { using var json = JsonDocument.Parse(File.ReadAllText(path)); return json.RootElement.TryGetProperty("launchPassed", out var value) && value.ValueKind == JsonValueKind.True; }
        catch (Exception e) when (e is IOException or JsonException) { return false; }
    }
    private static HashSet<string> Attempted(string path)
    {
        try
        {
            return LibraryTestProgress.Attempted(File.ReadAllText(path));
        }
        catch(Exception e) when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) {return [];}
    }
    private static string ControlStatus(string path)
    {
        try { using var json = JsonDocument.Parse(File.ReadAllText(path)); return json.RootElement.GetProperty("controlAudit").GetProperty("status").GetString() ?? "needs-review"; }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { return "needs-review"; }
    }
    private static string? ResultFingerprint(string path)
    {
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(path));
            if(!json.RootElement.TryGetProperty("configurationFingerprint",out var value) || value.ValueKind!=JsonValueKind.String)return null;
            string? fingerprint=value.GetString();
            return fingerprint is not null && System.Text.RegularExpressions.Regex.IsMatch(fingerprint,"^[A-F0-9]{64}$")?fingerprint:null;
        }
        catch(Exception e) when(e is IOException or JsonException) {return null;}
    }
    private static bool DiagnosticsUnavailable(string path)
    {
        try {using var json=JsonDocument.Parse(File.ReadAllText(path));return json.RootElement.TryGetProperty("codeIntegrity",out var report) && report.TryGetProperty("checked",out var value) && value.ValueKind==JsonValueKind.False;}
        catch(Exception e) when(e is IOException or JsonException) {return false;}
    }
    private static string? SafeFingerprint(GameEntry game, IEnumerable<GunBinding> bindings, string data)
    {
        try { return Fingerprint(game, bindings, data); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException or JsonException) { return null; }
    }
    private static string Fingerprint(GameEntry game, IEnumerable<GunBinding> bindings, string data) =>
        GameTestFingerprint.Create(game,bindings,data,System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(App).Assembly)?.InformationalVersion??"");
    private static void Atomic(string path, string text) { File.WriteAllText(path + ".new", text); File.Move(path + ".new", path, true); }
}
