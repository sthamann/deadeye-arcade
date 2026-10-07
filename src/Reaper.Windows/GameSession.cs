using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Reaper.Core;

namespace Reaper.Windows;

public sealed class GameSession
{
    private readonly Dictionary<int, DateTime> owned = [];
    private volatile bool stop;
    private readonly object sync = new();
    public bool Active { get; private set; }
    public event Action<string>? Changed;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    { public uint Size, Usage, Pid; public nint Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name; }
    [DllImport("kernel32.dll")] private static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32First(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32Next(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    private static List<(int Id, int Parent)> Snapshot()
    {
        List<(int, int)> rows = []; var handle = CreateToolhelp32Snapshot(2, 0); if (handle == -1) return rows;
        try { var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" }; if (Process32First(handle, ref entry)) do { rows.Add(((int)entry.Pid, (int)entry.Parent)); } while (Process32Next(handle, ref entry)); }
        finally { CloseHandle(handle); }
        return rows;
    }
    public async Task Run(GameEntry game, string dataDirectory, IEnumerable<GunBinding> bindings)
    {
        if (Active) throw new InvalidOperationException("Es läuft bereits ein Spiel.");
        _ = LaunchRules.Prepare(game);
        var helpers = new List<Process>();
        try
        {
            foreach (var helper in game.Helpers ?? [])
            {
                var previous = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(helper.Executable));
                bool running = previous.Length > 0; foreach (var p in previous) p.Dispose();
                if (running) throw new IOException("Der Helfer läuft bereits. Bitte vor dem Start schließen: " + Path.GetFileName(helper.Executable));
                var info = new ProcessStartInfo(helper.Executable) { WorkingDirectory = helper.WorkingDirectory, UseShellExecute = false };
                foreach (var argument in helper.Arguments) info.ArgumentList.Add(argument);
                helpers.Add(Process.Start(info) ?? throw new IOException("Helfer konnte nicht gestartet werden."));
            }
            await RunGame(game, dataDirectory, bindings);
        }
        finally
        {
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
        if (Active) throw new InvalidOperationException("Es läuft bereits ein Spiel.");
        var info = LaunchRules.Prepare(game);
        if (game.Source == "teknoparrot")
        {
            var previous = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(game.Executable));
            bool running = previous.Length > 0; foreach (var p in previous) p.Dispose();
            if (running) throw new InvalidOperationException("Bitte die bereits geöffnete TeknoParrot-Oberfläche schließen. Danach kann Reaper Arcade die eigene Spielsitzung starten.");
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
        var started = DateTime.Now; owned.Clear(); stop = false;
        using var process = Process.Start(info) ?? throw new IOException("Das Spiel konnte nicht gestartet werden.");
        lock (sync) owned[process.Id] = process.StartTime;
        Active = true; Changed?.Invoke("running");
        bool targetSeen = game.Source != "teknoparrot", descendantSeen = false;
        var targets = new HashSet<int>(); if (targetSeen) targets.Add(process.Id);
        var lastChild = DateTime.Now;
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
                    if (children > 0) lastChild = DateTime.Now;
                    if (targetSeen && liveTarget == 0 && (game.Source == "teknoparrot" || children == 0)) break;
                    if (descendantSeen && children == 0 && DateTime.Now - lastChild > TimeSpan.FromSeconds(3)) break;
                    if (live == 0 && DateTime.Now - started > TimeSpan.FromSeconds(15)) throw new IOException("Der Starter wurde beendet, aber kein Spielprozess erkannt. Bitte das Profil direkt in seinem Emulator prüfen.");
                    if (!targetSeen && !descendantSeen && DateTime.Now - started > TimeSpan.FromSeconds(30)) throw new IOException("Kein Spielprozess erkannt. TeknoParrot-Profil direkt prüfen.");
                }
                await Task.Delay(200);
            }
        }
        finally
        {
            if (game.Source == "teknoparrot") try { if (!process.HasExited) process.CloseMainWindow(); } catch (InvalidOperationException) { }
            // The owning wrapper releases this session's helpers before returning to the menu.
        }
    }
    public async Task End()
    {
        if (!Active) return;
        KeyValuePair<int, DateTime>[] list; lock (sync) list = owned.ToArray();
        foreach (var pair in list)
        {
            try { using var p = Process.GetProcessById(pair.Key); if (!p.HasExited && p.StartTime == pair.Value) p.CloseMainWindow(); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        await Task.Delay(1800);
        foreach (var pair in list.Reverse())
        {
            try { using var p = Process.GetProcessById(pair.Key); if (!p.HasExited && p.StartTime == pair.Value) p.Kill(); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        stop = true;
    }
}
