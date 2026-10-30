using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Text;
using Reaper.Core;

namespace Reaper.Windows;

public sealed class GameSession
{
    private readonly Dictionary<int, DateTime> owned = [];
    private volatile bool stop;
    private volatile bool overlayVisible;
    private string targetExecutable = "";
    private readonly object sync = new();
    private Task? ending;
    private int reserved;
    private nint job;
    private nint gameWindow;
    private DolphinGunBridge? bridge;
    public void ResetInput() => bridge?.ResetInput();
    public void Input(RawPacket packet)
    {
        var current=bridge;
        if(packet.Kind=="mouse") current?.Mouse(packet.DeviceId,packet.Absolute,packet.X,packet.Y,packet.Buttons);
        else if(packet.Kind=="keyboard") current?.Key(packet.DeviceId,packet.Key,packet.Down);
    }
    private readonly List<(nint Handle, int Show)> overlayWindows = [];
    public bool Active { get; private set; }
    public List<string> CleanupIssues { get; } = [];
    public event Action<string>? Changed;
    public event Action<nint>? GameWindowReady;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    { public uint Size, Usage, Pid; public nint Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name; }
    [DllImport("kernel32.dll")] private static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32First(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32Next(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int maximum);
    private static bool IsRenderWindow(nint handle)
    {
        var name = new StringBuilder(256); GetClassName(handle, name, name.Capacity);
        return name.ToString() is not ("#32770" or "ConsoleWindowClass" or "CASCADIA_HOSTING_WINDOW_CLASS");
    }
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle,out uint processId);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool QueryInformationJobObject(nint job,int info,nint buffer,uint size,out uint returned);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool TerminateJobObject(nint job,uint exitCode);
    public nint GameWindow => gameWindow;
    public bool GameWindowAvailable
    {
        get
        {
            if (gameWindow == 0 || !IsWindowVisible(gameWindow) || IsIconic(gameWindow) || !IsRenderWindow(gameWindow)) return false;
            GetWindowThreadProcessId(gameWindow, out var id);
            return LiveProcesses().Contains((int)id);
        }
    }
    public int[] LiveProcesses()
    {
        KeyValuePair<int,DateTime>[] list; lock(sync) list=owned.ToArray();
        return list.Where(pair => {
            try {using var process=Process.GetProcessById(pair.Key);return !process.HasExited && process.StartTime==pair.Value;}
            catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) {return false;}
        }).Select(pair=>pair.Key).ToArray();
    }
    public object InspectProcesses() => new {
        targetExecutable, gameWindow = gameWindow.ToInt64(), foregroundWindow = GetForegroundWindow().ToInt64(),
        foregroundProcess = ForegroundProcess(),
        processes=LiveProcesses().Select(id=> {
            try {using var p=Process.GetProcessById(id);return new {id,p.ProcessName,path=p.MainModule?.FileName,window=p.MainWindowHandle.ToInt64(),p.MainWindowTitle};}
            catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) {return new {id,ProcessName="",path=(string?)null,window=0L,MainWindowTitle=e.Message};}
        }).ToArray()
    };
    private static object? ForegroundProcess()
    {
        GetWindowThreadProcessId(GetForegroundWindow(),out var id);
        if(id==0)return null;
        try {using var process=Process.GetProcessById((int)id);return new {id,process.ProcessName,process.MainWindowTitle};}
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) {return new {id,ProcessName="",MainWindowTitle=e.Message};}
    }
    private HashSet<int> JobProcesses()
    {
        var result=new HashSet<int>();
        if(job==0) return result;
        const int bytes=65536; var buffer=Marshal.AllocHGlobal(bytes);
        try {
            if(QueryInformationJobObject(job,3,buffer,bytes,out _)) {
                int count=Math.Min(Marshal.ReadInt32(buffer,4),(bytes-8)/IntPtr.Size);
                for(int i=0;i<count;i++) result.Add((int)Marshal.ReadIntPtr(buffer,8+i*IntPtr.Size));
            }
        } finally {Marshal.FreeHGlobal(buffer);}
        return result;
    }
    public bool HasGameForeground() => Active && !overlayVisible && ending is null
        && GameWindowAvailable && GetForegroundWindow()==gameWindow;
    public void HideForOverlay()
    {
        bridge?.Pause(true);
        overlayVisible = true; overlayWindows.Clear();
        // Keep the game behind the translucent menu. Minimizing it destroys the
        // requested overlay effect and can make old renderers lose their device.
    }
    public void RestoreFromOverlay()
    {
        bridge?.Pause(false);
        overlayVisible = false;
        if (Active) foreach (var window in overlayWindows) { ShowWindowAsync(window.Handle, window.Show); SetForegroundWindow(window.Handle); }
        // A launcher can own a visible status window. Finish with the actual game in front.
        KeyValuePair<int, DateTime>[] list; lock (sync) list = owned.ToArray();
        if (Active) foreach (var pair in list)
            try
            {
                using var process = Process.GetProcessById(pair.Key);
                if (!process.HasExited && process.StartTime == pair.Value && string.Equals(process.MainModule?.FileName, targetExecutable, StringComparison.OrdinalIgnoreCase) && process.MainWindowHandle != 0)
                    SetForegroundWindow(process.MainWindowHandle);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        overlayWindows.Clear();
        if(Active && gameWindow!=0) SetForegroundWindow(gameWindow);
    }
    private static List<(int Id, int Parent)> Snapshot()
    {
        List<(int, int)> rows = []; var handle = CreateToolhelp32Snapshot(2, 0); if (handle == -1) return rows;
        try { var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" }; if (Process32First(handle, ref entry)) do { rows.Add(((int)entry.Pid, (int)entry.Parent)); } while (Process32Next(handle, ref entry)); }
        finally { CloseHandle(handle); }
        return rows;
    }
    public async Task Run(GameEntry game, string dataDirectory, IEnumerable<GunBinding> bindings, CancellationToken cancellationToken = default, bool fixesPrepared = false)
    {
        if (Interlocked.CompareExchange(ref reserved,1,0)!=0) throw new InvalidOperationException(I18n.T("Es läuft bereits ein Spiel."));
        CleanupIssues.Clear();
        var helpers = new List<Process>();
        var helperJobs = new List<nint>();
        GameInputPreparation? input = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!fixesPrepared) await AutomaticFixes.Run([game], dataDirectory);
            cancellationToken.ThrowIfCancellationRequested();
            _ = LaunchRules.Prepare(game);
            input = await GameInputPreparation.Prepare(game, dataDirectory, bindings);
            cancellationToken.ThrowIfCancellationRequested();
            bridge=input.Bridge;
            game = input.Game;
            var players = input.Players;
            var helperFiles = KnownFixes.MultiplayerFiles(game);
            var beforeHelpers = KnownFixes.Snapshot(helperFiles);
            MultiplayerSetup.Configure(game, players);
            KnownFixes.RecordChanges(game, dataDirectory, "multiplayer-plugin", beforeHelpers, helperFiles);
            foreach (var helper in game.Helpers ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var previous = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(helper.Executable));
                bool running = previous.Length > 0; foreach (var p in previous) p.Dispose();
                if (running) throw new IOException(I18n.T("Der Helfer läuft bereits. Bitte vor dem Start schließen: ") + Path.GetFileName(helper.Executable));
                var info = new ProcessStartInfo(helper.Executable) { WorkingDirectory = helper.WorkingDirectory, UseShellExecute = false };
                foreach (var argument in helper.Arguments) info.ArgumentList.Add(argument);
                helpers.Add(NativeOwnedLaunch.Start(info, handle => helperJobs.Add(NativeOwnedLaunch.CreateJob(handle))));
            }
            await RunGame(game, dataDirectory, players, cancellationToken);
        }
        finally
        {
            if (ending is not null) await ending;
            foreach (var helper in helpers)
            {
                try { if (!helper.HasExited) helper.CloseMainWindow(); } catch (InvalidOperationException) { }
            }
            if (helpers.Count > 0) await Task.Delay(1200);
            foreach (var helper in helpers)
            {
                try
                {
                    if (!helper.HasExited) helper.Kill(true);
                    await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException) { CleanupIssues.Add("Helper cleanup: " + e.Message); }
                helper.Dispose();
            }
            foreach (nint helperJob in helperJobs) NativeOwnedLaunch.CloseHandle(helperJob);
            lock(sync) { if(job!=0) {CloseHandle(job);job=0;} }
            bridge=null;
            try {if(input is not null) await input.DisposeAsync();}
            finally {Active=false;gameWindow=0;Interlocked.Exchange(ref reserved,0);Changed?.Invoke("ended");}
        }
    }
    private async Task RunGame(GameEntry game, string dataDirectory, IEnumerable<GunBinding> bindings, CancellationToken cancellationToken)
    {
        if (Active) throw new InvalidOperationException(I18n.T("Es läuft bereits ein Spiel."));
        var profileFiles = game.Source == "teknoparrot" ? new[] { game.SourcePath } : new[] { Path.Combine(game.WorkingDirectory, "EMULATOR.INI") };
        var beforeProfile = KnownFixes.Snapshot(profileFiles);
        EmulatorSetup.ConfigurePaths(game);
        string loader = Path.Combine(Path.GetDirectoryName(game.Executable)!, "ElfLdr2", "libs");
        string[] libraries = [Path.Combine(loader, "librnalindbergh_jr.so"), Path.Combine(loader, "libcri_soundoutput_lindbergh_jr.so")];
        var beforeLibraries = KnownFixes.Snapshot(libraries);
        LocalRuntimeSetup.Configure(game);
        KnownFixes.RecordChanges(game, dataDirectory, "local-loader-libs", beforeLibraries, libraries);
        TeknoGunSetup.Configure(game,bindings);
        GamePresentation.Prepare(game);
        KnownFixes.RecordChanges(game, dataDirectory, "game-profile", beforeProfile, profileFiles);
        var info = LaunchRules.Prepare(game);
        if(SupermodelSetup.IsSupermodel(game) && bindings.Any()) info.ArgumentList.Add("-input-system=rawinput");
        if (game.Source == "teknoparrot")
        {
            var previous = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(game.Executable));
            bool running = previous.Length > 0; foreach (var p in previous) p.Dispose();
            if (running) throw new InvalidOperationException(I18n.T("Bitte die bereits geöffnete TeknoParrot-Oberfläche schließen. Danach kann Deadeye Arcade die eigene Spielsitzung starten."));
        }
        if (game.Source == "mame")
        {
            info.ArgumentList.Add("-multikeyboard");
            info.ArgumentList.Add("-keyboardprovider");info.ArgumentList.Add("rawinput");
            foreach(string argument in new[]{"-lightgun","-multimouse","-lightgunprovider","rawinput","-mouseprovider","rawinput","-adstick_device","lightgun","-lightgun_device","lightgun"})info.ArgumentList.Add(argument);
            // MAME creates its own game window; its console must not cover it.
            info.CreateNoWindow = true;
            string ctrl = Path.Combine(dataDirectory, "controllers"); Directory.CreateDirectory(ctrl);
            string controller = Path.Combine(ctrl, "reaper.cfg");
            var beforeController = KnownFixes.Snapshot([controller]);
            File.WriteAllText(controller, LaunchRules.MameController(bindings));
            KnownFixes.RecordChanges(game, dataDirectory, "usb-identity", beforeController, [controller]);
            info.ArgumentList.Add("-ctrlrpath"); info.ArgumentList.Add(ctrl); info.ArgumentList.Add("-ctrlr"); info.ArgumentList.Add("reaper");
        }
        string target = game.Executable;
        if (game.Source == "teknoparrot")
        {
            var doc = XDocument.Load(game.SourcePath);
            string? path = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "GamePath")?.Value;
            if (!string.IsNullOrWhiteSpace(path)) target = Path.GetFullPath(path, game.WorkingDirectory);
        }
        HashSet<int> existing = Snapshot().Select(p => p.Id).ToHashSet();
        targetExecutable = target;
        var started = DateTime.Now; owned.Clear(); stop = false; ending = null; overlayVisible = false; overlayWindows.Clear();
        cancellationToken.ThrowIfCancellationRequested();
        using var process = NativeOwnedLaunch.Start(info, handle => job = NativeOwnedLaunch.CreateJob(handle));
        lock (sync) owned[process.Id] = process.StartTime;
        Active = true; Changed?.Invoke("running");
        bool targetSeen = game.Source != "teknoparrot", descendantSeen = false;
        var targets = new HashSet<int>(); if (targetSeen) targets.Add(process.Id);
        var lastChild = DateTime.Now;
        var presented = new HashSet<nint>();
        bool legendShown=false;
        nint legendWindow=0; long legendSince=0;
        try
        {
            while (!stop && !cancellationToken.IsCancellationRequested)
            {
                var snapshot = Snapshot();
                lock (sync)
                {
                    var jobProcesses=JobProcesses();
                    // Include only new processes from this session's tree or the exact configured game executable.
                    foreach (var item in snapshot.Where(p => jobProcesses.Contains(p.Id) && !existing.Contains(p.Id) && !owned.ContainsKey(p.Id)))
                    {
                        try
                        {
                            using var candidate = Process.GetProcessById(item.Id);
                            if (candidate.StartTime < started.AddSeconds(-1)) continue;
                            bool exact = string.Equals(candidate.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase);
                            if (jobProcesses.Contains(item.Id))
                            {
                                owned[item.Id] = candidate.StartTime;
                                if (item.Id != process.Id) descendantSeen = true;
                                if (exact) { targetSeen = true; targets.Add(item.Id); }
                            }
                        }
                        catch (Exception e) when (e is System.ComponentModel.Win32Exception or ArgumentException or InvalidOperationException) { }
                    }
                    int live = 0, liveTarget = 0, children = 0;
                    foreach (var pair in owned.ToArray())
                    {
                        try { using var p = Process.GetProcessById(pair.Key); if (!p.HasExited && p.StartTime == pair.Value) { live++; if (targets.Contains(pair.Key)) liveTarget++; if (pair.Key != process.Id) children++; } }
                        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                    }
                    // Minimized frontend may otherwise leave a terminal ahead of a slowly created game window.
                    // Focus the actual renderer once, only during startup and while no overlay is open.
                    if (!overlayVisible && DateTime.Now - started < TimeSpan.FromSeconds(20))
                        foreach (var pair in owned.ToArray())
                            try
                            {
                                using var gameProcess = Process.GetProcessById(pair.Key);
                                if (gameProcess.HasExited || gameProcess.StartTime != pair.Value) continue;
                                if(!IsGameRuntime(gameProcess,game,target,process.Id))continue;
                                var handle = gameProcess.MainWindowHandle;
                                if (handle != 0 && IsWindowVisible(handle) && presented.Add(handle)) { if(IsIconic(handle)) ShowWindowAsync(handle, 9); SetForegroundWindow(handle); }
                            }
                            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                    // Wait for the actual game window, not the launcher or dependency dialog.
                    // Count the ten seconds only after a stable foreground game window exists.
                    // Games such as Blue Estate replace their splash window after startup.
                    // Keep the renderer handle current for exit checks and overlay restoration,
                    // while displaying the startup legend only once per session.
                    if((!legendShown || !GameWindowAvailable) && !overlayVisible && ending is null)
                    {
                        nint readyWindow=0;
                        foreach(var pair in owned.ToArray())
                            try {
                                using var candidate=Process.GetProcessById(pair.Key);
                                if(candidate.HasExited || candidate.StartTime!=pair.Value) continue;
                                var handle=candidate.MainWindowHandle;
                                if(handle==0 || !IsWindowVisible(handle) || IsIconic(handle) || !IsRenderWindow(handle)) continue;
                                if(IsGameRuntime(candidate,game,target,process.Id)) {readyWindow=handle;break;}
                            } catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                        if(readyWindow==0) legendWindow=0;
                        else if(legendWindow!=readyWindow) {legendWindow=readyWindow;legendSince=Environment.TickCount64;}
                        else if(Environment.TickCount64-legendSince>=750) {
                            gameWindow=readyWindow;GamePresentation.Apply(game,readyWindow);
                            SetForegroundWindow(readyWindow);
                            if(!legendShown) {legendShown=true;GameWindowReady?.Invoke(readyWindow);}
                        }
                    }
                    if (children > 0) lastChild = DateTime.Now;
                    if (targetSeen && liveTarget == 0 && (game.Source == "teknoparrot" || children == 0)) break;
                    if (descendantSeen && children == 0 && DateTime.Now - lastChild > TimeSpan.FromSeconds(3)) break;
                    if (live == 0 && DateTime.Now - started > TimeSpan.FromSeconds(15)) throw new IOException(I18n.T("Der Starter wurde beendet, aber kein Spielprozess erkannt. Bitte das Profil direkt in seinem Emulator prüfen."));
                    if (!targetSeen && !descendantSeen && DateTime.Now - started > TimeSpan.FromSeconds(30)) throw new IOException(I18n.T("Kein Spielprozess erkannt. TeknoParrot-Profil direkt prüfen."));
                }
                await Task.Delay(200);
            }
            if(!stop && !cancellationToken.IsCancellationRequested && ending is null && game.Source!="teknoparrot" && process.HasExited && process.ExitCode!=0)
                throw new IOException(I18n.F($"Das Spiel wurde mit Fehlercode {process.ExitCode} beendet. Bitte das Emulatorprotokoll prüfen."));
        }
        finally
        {
            await End();
            // The owning wrapper releases this session's helpers before returning to the menu.
        }
    }
    public Task End() { lock (sync) return ending ??= EndOwned(); }
    private async Task EndOwned()
    {
        if (!Active) return;
        KeyValuePair<int, DateTime>[] list; lock (sync) list = owned.ToArray();
        foreach (var pair in list)
        {
            try { using var p = Process.GetProcessById(pair.Key); if (!p.HasExited && p.StartTime == pair.Value) p.CloseMainWindow(); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        await Task.Delay(1800);
        // Launchers may reopen their library while the game closes. Include children
        // observed during the grace period, while retaining PID/start-time ownership.
        lock (sync) list = owned.ToArray();
        lock(sync) {if(job!=0) TerminateJobObject(job,0);}
        foreach (var pair in list.Reverse())
        {
            try { using var p = Process.GetProcessById(pair.Key); if (!p.HasExited && p.StartTime == pair.Value) p.Kill(); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        stop = true;
    }
    private static bool IsGameRuntime(Process candidate,GameEntry game,string target,int launcherPid)
    {
        string? image=candidate.MainModule?.FileName;
        return !GameRuntimeWindows.KnownLauncher(target) && string.Equals(image,target,StringComparison.OrdinalIgnoreCase)
            || game.Source=="teknoparrot" && IsTeknoRuntime(candidate)
            || game.Source!="teknoparrot" && candidate.Id!=launcherPid && image is not null && GameRuntimeWindows.ChildRuntime(game.Executable,game.WorkingDirectory,image);
    }
    private static bool IsTeknoRuntime(Process process) => process.ProcessName.Equals("BudgieLoader",StringComparison.OrdinalIgnoreCase)
        || process.ProcessName.Equals("elfldr2",StringComparison.OrdinalIgnoreCase)
        || process.ProcessName.Equals("linuxloader",StringComparison.OrdinalIgnoreCase)
        || process.ProcessName.Equals("rpcs3",StringComparison.OrdinalIgnoreCase) && process.MainWindowTitle.Equals("RPCS3 via TeknoParrot",StringComparison.Ordinal)
        || process.ProcessName.Equals("Play",StringComparison.OrdinalIgnoreCase) && process.MainWindowTitle.StartsWith("Play! - [",StringComparison.Ordinal);
}
