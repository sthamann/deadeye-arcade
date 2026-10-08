using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
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
    private readonly List<(nint Handle, int Show)> overlayWindows = [];
    public bool Active { get; private set; }
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
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle,out uint processId);
    public bool HasGameForeground()
    {
        if(!Active || overlayVisible || ending is not null) return false;
        GetWindowThreadProcessId(GetForegroundWindow(),out var id);
        DateTime start; lock(sync) if(!owned.TryGetValue((int)id,out start))return false;
        try {using var process=Process.GetProcessById((int)id);return !process.HasExited && process.StartTime==start;}
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) {return false;}
    }
    public void HideForOverlay()
    {
        overlayVisible = true; overlayWindows.Clear();
        KeyValuePair<int, DateTime>[] list; lock (sync) list = owned.ToArray();
        foreach (var pair in list)
            try
            {
                using var p = Process.GetProcessById(pair.Key);
                if (p.HasExited || p.StartTime != pair.Value || p.MainWindowHandle == 0 || IsIconic(p.MainWindowHandle)) continue;
                var handle = p.MainWindowHandle; overlayWindows.Add((handle, IsZoomed(handle) ? 3 : 9)); ShowWindowAsync(handle, 6);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
    public void RestoreFromOverlay()
    {
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
    }
    private static List<(int Id, int Parent)> Snapshot()
    {
        List<(int, int)> rows = []; var handle = CreateToolhelp32Snapshot(2, 0); if (handle == -1) return rows;
        try { var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" }; if (Process32First(handle, ref entry)) do { rows.Add(((int)entry.Pid, (int)entry.Parent)); } while (Process32Next(handle, ref entry)); }
        finally { CloseHandle(handle); }
        return rows;
    }
    public async Task Run(GameEntry game, string dataDirectory, IEnumerable<GunBinding> bindings)
    {
        if (Active) throw new InvalidOperationException(I18n.T("Es läuft bereits ein Spiel."));
        _ = LaunchRules.Prepare(game);
        var players = bindings.ToArray();
        MultiplayerSetup.Configure(game, players);
        var helpers = new List<Process>();
        try
        {
            foreach (var helper in game.Helpers ?? [])
            {
                var previous = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(helper.Executable));
                bool running = previous.Length > 0; foreach (var p in previous) p.Dispose();
                if (running) throw new IOException(I18n.T("Der Helfer läuft bereits. Bitte vor dem Start schließen: ") + Path.GetFileName(helper.Executable));
                var info = new ProcessStartInfo(helper.Executable) { WorkingDirectory = helper.WorkingDirectory, UseShellExecute = false };
                foreach (var argument in helper.Arguments) info.ArgumentList.Add(argument);
                helpers.Add(Process.Start(info) ?? throw new IOException(I18n.T("Helfer konnte nicht gestartet werden.")));
            }
            await RunGame(game, dataDirectory, players);
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
                try { if (!helper.HasExited) helper.Kill(true); }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                helper.Dispose();
            }
            Active = false; Changed?.Invoke("ended");
        }
    }
    private async Task RunGame(GameEntry game, string dataDirectory, IEnumerable<GunBinding> bindings)
    {
        if (Active) throw new InvalidOperationException(I18n.T("Es läuft bereits ein Spiel."));
        EmulatorSetup.ConfigurePaths(game);
        TeknoGunSetup.Configure(game,bindings);
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
            // MAME creates its own game window; its console must not cover it.
            info.CreateNoWindow = true;
            string ctrl = Path.Combine(dataDirectory, "controllers"); Directory.CreateDirectory(ctrl);
            File.WriteAllText(Path.Combine(ctrl, "reaper.cfg"), LaunchRules.MameController(bindings));
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
        using var process = Process.Start(info) ?? throw new IOException(I18n.T("Das Spiel konnte nicht gestartet werden."));
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
            while (!stop)
            {
                var snapshot = Snapshot();
                lock (sync)
                {
                    // Include only new processes from this session's tree or the exact configured game executable.
                    foreach (var item in snapshot.Where(p => !existing.Contains(p.Id) && !owned.ContainsKey(p.Id)))
                    {
                        try
                        {
                            using var candidate = Process.GetProcessById(item.Id);
                            if (candidate.StartTime < started.AddSeconds(-1)) continue;
                            bool exact = string.Equals(candidate.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase);
                            if (owned.ContainsKey(item.Parent) || exact && DateTime.Now - started < TimeSpan.FromSeconds(15))
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
                    // Focus each session window once, only during startup and while no overlay is open.
                    if (!overlayVisible && DateTime.Now - started < TimeSpan.FromSeconds(20))
                        foreach (var pair in owned.ToArray())
                            try
                            {
                                using var gameProcess = Process.GetProcessById(pair.Key);
                                if (gameProcess.HasExited || gameProcess.StartTime != pair.Value) continue;
                                var handle = gameProcess.MainWindowHandle;
                                if (handle != 0 && IsWindowVisible(handle) && presented.Add(handle)) { if(IsIconic(handle)) ShowWindowAsync(handle, 9); SetForegroundWindow(handle); }
                            }
                            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                    // Wait for the actual game window, not the launcher or dependency dialog.
                    // Count the ten seconds only after a stable foreground game window exists.
                    if(!legendShown && !overlayVisible && ending is null)
                    {
                        var foreground=GetForegroundWindow();
                        GetWindowThreadProcessId(foreground,out uint foregroundPid);
                        bool ready=false;
                        if(owned.TryGetValue((int)foregroundPid,out var expectedStart))
                            try {
                                using var candidate=Process.GetProcessById((int)foregroundPid);
                                ready=!candidate.HasExited && candidate.StartTime==expectedStart && IsWindowVisible(foreground) && !IsIconic(foreground)
                                    && (string.Equals(candidate.MainModule?.FileName,target,StringComparison.OrdinalIgnoreCase) || game.Source=="steam" && foregroundPid!=process.Id);
                            } catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                        if(!ready) legendWindow=0;
                        else if(legendWindow!=foreground) {legendWindow=foreground;legendSince=Environment.TickCount64;}
                        else if(Environment.TickCount64-legendSince>=750) {legendShown=true;GameWindowReady?.Invoke(foreground);}
                    }
                    if (children > 0) lastChild = DateTime.Now;
                    if (targetSeen && liveTarget == 0 && (game.Source == "teknoparrot" || children == 0)) break;
                    if (descendantSeen && children == 0 && DateTime.Now - lastChild > TimeSpan.FromSeconds(3)) break;
                    if (live == 0 && DateTime.Now - started > TimeSpan.FromSeconds(15)) throw new IOException(I18n.T("Der Starter wurde beendet, aber kein Spielprozess erkannt. Bitte das Profil direkt in seinem Emulator prüfen."));
                    if (!targetSeen && !descendantSeen && DateTime.Now - started > TimeSpan.FromSeconds(30)) throw new IOException(I18n.T("Kein Spielprozess erkannt. TeknoParrot-Profil direkt prüfen."));
                }
                await Task.Delay(200);
            }
            if(!stop && ending is null && game.Source!="teknoparrot" && process.HasExited && process.ExitCode!=0)
                throw new IOException(I18n.F($"Das Spiel wurde mit Fehlercode {process.ExitCode} beendet. Bitte das Emulatorprotokoll prüfen."));
        }
        finally
        {
            if (game.Source == "teknoparrot") await End();
            // The owning wrapper releases this session's helpers before returning to the menu.
        }
    }
    public Task End() => ending ??= EndOwned();
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
        foreach (var pair in list.Reverse())
        {
            try { using var p = Process.GetProcessById(pair.Key); if (!p.HasExited && p.StartTime == pair.Value) p.Kill(); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        stop = true;
    }
}
