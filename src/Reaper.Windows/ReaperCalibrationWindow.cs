using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Reaper.Core;

namespace Reaper.Windows;

public static class ReaperCalibrationPackage
{
    private const string PackageHash = "2755688a2322bfee97034dc9c2a646a633dbedcfdeb0175db8845e743dfc2cd9";
    internal const string DllHash = "bf3185b301b69c9c837901839cda2b617e4171c3d6b84eca2fc38f2ceaf7e16f";
    public static string PathFor(string data) => System.IO.Path.Combine(data,"gun-software","rs3-calibration","hidapi.dll");
    public static bool Prepared(string data) => Valid(PathFor(data));
    internal static bool Valid(string path) => File.Exists(path) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(DllHash,StringComparison.OrdinalIgnoreCase);
    public static async Task<string> Prepare(string data)
    {
        string dll = PathFor(data); if(Valid(dll)) return dll;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dll)!);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var response = await http.GetAsync("https://drive.google.com/uc?export=download&id=1k2EZkifwLNdcFd3F12mq2v5BkUBlCWyx",HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if(response.RequestMessage?.RequestUri?.Scheme!="https") throw new IOException(I18n.T("Unerwartete Download-Quelle."));
        await using var input = await response.Content.ReadAsStreamAsync();
        using var bytes = new MemoryStream(); byte[] buffer = new byte[65536]; int count;
        while((count=await input.ReadAsync(buffer))>0) { if(bytes.Length+count>2_000_000) throw new IOException(I18n.T("Herstellerpaket ist unerwartet groß.")); bytes.Write(buffer,0,count); }
        if(!Convert.ToHexString(SHA256.HashData(bytes.ToArray())).Equals(PackageHash,StringComparison.OrdinalIgnoreCase)) throw new IOException(I18n.T("Das Herstellerpaket hat sich verändert. Vor Installation muss die neue Version geprüft werden."));
        bytes.Position=0; using var zip = new ZipArchive(bytes);
        var entry = zip.GetEntry("RsCalibration_Windows/Plugins/x86_64/hidapi.dll") ?? throw new IOException(I18n.T("Kalibrierungsmodul fehlt."));
        string temporary=dll+".new"; entry.ExtractToFile(temporary,true);
        if(!Valid(temporary)) throw new IOException(I18n.T("Kalibrierungsmodul wurde nicht bestätigt."));
        File.Move(temporary,dll,true); return dll;
    }
}

