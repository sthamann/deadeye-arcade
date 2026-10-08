using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Reaper.Core;

namespace Reaper.Windows;

public record RawPacket(string DeviceId, string Kind, int Key, bool Down, int Buttons, int X, int Y, bool Absolute, bool VirtualDesktop);
public sealed class RawInput : IDisposable
{
    public event Action<RawPacket>? Packet;
    public event Action? DevicesChanged;
    public List<InputDevice> Devices { get; private set; } = [];
    public List<string> MouseOrder { get; private set; } = [];
    [StructLayout(LayoutKind.Sequential)] private struct DeviceList { public nint Device; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] private struct Registration { public ushort Page, Usage; public uint Flags; public nint Window; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public nint Device, WParam; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputDeviceList([Out] DeviceList[]? devices, ref uint count, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetRawInputDeviceInfo(nint device, uint command, StringBuilder? data, ref uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("hid.dll")] private static extern bool HidD_GetProductString(SafeFileHandle device, byte[] buffer, uint length);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW")] private static extern uint DeviceInfoBytes(nint device, uint command, nint data, ref uint size);
    [DllImport("hid.dll")] private static extern int HidP_GetUsages(int type, ushort page, ushort collection, [Out] ushort[] usages, ref uint count, nint preparsed, byte[] report, uint length);
    private readonly Dictionary<nint, string> names = [];
    private bool registered;
    private readonly Dictionary<string, HashSet<ushort>> hidButtons = new(StringComparer.OrdinalIgnoreCase);
    private const uint DeviceName = 0x20000007;
    public void Register(nint window)
    {
        // INPUTSINK observes the guns during a game; it does not inject or steal game inputs.
        Registration[] registration = [new() { Page = 1, Usage = 2, Flags = 0x2100, Window = window }, new() { Page = 1, Usage = 6, Flags = 0x2100, Window = window }, new() { Page = 1, Usage = 4, Flags = 0x2100, Window = window }, new() { Page = 1, Usage = 5, Flags = 0x2100, Window = window }];
        if (!RegisterRawInputDevices(registration, (uint)registration.Length, (uint)Marshal.SizeOf<Registration>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        registered = true;
        Refresh();
    }
    public void Refresh()
    {
        uint count = 0; uint size = (uint)Marshal.SizeOf<DeviceList>();
        if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) return;
        var list = new DeviceList[count];
        uint found = GetRawInputDeviceList(list, ref count, size); if (found == uint.MaxValue) return;
        names.Clear(); MouseOrder = []; List<InputDevice> devices = [];
        foreach (var d in list.Take((int)found))
        {
            int mouseIndex = MouseOrder.Count;
            if (d.Type == 0) MouseOrder.Add("");
            uint chars = 0; GetRawInputDeviceInfo(d.Device, DeviceName, null, ref chars);
            if (chars == 0) continue;
            var text = new StringBuilder((int)chars + 1);
            if (GetRawInputDeviceInfo(d.Device, DeviceName, text, ref chars) == uint.MaxValue) continue;
            string id = text.ToString(); names[d.Device] = id;
            if (d.Type == 0) MouseOrder[mouseIndex] = id;
            string product = "";
            using (var handle = CreateFile(id, 0, 3, 0, 3, 0, 0))
            {
                var buffer = new byte[256];
                if (!handle.IsInvalid && HidD_GetProductString(handle, buffer, (uint)buffer.Length)) product = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            }
            string kind = d.Type == 0 ? "mouse" : d.Type == 1 ? "keyboard" : "hid";
            bool retro = product.Contains("Retro Shooter", StringComparison.OrdinalIgnoreCase) || product.Contains("3AGAME", StringComparison.OrdinalIgnoreCase);
            devices.Add(new(id, string.IsNullOrEmpty(product) ? kind == "mouse" ? I18n.T("Maus / Lightgun") : kind == "keyboard" ? "Tasteneingang" : I18n.T("HID-Gerät") : product, kind, retro));
        }
        Devices = devices;
        foreach (var id in hidButtons.Keys.Where(id => !devices.Any(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).ToArray()) hidButtons.Remove(id);
    }
    public void Message(int message, nint lParam)
    {
        if (message == 0xFE) { Refresh(); DevicesChanged?.Invoke(); return; }
        if (message != 0xFF) return;
        uint size = 0, headerSize = (uint)Marshal.SizeOf<Header>();
        if (GetRawInputData(lParam, 0x10000003, 0, ref size, headerSize) == uint.MaxValue || size < headerSize) return;
        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, 0x10000003, buffer, ref size, headerSize) == uint.MaxValue) return;
            var h = Marshal.PtrToStructure<Header>(buffer);
            if (!names.TryGetValue(h.Device, out var id)) { Refresh(); if (!names.TryGetValue(h.Device, out id)) return; }
            nint data = buffer + (int)headerSize;
            if (h.Type == 0 && size >= headerSize + 24)
            {
                ushort flags = (ushort)Marshal.ReadInt16(data); int buttons = (ushort)Marshal.ReadInt16(data, 4);
                Packet?.Invoke(new(id, "mouse", 0, false, buttons, Marshal.ReadInt32(data, 12), Marshal.ReadInt32(data, 16), (flags & 1) != 0, (flags & 2) != 0));
            }
            else if (h.Type == 1 && size >= headerSize + 16)
            {
                int flags = (ushort)Marshal.ReadInt16(data, 2); int key = (ushort)Marshal.ReadInt16(data, 6);
                Packet?.Invoke(new(id, "keyboard", key, (flags & 1) == 0, 0, 0, 0, false, false));
            }
            else if (h.Type == 2 && size >= headerSize + 8 && Devices.Any(d => d.Id == id && d.RetroShooter))
                ReadHidButtons(h.Device, id, data, size - headerSize);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private void ReadHidButtons(nint device, string id, nint data, uint available)
    {
        int reportSize = Marshal.ReadInt32(data), reportCount = Marshal.ReadInt32(data, 4);
        if (reportSize <= 0 || reportSize > 4096 || reportCount <= 0 || (long)reportSize * reportCount > available - 8) return;
        uint bytes = 0;
        if (DeviceInfoBytes(device, 0x20000005, 0, ref bytes) == uint.MaxValue || bytes == 0 || bytes > 65536) return;
        nint preparsed = Marshal.AllocHGlobal((int)bytes);
        try
        {
            if (DeviceInfoBytes(device, 0x20000005, preparsed, ref bytes) == uint.MaxValue) return;
            for (int i = 0; i < reportCount; i++)
            {
                var report = new byte[reportSize]; Marshal.Copy(data + 8 + i * reportSize, report, 0, reportSize);
                var usages = new ushort[128]; uint count = (uint)usages.Length;
                if (HidP_GetUsages(0, 9, 0, usages, ref count, preparsed, report, (uint)reportSize) != 0x00110000) continue;
                var current = usages.Take((int)count).ToHashSet();
                if (!hidButtons.TryGetValue(id, out var previous)) previous = [];
                foreach (var usage in previous.Except(current)) Packet?.Invoke(new(id, "hid", usage, false, 0, 0, 0, false, false));
                foreach (var usage in current.Except(previous)) Packet?.Invoke(new(id, "hid", usage, true, 0, 0, 0, false, false));
                hidButtons[id] = current;
            }
        }
        finally { Marshal.FreeHGlobal(preparsed); }
    }
    public void Dispose()
    {
        if (!registered) return;
        registered = false;
        Registration[] off = [new() { Page = 1, Usage = 2, Flags = 1 }, new() { Page = 1, Usage = 6, Flags = 1 }, new() { Page = 1, Usage = 4, Flags = 1 }, new() { Page = 1, Usage = 5, Flags = 1 }];
        RegisterRawInputDevices(off, (uint)off.Length, (uint)Marshal.SizeOf<Registration>());
    }
}
