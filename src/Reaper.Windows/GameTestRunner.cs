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
        var startedAt=DateTimeOffset.UtcNow;
        var elapsed = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        Task run;
        try { run = session.Run(game, data, state.Bindings, cancellation.Token); }
        catch (Exception e) { run = Task.FromException(e); }
        bool cancelled = false;long? focusedSince=null;GameBlockingDialog? blockingDialog=null;long? blockedSince=null;
        while (!observation.Finished)
        {
            if (stop.WaitOne(0)) { cancelled = true; break; }
            var dialog=session.BlockingDialog();
            if(dialog is not null && dialog==blockingDialog)
            {
                blockedSince??=elapsed.ElapsedMilliseconds;
                if(elapsed.ElapsedMilliseconds-blockedSince>=2000)break;
            }
            else {blockingDialog=dialog;blockedSince=null;}
            observation.Update(elapsed.ElapsedMilliseconds, session.GameWindowAvailable, run.IsCompleted);
            if(session.GameWindowAvailable && session.HasGameForeground())focusedSince??=elapsed.ElapsedMilliseconds;
            else focusedSince=null;
            if (!observation.Finished) await Task.Delay(100);
        }
        bool available = session.GameWindowAvailable;
        bool foreground = session.HasGameForeground();
        double foregroundStableSeconds=focusedSince is {} since?(elapsed.ElapsedMilliseconds-since)/1000.0:0;
        var before = session.LiveProcesses(); var details = session.InspectProcesses();
        cancellation.Cancel();
        await session.End();
        string? error = null; GameTestFailure? failure = null;
        try { await run; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception e) { error = e.Message; failure = GameTestFailure.From(e); }
        if(blockingDialog is not null) {error="A modal system or game dialog blocked the test: "+blockingDialog.Title;failure=new("blocked-dialog",error,"",0,null);}
        if (error is null && observation.State != "passed") error = cancelled ? "Test cancelled." : observation.State;
        var after = session.LiveProcesses();
        var cleanupWait = Stopwatch.StartNew();
        while(after.Length>0 && cleanupWait.Elapsed<TimeSpan.FromSeconds(3)) {await Task.Delay(100);after=session.LiveProcesses();}
        var blockingDialogAfterExit=session.BlockingDialog();
        var codeIntegrity=await Task.Run(()=>CodeIntegrityDiagnostics.Read(game,startedAt));
        if(codeIntegrity.Blocks.Length>0)
        {
            error="Windows App Control blocked a game or emulator file: "+codeIntegrity.Blocks[0].File;
            failure=new("windows-app-control",error,"",0,null);
        }
        if(error is null && !codeIntegrity.Checked) {error="Windows Code Integrity checks could not complete: "+codeIntegrity.Error;failure=new("diagnostics-unavailable",error,"",0,null);}
        if(error is null && blockingDialogAfterExit is not null) {error="A modal system or game dialog remained after cleanup: "+blockingDialogAfterExit.Title;failure=new("blocked-dialog-after-exit",error,"",0,null);}
        if(error is null && (!foreground || foregroundStableSeconds<2)) {error="The game window did not retain foreground focus.";failure=new("foreground-missing",error,"",0,null);}
        if(error is null && (after.Length>0 || session.CleanupIssues.Count>0)) {error="The game or its helpers did not close cleanly.";failure=new("cleanup-failed",error,"",0,null);}
        input.Refresh();
        var controls = OverlayControls.Read(game, data, state.Bindings, input.Devices);
        var audit = GameControlAudit.Read(game, data, state.Bindings, input.Devices);
        var simulation = (await Task.Run(()=>ControlSimulation.Run([game],data,state.Bindings,input.Devices))).Games.Single();
        bool passed = !cancelled && error is null && available && foreground && after.Length == 0 && session.CleanupIssues.Count == 0;
        string? version=System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(App).Assembly)?.InformationalVersion;
        string? fingerprint=null;
        try {fingerprint=GameTestFingerprint.Create(game,state.Bindings,data,version??"");}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException or JsonException) { }
        string file = Path.Combine(data, "game-test-" + game.Id + ".json");
        string json = JsonSerializer.Serialize(new {
            schemaVersion = 2, time = DateTimeOffset.Now, game.Id, game.Title, seconds,
            appVersion = version, configurationFingerprint = fingerprint, testedAssignments = state.Bindings,
            observedSeconds = observation.ObservedMilliseconds / 1000.0,
            totalSeconds = elapsed.Elapsed.TotalSeconds,
            startupSeconds = observation.FirstReadyAt / 1000.0,
            phase = blockingDialog is null?observation.State:"blocked-dialog", cancelled, blockingDialog, blockingDialogAfterExit,
            exitedEarly = observation.State == "exited-early", windowObserved = observation.FirstReadyAt is not null,
            gameWindowAvailable = available, foreground, foregroundStableSeconds, error, failure, codeIntegrity, processDetails = details,
            processesBeforeExit = before, processesAfterExit = after, ended = !session.Active && after.Length == 0,
            controls, controlAudit = audit, controlSimulation = simulation, cleanupIssues = session.CleanupIssues,
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
