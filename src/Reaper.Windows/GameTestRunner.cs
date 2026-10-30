using System.Diagnostics;
using System.Text.Json;
using Reaper.Core;
using System.Windows;
using System.Windows.Interop;

namespace Reaper.Windows;

internal static class GameTestRunner
{
    public static string StopName(int pid) => "Local\\DeadeyeGameTest-stop-" + pid;
    public static bool RunPumped(GameEntry game, LibraryState state, string data, int seconds)
    {
        bool result = false;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            try { result = await Run(game, state, data, seconds); }
            catch (Exception e) { WriteFailure(game, data, "test-error", e.ToString()); }
            finally { app.Shutdown(); }
        };
        app.Run();
        return result;
    }
    public static async Task<bool> Run(GameEntry game, LibraryState state, string data, int seconds)
    {
        if (seconds is < 5 or > 120) throw new ArgumentException("Test duration must be between 5 and 120 seconds.");
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName(Environment.ProcessId));
        var session = new GameSession();
        using var input = new RawInput(); input.Refresh();
        using var receiver = new HwndSource(new HwndSourceParameters("Deadeye game test input") { WindowStyle = 0 });
        receiver.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) => { input.Message(message, lParam); return 0; });
        input.Register(receiver.Handle);
        using var emergency = new EmergencyExit(() => { try { stop.Set(); } catch (ObjectDisposedException) { } LibraryTests.Stop(); }, bindings: state.Bindings);
        // The diagnostic path must service real raw input just like the frontend.
        input.Packet += session.Input;
        var observation = new GameTestObservation(seconds);
        var elapsed = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        Task run;
        try { run = session.Run(game, data, state.Bindings, cancellation.Token); }
        catch (Exception e) { run = Task.FromException(e); }
        bool cancelled = false;
        while (!observation.Finished)
        {
            if (stop.WaitOne(0)) { cancelled = true; break; }
            observation.Update(elapsed.ElapsedMilliseconds, session.GameWindowAvailable, run.IsCompleted);
            if (!observation.Finished) await Task.Delay(100);
        }
        bool foreground = session.HasGameForeground(), available = session.GameWindowAvailable;
        var before = session.LiveProcesses(); var details = session.InspectProcesses();
        cancellation.Cancel();
        await session.End();
        string? error = null;
        try { await run; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception e) { error = e.Message; }
        if (error is null && observation.State != "passed") error = cancelled ? "Test cancelled." : observation.State;
        var after = session.LiveProcesses();
        input.Refresh();
        var controls = OverlayControls.Read(game, data, state.Bindings, input.Devices);
        var audit = GameControlAudit.Read(game, data, state.Bindings, input.Devices);
        bool passed = !cancelled && error is null && available && foreground && after.Length == 0 && session.CleanupIssues.Count == 0;
        string file = Path.Combine(data, "game-test-" + game.Id + ".json");
        string json = JsonSerializer.Serialize(new {
            schemaVersion = 2, time = DateTimeOffset.Now, game.Id, game.Title, seconds,
            observedSeconds = observation.ObservedMilliseconds / 1000.0,
            totalSeconds = elapsed.Elapsed.TotalSeconds,
            startupSeconds = observation.FirstReadyAt / 1000.0,
            phase = observation.State, cancelled,
            exitedEarly = observation.State == "exited-early", windowObserved = observation.FirstReadyAt is not null,
            gameWindowAvailable = available, foreground, error, processDetails = details,
            processesBeforeExit = before, processesAfterExit = after, ended = !session.Active && after.Length == 0,
            controls, controlAudit = audit, cleanupIssues = session.CleanupIssues,
            playerControls = state.Bindings.Select(binding => new { binding.Player, binding.SystemId, hints = StartupControls.ForPlayer(controls, binding) }),
            game.Helpers, game.Players, launchPassed = passed,
            player1Verified = false, player2Verified = false, recoilVerified = false
        }, JsonDefaults.Options);
        File.WriteAllText(file + ".new", json); File.Move(file + ".new", file, true);
        return passed;
    }
    public static void WriteFailure(GameEntry game, string data, string phase, string error)
    {
        Directory.CreateDirectory(data);
        string path = Path.Combine(data, "game-test-" + game.Id + ".json");
        File.WriteAllText(path + ".new", JsonSerializer.Serialize(new {
            schemaVersion = 2, time = DateTimeOffset.Now, game.Id, game.Title, phase, error,
            launchPassed = false, player1Verified = false, player2Verified = false, recoilVerified = false
        }, JsonDefaults.Options));
        File.Move(path + ".new", path, true);
    }

}
