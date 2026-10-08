using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Reaper.Core;
using DrawingPath = System.Windows.Shapes.Path;

namespace Reaper.Windows;

// A passive, click-through legend. Never use HideForOverlay or Activate here:
// the game retains foreground input throughout the ten-second introduction.
public sealed class StartupOverlay : Window
{
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromMilliseconds(100)};
    private readonly Stopwatch elapsed=new();
    private readonly TextBlock countdown;
    private readonly ProgressBar progress;
    private HwndSource? source;
    private static readonly Brush Muted=ColorBrush("#a7bbd2"),Accent=ColorBrush("#ff894f");
    [StructLayout(LayoutKind.Sequential)] private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint handle,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint handle,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint handle);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint handle,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint handle,int index,nint value);
    public StartupOverlay(GameEntry game,GameControls controls,IEnumerable<GunBinding> bindings,nint gameWindow,Func<bool> gameForeground)
    {
        Title="Deadeye · "+I18n.T("Spielsteuerung");
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true;
        ShowActivated=false; ShowInTaskbar=false; Focusable=false; IsHitTestVisible=false;
        WindowStartupLocation=WindowStartupLocation.Manual;
        var monitor=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};
        if(!GetMonitorInfo(MonitorFromWindow(gameWindow,2),ref monitor)) throw new IOException("Cannot locate game monitor.");
        double dpi=Math.Max(96,GetDpiForWindow(gameWindow))/96d;
        double screenWidth=(monitor.Monitor.Right-monitor.Monitor.Left)/dpi,screenHeight=(monitor.Monitor.Bottom-monitor.Monitor.Top)/dpi;
        Width=Math.Min(1320,screenWidth-48); Height=Math.Min(720,screenHeight*.67);
        Left=monitor.Monitor.Left/dpi+24; Top=monitor.Monitor.Bottom/dpi-Height-24;
        var panel=new StackPanel {Margin=new Thickness(26)};
        var header=new DockPanel(); countdown=Text("10s",20,Accent); countdown.HorizontalAlignment=HorizontalAlignment.Right;
        DockPanel.SetDock(countdown,Dock.Right); header.Children.Add(countdown);
        header.Children.Add(Text("DEADEYE  /  "+I18n.T("SPIELSTEUERUNG"),18,Accent)); panel.Children.Add(header);
        panel.Children.Add(Text(game.Title,30,Brushes.White));
        panel.Children.Add(Text(game.Platform+" · "+I18n.T("Du kannst sofort spielen."),15,Muted));
        var players=new UniformGrid {Rows=1,Margin=new Thickness(0,18,0,12)};
        var all=bindings.Where(b=>b.Player is 1 or 2).OrderBy(b=>b.Player).ToArray();
        var mapped=all.Where(b=>StartupControls.ForPlayer(controls,b).Length>0).ToArray();
        var displayed=mapped.Length>0?mapped:all;
        foreach(var binding in displayed) players.Children.Add(PlayerCard(controls,binding,displayed.Length==1));
        if(all.Length==0) players.Children.Add(Text(I18n.T("Keine Gun zugeordnet. Spielbelegung in Meine Guns einrichten."),20,Muted));
        panel.Children.Add(players);
        foreach(var binding in all.Except(displayed)) panel.Children.Add(Text("P"+binding.Player+" · "+(GunSystems.Catalog.FirstOrDefault(s=>s.Id==binding.SystemId)?.Name??binding.SystemId)+" · "+I18n.T("Spielbelegung für diesen Spieler noch nicht bestätigt."),17,Accent));
        // Keep provenance visible, including Dolphin's unverified independent P2 status.
        panel.Children.Add(Text(controls.Note,14,Muted));
        panel.Children.Add(Text(I18n.T("Abzug 10 Sekunden halten → Spielmenü · danach loslassen"),17,Brushes.White));
        progress=new ProgressBar {Height=3,Maximum=StartupControls.DurationMilliseconds,Value=StartupControls.DurationMilliseconds,Foreground=Accent,Background=ColorBrush("#34465d"),Margin=new Thickness(0,12,0,0)};
        panel.Children.Add(progress);
        var border=new Border {Background=ColorBrush("#ee101924"),BorderBrush=ColorBrush("#536781"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Child=panel};
        Content=new Viewbox {Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=border}; border.Width=1270;
        SourceInitialized+=(_,_)=> {
            var handle=new WindowInteropHelper(this).Handle;
            // Layered + transparent passes mouse hits to windows underneath. NOACTIVATE
            // also protects against keyboard/accessibility focus and clicks.
            SetWindowLongPtr(handle,-20,GetWindowLongPtr(handle,-20)|0x08000000|0x00000020|0x00000080);
            source=HwndSource.FromHwnd(handle); source.AddHook(Hook);
        };
        Loaded+=(_,_)=> {elapsed.Restart();timer.Start();};
        timer.Tick+=(_,_)=> {
            long remaining=StartupControls.DurationMilliseconds-elapsed.ElapsedMilliseconds;
            if(remaining<=0||!gameForeground()) {Close();return;}
            countdown.Text=(int)Math.Ceiling(remaining/1000d)+"s"; progress.Value=remaining;
        };
        Closed+=(_,_)=> {timer.Stop();elapsed.Stop();source?.RemoveHook(Hook);};
    }
    private static nint Hook(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==0x21) {handled=true;return 3;} // MA_NOACTIVATE
        if(message==0x84) {handled=true;return -1;} // HTTRANSPARENT
        return 0;
    }
    private static UIElement PlayerCard(GameControls controls,GunBinding binding,bool wide)
    {
        var hints=StartupControls.ForPlayer(controls,binding);
        var panel=new StackPanel {Margin=new Thickness(12)};
        string model=GunSystems.Catalog.FirstOrDefault(s=>s.Id==binding.SystemId)?.Name??binding.SystemId;
        panel.Children.Add(Text("P"+binding.Player+" · "+model,21,ColorBrush("#86dfb4")));

        if(hints.Length==0) panel.Children.Add(Text(I18n.T("Spielbelegung für diesen Spieler noch nicht bestätigt."),18,Accent));
        var hardware=StartupControls.Hardware(binding);
        // Two columns keep every profile action visible without scrolling.
        var rows=new UniformGrid {Columns=2};
        foreach(var hint in hints) {
            var item=new StackPanel {Margin=new Thickness(3,5,10,5)};
            string numbers=string.Join(" · ",hint.ControlIds.Select(id=>Array.FindIndex(hardware,c=>c.Id==id)+1));
            item.Children.Add(Text((hint.Resolved?numbers+"  ":"?  ")+hint.Function,18,hint.Resolved?Brushes.White:Accent));
            item.Children.Add(Text(hint.Input,16,Muted)); rows.Children.Add(item);
        }
        var body=new Grid();
        body.ColumnDefinitions.Add(new() {Width=wide?new GridLength(360):new GridLength(1,GridUnitType.Star)});
        if(wide) body.ColumnDefinitions.Add(new() {Width=new GridLength(1,GridUnitType.Star)});
        else {body.RowDefinitions.Add(new() {Height=GridLength.Auto});body.RowDefinitions.Add(new() {Height=GridLength.Auto});}
        var diagram=GunDiagram(binding,hints); body.Children.Add(diagram);
        if(wide) Grid.SetColumn(rows,1);else Grid.SetRow(rows,1);
        body.Children.Add(rows);panel.Children.Add(body);
        return new Border {Background=ColorBrush("#1b2939"),CornerRadius=new CornerRadius(12),Margin=new Thickness(5),Child=panel};
    }
    private static UIElement GunDiagram(GunBinding binding,StartupControlHint[] hints)
    {
        var canvas=new Canvas {Width=560,Height=290};
        using var resource=typeof(StartupOverlay).Assembly.GetManifestResourceStream("Reaper.Windows.GunArtwork.json")!;
        var artwork=JsonSerializer.Deserialize<Dictionary<string,ArtworkPath[]>>(resource)!;
        if(artwork.TryGetValue(binding.SystemId,out var paths)) foreach(var path in paths) {
            Brush? fill=path.fill=="none"?null:path.fill.Length>0?ColorBrush(path.fill):new LinearGradientBrush(Color.FromRgb(82,100,121),Color.FromRgb(18,29,43),90);
            canvas.Children.Add(new DrawingPath {Data=Geometry.Parse(path.d),Fill=fill,Stroke=path.stroke.Length>0?ColorBrush(path.stroke):ColorBrush("#9daec4"),StrokeThickness=double.Parse(path.width,System.Globalization.CultureInfo.InvariantCulture),StrokeLineJoin=PenLineJoin.Round});
        }
        var hardware=StartupControls.Hardware(binding); var active=hints.Where(h=>h.Resolved).SelectMany(h=>h.ControlIds).ToHashSet();
        for(int i=0;i<hardware.Length;i++) {
            var control=hardware[i]; bool used=active.Contains(control.Id);
            var marker=new Border {Width=24,Height=24,CornerRadius=new CornerRadius(12),Background=used?Accent:ColorBrush("#263c52"),BorderBrush=ColorBrush("#d1e0f0"),BorderThickness=new Thickness(1),Child=new TextBlock {Text=(i+1).ToString(),FontSize=12,FontWeight=FontWeights.Bold,Foreground=used?ColorBrush("#102031"):Muted,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
            Canvas.SetLeft(marker,control.X-12);Canvas.SetTop(marker,control.Y-12);canvas.Children.Add(marker);
        }
        return new Viewbox {Height=230,Stretch=Stretch.Uniform,Child=canvas};
    }
    private record ArtworkPath(string d,string fill,string stroke,string width);
    private static SolidColorBrush ColorBrush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Text(string text,double size,Brush color)=>new() {Text=text,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)};
}
