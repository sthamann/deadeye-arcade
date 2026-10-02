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
    [StructLayout(LayoutKind.Sequential)] private struct DeviceList { public nint Device; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] private struct Registration { public ushort Page, Usage; public uint Flags; public nint Window; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public nint Device, WParam; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputDeviceList([Out] DeviceList[]? devices, ref uint count, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetRawInputDeviceInfo(nint device, uint command, StringBuilder? data, ref uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("hid.dll")] private static extern bool HidD_GetProductString(SafeFileHandle device, byte[] buffer, uint length);
    private readonly Dictionary<nint, string> names = [];
    private const uint DeviceName = 0x20000007;
    public void Register(nint window)
    {
        // INPUTSINK observes the guns during a game; it does not inject or steal game inputs.
        Registration[] registration = [new() { Page = 1, Usage = 2, Flags = 0x2100, Window = window }, new() { Page = 1, Usage = 6, Flags = 0x2100, Window = window }];
        if (!RegisterRawInputDevices(registration, 2, (uint)Marshal.SizeOf<Registration>())) throw new Win32Exception(Marshal.GetLastWin32Error());
        Refresh();
    }
    public void Refresh()
    {
        uint count = 0; uint size = (uint)Marshal.SizeOf<DeviceList>();
        if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) return;
        var list = new DeviceList[count];
        uint found = GetRawInputDeviceList(list, ref count, size); if (found == uint.MaxValue) return;
        names.Clear(); List<InputDevice> devices = [];
        foreach (var d in list.Take((int)found))
        {
            uint chars = 0; GetRawInputDeviceInfo(d.Device, DeviceName, null, ref chars);
            if (chars == 0) continue;
            var text = new StringBuilder((int)chars + 1);
            if (GetRawInputDeviceInfo(d.Device, DeviceName, text, ref chars) == uint.MaxValue) continue;
            string id = text.ToString(); names[d.Device] = id;
            string product = "";
            using (var handle = CreateFile(id, 0, 3, 0, 3, 0, 0))
            {
                var buffer = new byte[256];
                if (!handle.IsInvalid && HidD_GetProductString(handle, buffer, (uint)buffer.Length)) product = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            }
            string kind = d.Type == 0 ? "mouse" : d.Type == 1 ? "keyboard" : "hid";
            bool retro = product.Contains("Retro Shooter", StringComparison.OrdinalIgnoreCase) || product.Contains("3AGAME", StringComparison.OrdinalIgnoreCase);
            devices.Add(new(id, string.IsNullOrEmpty(product) ? kind == "mouse" ? "Maus / Lightgun" : kind == "keyboard" ? "Tasteneingang" : "HID-Gerät" : product, kind, retro));
        }
        Devices = devices;
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
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public void Dispose()
    {
        Registration[] off = [new() { Page = 1, Usage = 2, Flags = 1 }, new() { Page = 1, Usage = 6, Flags = 1 }];
        RegisterRawInputDevices(off, 2, (uint)Marshal.SizeOf<Registration>());
    }
}