public sealed class ReaperCalibrationWindow : Window
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Clean(int vid,int pid);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Write(int vid,int pid,byte[] command,int count);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window,nint after,int x,int y,int w,int h,uint flags);
    private readonly ReaperCalibration plan = new();
    private readonly GunBinding binding;
    private readonly Func<bool> connected;
    private readonly TextBlock heading = new() { Foreground=Brushes.White,FontSize=28,TextAlignment=TextAlignment.Center,Margin=new(20),TextWrapping=TextWrapping.Wrap };
    private readonly Canvas targets = new();
    private nint module;
    private readonly Clean clean;
    private readonly Write write;
    private bool started, disposed, complete;
    private readonly int pid;
    public bool CommandsSent => complete;
    public ReaperCalibrationWindow(Window owner,GunBinding binding,string dll,Func<bool> connected)
    {
        if(!ReaperCalibrationPackage.Valid(dll)) throw new IOException(I18n.T("Kalibrierungsmodul wurde nicht bestätigt."));
        this.binding=binding; this.connected=connected; pid=0x574f+binding.Player;
        if(binding.SystemId!="rs3" || binding.Player is <1 or >2 || !binding.MouseId.Contains($"vid_0483&pid_{pid:x4}",StringComparison.OrdinalIgnoreCase)) throw new IOException(I18n.T("Gun-ID und Spielerzuordnung stimmen nicht überein."));
        module=NativeLibrary.Load(dll);
        try { clean=Marshal.GetDelegateForFunctionPointer<Clean>(NativeLibrary.GetExport(module,"hhh_hid_clean")); write=Marshal.GetDelegateForFunctionPointer<Write>(NativeLibrary.GetExport(module,"hhh_hid_write")); }
        catch { NativeLibrary.Free(module); module=0; throw; }
        Owner=owner; Title=I18n.T("RS3 Bildschirmkalibrierung"); WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; Topmost=true; Background=new SolidColorBrush(Color.FromRgb(8,14,24));
        var grid = new Grid(); grid.Children.Add(targets); heading.VerticalAlignment=VerticalAlignment.Center; grid.Children.Add(heading);
        var cancel = new Button { Content=I18n.T("Abbrechen · Grifftaste / Escape"),FontSize=20,Padding=new(24,15,24,15),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(25) };
        cancel.Click+=(_,_)=>Close(); grid.Children.Add(cancel); Content=grid;
        heading.Text=I18n.F($"P{binding.Player} · RS3 kalibrieren")+"\n\n"+I18n.T("Vier IR-Punkte und 24-V-Versorgung anschließen. In deiner Spielposition stehen. Erst den Abzug drücken, dann jedes Ziel genau anvisieren und einmal schießen. Immer vollständig loslassen. Andere Guns nicht verwenden.")+"\n\n"+I18n.T("Nur in 16:9 kalibrieren. Danach wird 4:3 beim Spielstart automatisch umgerechnet.");
        SourceInitialized+=(_,_)=> { var info=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()}; if(GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(owner).Handle,2),ref info)) SetWindowPos(new WindowInteropHelper(this).Handle, -1,info.Monitor.Left,info.Monitor.Top,info.Monitor.Right-info.Monitor.Left,info.Monitor.Bottom-info.Monitor.Top,0x40); };
        SizeChanged+=(_,_)=>Draw();
        KeyDown+=(_,e)=> { if(e.Key==System.Windows.Input.Key.Escape) Close(); };
        Closed+=(_,_)=>DisposeTransport();
    }
    public void Input(RawPacket packet)
    {
        if(disposed || packet.Kind!="mouse" || !packet.DeviceId.Equals(binding.MouseId,StringComparison.OrdinalIgnoreCase)) return;
        if((packet.Buttons&4)!=0) { Close(); return; }
        if((packet.Buttons&2)!=0) plan.Trigger(false);
        if((packet.Buttons&1)==0) return;
        try
        {
            if(!connected()) throw new IOException(I18n.T("Die ausgewählte Gun ist nicht mehr verbunden."));
            if(!started) { clean(0x0483,pid); started=true; }
            if(plan.Trigger(true) is not byte command) return;
            // Vendor DLL encodes commands through HID enumeration and always returns zero.
            // It cannot acknowledge calibration success; require the separate aim test.
            write(0x0483,pid,[command],1);
            if(plan.Saved) {complete=true;Close();} else Draw();
        }
        catch { Close(); throw; }
    }
    private void Draw()
    {
        targets.Children.Clear(); if(plan.Step<0 || plan.Saved) return;
        heading.VerticalAlignment=VerticalAlignment.Top; heading.FontSize=22;
        heading.Text=I18n.F($"P{binding.Player} · Ziel {plan.Step+1} / 4")+" · "+I18n.T("Genau anvisieren, einmal schießen, loslassen.");
        var point=ReaperCalibration.Targets[plan.Step]; double x=ActualWidth*point.X,y=ActualHeight*point.Y;
        var ring=new Ellipse { Width=64,Height=64,Stroke=Brushes.Cyan,StrokeThickness=4 }; Canvas.SetLeft(ring,x-32);Canvas.SetTop(ring,y-32);targets.Children.Add(ring);
        foreach(var line in new[]{new Line {X1=x-45,Y1=y,X2=x+45,Y2=y},new Line {X1=x,Y1=y-45,X2=x,Y2=y+45}}) { line.Stroke=Brushes.White;line.StrokeThickness=2;targets.Children.Add(line); }
        var dot=new Ellipse {Width=6,Height=6,Fill=Brushes.White};Canvas.SetLeft(dot,x-3);Canvas.SetTop(dot,y-3);targets.Children.Add(dot);
    }
    private void DisposeTransport()
    {
        if(disposed) return; disposed=true;
        try { if(started) { if(!complete) write(0x0483,pid,[0xBA],1); clean(0x0483,pid); } }
        finally { if(module!=0) { NativeLibrary.Free(module);module=0; } }
    }
}
