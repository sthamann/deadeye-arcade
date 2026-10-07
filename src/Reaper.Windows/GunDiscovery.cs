using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Reaper.Core;

namespace Reaper.Windows;

// Windows ContainerId is the physical unit: mouse + keyboard + COM count as one gun.
public static class GunDiscovery
{
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInfo { public uint Size; public Guid Class; public uint Instance; public nint Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct InterfaceInfo { public uint Size; public Guid Class; public uint Flags; public nint Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; public PropertyKey(string format, uint id) { Format = new(format); Id = id; } }
    private static readonly PropertyKey Container = new("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C", 2), BusName = new("540B947E-8B40-45BC-A8A2-6A0B894CBDA2", 4), Friendly = new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 14), Description = new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 2), Problem = new("4340A6C5-93FA-4706-972C-7B648008A5A7", 3);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern nint SetupDiGetClassDevsW(nint guid, string? enumerator, nint window, uint flags);
    [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInfo(nint set, uint index, ref DeviceInfo info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInstanceIdW(nint set, ref DeviceInfo info, StringBuilder buffer, uint size, out uint needed);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDevicePropertyW(nint set, ref DeviceInfo info, ref PropertyKey key, out uint type, byte[] buffer, uint size, out uint needed, uint flags);
    [DllImport("setupapi.dll")] private static extern nint SetupDiOpenDevRegKey(nint set, ref DeviceInfo info, uint scope, uint profile, uint type, uint access);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(nint set);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, EntryPoint = "SetupDiGetClassDevsW")] private static extern nint InterfaceSet(ref Guid guid, string? enumerator, nint window, uint flags);
    [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInterfaces(nint set, nint info, ref Guid guid, uint index, ref InterfaceInfo data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInterfaceDetailW(nint set, ref InterfaceInfo data, nint detail, uint size, out uint needed, nint info);
    private static IEnumerable<InputDevice> Interfaces(string classId, string kind)
    {
        Guid guid = new(classId); nint set = InterfaceSet(ref guid, null, 0, 18); if (set == -1) yield break;
        try
        {
            for (uint i = 0; ; i++)
            {
                var info = new InterfaceInfo { Size = (uint)Marshal.SizeOf<InterfaceInfo>() };
                if (!SetupDiEnumDeviceInterfaces(set, 0, ref guid, i, ref info)) break;
                SetupDiGetDeviceInterfaceDetailW(set, ref info, 0, 0, out uint size, 0);
                if (size < 8 || size > 16384) continue;
                nint buffer = Marshal.AllocHGlobal((int)size);
                string? path = null;
                try { Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6); if (SetupDiGetDeviceInterfaceDetailW(set, ref info, buffer, size, out _, 0)) path = Marshal.PtrToStringUni(buffer + 4); }
                finally { Marshal.FreeHGlobal(buffer); }
                if (!string.IsNullOrWhiteSpace(path)) yield return new(path, "Windows-Geräteschnittstelle", kind, false);
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }
    private record Node(string Instance, string Container, string Name, string? Port, bool Healthy);
    public static PhysicalGun[] Scan(IReadOnlyList<InputDevice> inputs)
    {
        nint set = SetupDiGetClassDevsW(0, null, 0, 6); if (set == -1) throw new IOException("Windows-Geräteabfrage konnte nicht gestartet werden.");
        List<Node> nodes = [];
        try
        {
            for (uint i = 0; ; i++)
            {
                var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(set, i, ref info)) break;
                var instance = new StringBuilder(1024); if (!SetupDiGetDeviceInstanceIdW(set, ref info, instance, 1024, out _)) continue;
                byte[] container = Property(set, ref info, Container); if (container.Length != 16) continue;
                string name = Text(Property(set, ref info, BusName)); if (name == "") name = Text(Property(set, ref info, Friendly)); if (name == "") name = Text(Property(set, ref info, Description));
                string? port = null;
                if (info.Class == new Guid("4D36E978-E325-11CE-BFC1-08002BE10318"))
                {
                    nint key = SetupDiOpenDevRegKey(set, ref info, 1, 0, 1, 0x20019);
                    if (key != -1) { using var registry = RegistryKey.FromHandle(new SafeRegistryHandle(key, true)); port = registry.GetValue("PortName") as string; }
                }
                byte[] problem = Property(set, ref info, Problem);
                nodes.Add(new(instance.ToString(), new Guid(container).ToString(), name, port, problem.Length < 4 || BitConverter.ToUInt32(problem) == 0));
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        var interfaces = inputs.Concat(Interfaces("378DE44C-56EF-11D1-BC8C-00A0C91405DD", "mouse"))
            .Concat(Interfaces("884B96C3-56EF-11D1-BC8C-00A0C91405DD", "keyboard"))
            .Concat(Interfaces("4D1E55B2-F16F-11CF-88CB-001111000030", "hid")).ToArray();
        var result = new List<PhysicalGun>();
        foreach (var group in nodes.GroupBy(n => n.Container))
        {
            var identified = group.FirstOrDefault(n => GunSystems.Identify(n.Name) is not null);
            if (identified is null) continue;
            var matched = interfaces.Where(input => group.Any(n => Normalize(input.Id).Equals(n.Instance, StringComparison.OrdinalIgnoreCase))).GroupBy(input => Normalize(input.Id), StringComparer.OrdinalIgnoreCase).Select(g => g.OrderBy(d => d.Kind == "hid" ? 1 : 0).First()).ToArray();
            string system = GunSystems.Identify(identified.Name)!;
            result.Add(new(group.Key, identified.Name, system, "Windows USB-Produktname + Container-ID", matched.Select(d => d.Id).ToArray(),
                matched.FirstOrDefault(d => d.Kind == "mouse")?.Id, matched.FirstOrDefault(d => d.Kind == "keyboard")?.Id,
                group.Select(n => n.Port).FirstOrDefault(p => p is not null), group.All(n => n.Healthy), group.Where(n => !n.Healthy).Select(n => n.Name + ": Windows-Gerätefehler").Distinct().ToArray(), inputs.Any(d => matched.Any(m => Normalize(m.Id).Equals(Normalize(d.Id), StringComparison.OrdinalIgnoreCase)))));
        }
        return result.ToArray();
    }
    private static string Normalize(string path) => path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..].Split("#{")[0].Replace('#', '\\') : path;
    private static string Text(byte[] bytes) => Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    private static byte[] Property(nint set, ref DeviceInfo info, PropertyKey key)
    { byte[] bytes = new byte[4096]; return SetupDiGetDevicePropertyW(set, ref info, ref key, out _, bytes, (uint)bytes.Length, out uint needed, 0) ? bytes[..(int)needed] : []; }
}
