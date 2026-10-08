using System.Runtime.InteropServices;
using System.Text;

namespace Reaper.Windows;

internal static class InputDeviceNames
{
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey(Guid id, uint number) { public Guid Id = id; public uint Number = number; }
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_Interface_PropertyW(string path, ref PropertyKey key, out uint type, byte[]? data, ref uint size, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type, byte[]? data, ref uint size, uint flags);

    // Flycast uses DEVPKEY_NAME, rather than the HID product string, in mapping filenames.
    public static string? Name(string path)
    {
        var instance = new PropertyKey(new("78c34fc8-104a-4aca-9ea4-524d52996e57"), 256);
        uint size = 0;
        if (CM_Get_Device_Interface_PropertyW(path, ref instance, out _, null, ref size, 0) != 26 || size is 0 or >65536) return null;
        byte[] data = new byte[size];
        if (CM_Get_Device_Interface_PropertyW(path, ref instance, out _, data, ref size, 0) != 0) return null;
        if (CM_Locate_DevNodeW(out uint node, Encoding.Unicode.GetString(data).TrimEnd('\0'), 0) != 0) return null;
        var name = new PropertyKey(new("b725f130-47ef-101a-a5f1-02608c9eebac"), 10);
        size = 0;
        if (CM_Get_DevNode_PropertyW(node, ref name, out _, null, ref size, 0) != 26 || size is 0 or >65536) return null;
        data = new byte[size];
        return CM_Get_DevNode_PropertyW(node, ref name, out _, data, ref size, 0) == 0 ? Encoding.Unicode.GetString(data).TrimEnd('\0') : null;
    }
}
