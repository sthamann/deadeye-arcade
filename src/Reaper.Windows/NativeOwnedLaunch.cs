using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Reaper.Core;

namespace Reaper.Windows;

// Assign the process to its job before a launcher can create children or exit.
internal static class NativeOwnedLaunch
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public nint Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public ushort Show, ReservedSize; public nint ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public nint Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    { public long ProcessTime, JobTime; public uint Flags; public nuint MinWorkingSet, MaxWorkingSet; public uint ActiveProcessLimit; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters
    { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string application, StringBuilder command, nint processAttributes, nint threadAttributes,
        bool inheritHandles, uint flags, nint environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(nint process, uint code);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(nint job, int info, ref ExtendedLimits limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(nint job, nint process);

    public static nint CreateJob(nint process)
    {
        nint job = CreateJobObject(0, null);
        var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } };
        if (job != 0 && SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()) && AssignProcessToJobObject(job, process)) return job;
        int error = Marshal.GetLastWin32Error(); if (job != 0) CloseHandle(job);
        throw new Win32Exception(error, "Cannot supervise the process tree.");
    }
    public static Process Start(ProcessStartInfo info, Action<nint> assign)
    {
        if (info.UseShellExecute || info.RedirectStandardInput || info.RedirectStandardOutput || info.RedirectStandardError)
            throw new ArgumentException("Owned launch requires direct execution without redirected streams.");
        string arguments = info.ArgumentList.Count > 0 ? string.Join(" ", info.ArgumentList.Select(WindowsArguments.Quote)) : info.Arguments;
        var command = new StringBuilder(WindowsArguments.Quote(info.FileName) + " " + arguments);
        string environment = string.Join('\0', info.Environment.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => p.Key + "=" + p.Value)) + "\0\0";
        nint block = Marshal.StringToHGlobalUni(environment);
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        ProcessInfo handles = default; Process? process = null; bool resumed = false;
        try
        {
            if (!CreateProcessW(info.FileName, command, 0, 0, false, 0x4u | 0x400u | (info.CreateNoWindow ? 0x08000000u : 0), block,
                    string.IsNullOrWhiteSpace(info.WorkingDirectory) ? Environment.CurrentDirectory : info.WorkingDirectory, ref startup, out handles))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            assign(handles.Process);
            process = Process.GetProcessById((int)handles.ProcessId);
            // Open the managed handle while the process is still suspended.
            _ = process.Handle;
            if (ResumeThread(handles.Thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            resumed = true; return process;
        }
        finally
        {
            if (!resumed) { if (handles.Process != 0) TerminateProcess(handles.Process, 1); process?.Dispose(); }
            if (handles.Thread != 0) CloseHandle(handles.Thread);
            if (handles.Process != 0) CloseHandle(handles.Process);
            Marshal.FreeHGlobal(block);
        }
    }
}
