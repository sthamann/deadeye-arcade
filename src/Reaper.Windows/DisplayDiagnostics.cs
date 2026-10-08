using System.Runtime.InteropServices;
using System.Text.Json;
using Reaper.Core;

namespace Reaper.Windows;

internal record DisplayMode(int Width, int Height, int Hz);
internal record DisplayOutput(string Device, string Adapter, bool Primary, DisplayMode? Current, DisplayMode? Saved);
internal record DisplaySnapshot(DateTimeOffset Time, bool Remote, DisplayOutput[] Outputs);

// Current Windows session modes only. RDP modes are never presented as physical monitor measurements.
internal static class DisplayDiagnostics
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? name, int number, ref DisplayDevice device, int flags);
    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string name, int mode, nint data);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW")]
    private static extern bool WTSQuerySessionInformation(nint server, int session, int information, out nint buffer, out int bytes);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(nint buffer);
    private static string lastSignature = "";
    private static DisplayMode? Mode(string name, int mode)
    {
        // Unicode DEVMODEW, fixed 220-byte native layout including its display union.
        var data = Marshal.AllocHGlobal(220);
        try
        {
            for (int i = 0; i < 220; i++) Marshal.WriteByte(data, i, 0);
            Marshal.WriteInt16(data, 68, 220);
            return EnumDisplaySettings(name, mode, data) ? new(Marshal.ReadInt32(data, 172), Marshal.ReadInt32(data, 176), Marshal.ReadInt32(data, 184)) : null;
        }
        finally { Marshal.FreeHGlobal(data); }
    }
    private static bool Remote()
    {
        if (WTSQuerySessionInformation(0, -1, 16, out var buffer, out var bytes))
        {
            try { if (bytes >= 2) return Marshal.ReadInt16(buffer) != 0; }
            finally { WTSFreeMemory(buffer); }
        }
        return GetSystemMetrics(0x1000) != 0;
    }
    public static DisplaySnapshot Capture(string data)
    {
        var outputs = new List<DisplayOutput>();
        for (int i = 0; i < 30; i++)
        {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, i, ref device, 0)) break;
            if ((device.Flags & 1) == 0) continue;
            outputs.Add(new(device.Name, device.Description, (device.Flags & 4) != 0, Mode(device.Name, -1), Mode(device.Name, -2)));
        }
        var snapshot = new DisplaySnapshot(DateTimeOffset.Now, Remote(), outputs.ToArray());
        string signature = JsonSerializer.Serialize(new { snapshot.Remote, snapshot.Outputs }, JsonDefaults.Options);
        if (signature == lastSignature) return snapshot;
        try
        {
            Directory.CreateDirectory(data);
            string json = JsonSerializer.Serialize(snapshot, JsonDefaults.Options);
            string file = Path.Combine(data, "display-report.json"), temporary = file + "." + Environment.ProcessId + ".new";
            File.WriteAllText(temporary, json); File.Move(temporary, file, true);
            File.AppendAllText(Path.Combine(data, "display-history.jsonl"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = false }) + Environment.NewLine);
            lastSignature = signature;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Retry later; diagnostics must never prevent exiting a game. */ }
        return snapshot;
    }
}
