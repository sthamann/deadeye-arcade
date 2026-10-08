using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Reaper.Windows;

internal record DirectInputDevice(Guid Instance, string Name, string Path, int DolphinOrdinal)
{
    public string DolphinDevice => $"DInput/{DolphinOrdinal}/{Name}";
}

internal static class DirectInputDevices
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Instance
    {
        public uint Size; public Guid Id, Product; public uint Type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string InstanceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ProductName;
        public Guid ForceFeedback; public ushort UsagePage, Usage;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumCallback(nint instance, nint context);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumDevices(nint self, uint type, EnumCallback callback, nint context, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateDevice(nint self, ref Guid id, out nint device, nint outer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetProperty(nint self, nint property, nint header);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint Release(nint self);
    [DllImport("dinput8.dll")] private static extern int DirectInput8Create(nint instance, uint version, ref Guid id, out nint input, nint outer);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    private static T Method<T>(nint self, int index) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), index * IntPtr.Size));

    // Match Dolphin's DInput backend: attached game controllers, product name,
    // and ordinal within equal product names. Never assume a global device order.
    public static DirectInputDevice[] Scan()
    {
        var iid = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
        Marshal.ThrowExceptionForHR(DirectInput8Create(GetModuleHandle(null), 0x0800, ref iid, out var input, 0));
        var rows = new List<DirectInputDevice>(); Exception? failure = null;
        try
        {
            EnumCallback callback = (pointer, _) =>
            {
                nint device = 0, property = 0;
                try
                {
                    var instance = Marshal.PtrToStructure<Instance>(pointer);
                    Marshal.ThrowExceptionForHR(Method<CreateDevice>(input, 3)(input, ref instance.Id, out device, 0));
                    property = Marshal.AllocHGlobal(552);
                    Marshal.Copy(new byte[552], 0, property, 552);
                    Marshal.WriteInt32(property, 552); Marshal.WriteInt32(property, 4, 16);
                    string path = Method<GetProperty>(device, 5)(device, 12, property) >= 0 ? Marshal.PtrToStringUni(property + 32) ?? "" : "";
                    Marshal.Copy(new byte[552], 0, property, 552);
                    Marshal.WriteInt32(property, 536); Marshal.WriteInt32(property, 4, 16);
                    string name = Method<GetProperty>(device, 5)(device, 14, property) >= 0 ? Marshal.PtrToStringUni(property + 16) ?? "" : instance.ProductName;
                    name = Regex.Replace(name, @"\s+", " ").Trim();
                    if (name.Length > 0) rows.Add(new(instance.Id, name, path, rows.Count(r => r.Name == name)));
                    return 1;
                }
                catch (Exception error) { failure = error; return 0; }
                finally { if (property != 0) Marshal.FreeHGlobal(property); if (device != 0) Method<Release>(device, 2)(device); }
            };
            Marshal.ThrowExceptionForHR(Method<EnumDevices>(input, 4)(input, 4, callback, 0, 1));
            GC.KeepAlive(callback);
            if (failure is not null) throw new IOException("DirectInput device inspection failed.", failure);
            return rows.ToArray();
        }
        finally { Method<Release>(input, 2)(input); }
    }
}
