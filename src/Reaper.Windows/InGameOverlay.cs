using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Reaper.Core;

namespace Reaper.Windows;

public sealed class InGameOverlay : Window
{
    private readonly List<Button> buttons = [];
    private readonly Action<string> command;
    private int selected;
    private bool busy;
    private bool armed;
    public InGameOverlay(GameEntry game, GameControls controls, IEnumerable<GunBinding> bindings, Action<string> command, nint gameWindow = 0)
    {
        this.command = command;
        Title = I18n.T("Deadeye · Spielmenü"); WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual; Width = 1280; Height = 720; Topmost = true; ShowInTaskbar = false;
        AllowsTransparency = true; Background = new SolidColorBrush(Color.FromArgb(200,12,15,21)); Foreground = Brushes.White;
        var root = new Grid { Margin = new Thickness(28), Width = 1224, Height = 664 };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(Text(I18n.T("DEADEYE · SPIELMENÜ"), 16, Brush(255, 132, 73)));
        title.Children.Add(Text(game.Title, 34, Brushes.White));
        title.Children.Add(Text(game.Platform + " · " + game.Source + I18n.T("   |   Das Spiel wird nicht automatisch pausiert."), 16, Brush(182, 192, 206)));
        root.Children.Add(title);
        var actions = new UniformGrid { Rows = 1, Margin = new Thickness(0, 24, 0, 24) };
        AddAction(actions, I18n.T("Weiter spielen"), "resume"); AddAction(actions, I18n.T("Neu starten"), "restart"); AddAction(actions, I18n.T("Spiel beenden"), "end");
        Grid.SetRow(actions, 1); root.Children.Add(actions);
        var players = new UniformGrid { Rows = 1 };
        var allBindings = bindings.ToArray();
        foreach (int player in new[] { 1, 2 })
        {
            var binding = allBindings.FirstOrDefault(b => b.Player == player);
            var panel = new StackPanel { Margin = new Thickness(22) };
            string model = GunSystems.Catalog.FirstOrDefault(s => s.Id == binding?.SystemId)?.Name ?? I18n.T("Keine Gun zugeordnet");
            panel.Children.Add(Text("P" + player + " · " + model, 23, binding is null ? Brush(240, 130, 130) : Brush(126, 224, 177)));
            panel.Children.Add(Text(I18n.T("Belegung im Spielprofil"), 19, Brushes.White));
            var rows = controls.Rows.Where(r => r.Player == player || r.Player == 0).ToArray();
            if (rows.Length == 0) panel.Children.Add(Text(I18n.T("Keine bestätigte Spielbelegung verfügbar"), 16, Brush(182, 192, 206)));
            if (binding is not null)
            {
                var hints = StartupControls.ForPlayer(controls,binding);
                var hardware = StartupControls.Hardware(binding);
                panel.Children.Add(StartupOverlay.GunDiagram(binding,hints,110));
                var legend = new UniformGrid { Columns = 2 };
                foreach (var hint in hints.Where(h=>h.Resolved))
                {
                    var item = new StackPanel { Margin = new Thickness(3,3,10,3) };
                    string numbers = string.Join(" · ",hint.ControlIds.Select(id=>Array.FindIndex(hardware,c=>c.Id==id)+1));
                    item.Children.Add(Text(numbers+"  "+hint.Function,17,Brushes.White));
                    item.Children.Add(Text(hint.Input,14,Brush(182,192,206)));
                    legend.Children.Add(item);
                }
                panel.Children.Add(legend);
                foreach (var hint in hints.Where(h=>!h.Resolved)) AddRow(panel,hint.Function,hint.Input);
            }
            else foreach (var row in rows) AddRow(panel,row.Function,row.Input);
            var border = new Border { Background = Brush(24, 31, 43), CornerRadius = new CornerRadius(18), Margin = new Thickness(6), Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
            players.Children.Add(border);
        }
        Grid.SetRow(players, 2); root.Children.Add(players);
        var footer = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        footer.Children.Add(Text(controls.Note, 15, Brush(182, 192, 206)));
        footer.Children.Add(Text(I18n.T("Start + Münze 2 Sekunden halten → Spiel beenden. F12 beendet ebenfalls. Abzug 10 Sekunden halten → Menü. Loslassen und Aktion wählen."), 16, Brushes.White));
        Grid.SetRow(footer, 3); root.Children.Add(footer); Content = new Viewbox { Stretch = Stretch.Uniform, Child = root };
        SourceInitialized += (_, _) => OverlayPlacement.Place(this,gameWindow,false);
        Loaded += (_, _) => { OverlayPlacement.Place(this,gameWindow,false); Activate(); Select(0); };
        PreviewKeyDown += (_, e) =>
        {
            // Actual gun packets are dispatched by the launcher. Native keyboard is a fallback.
            string? action = e.Key switch { Key.Escape => "resume", Key.Left or Key.Up => "previous", Key.Right or Key.Down => "next", Key.Enter => "confirm", _ => null };
            if (action is not null) { e.Handled = true; if (!e.IsRepeat) Navigate(action); }
        };
    }
    public void Arm() { armed = true; foreach (var button in buttons) button.IsEnabled = true; }
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    private static TextBlock Text(string value, double size, Brush color) => new() { Text = value, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private static void AddRow(Panel panel, string action, string input)
    {
        panel.Children.Add(new Border { BorderBrush = Brush(53, 63, 81), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 5, 0, 5), Child = Text(action + "  →  " + input, 17, Brushes.White) });
    }
    private void AddAction(Panel panel, string label, string action)
    {
        var button = new Button { Content = label, Tag = action, FontSize = 23, MinHeight = 82, Margin = new Thickness(6), Padding = new Thickness(12), Background = Brush(38, 47, 64), Foreground = Brushes.White, BorderBrush = Brush(110, 126, 149), BorderThickness = new Thickness(2), Cursor = Cursors.Hand };
        button.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
              <Border x:Name="frame" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="12" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="frame" Property="Background" Value="#3B4960" /></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="frame" Property="Background" Value="#6B3C2C" /></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter TargetName="frame" Property="Opacity" Value="0.55" /></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        button.IsEnabled = false; button.Click += (_, _) => Execute(action); buttons.Add(button); panel.Children.Add(button);
    }
    private void Select(int index)
    {
        selected = (index + buttons.Count) % buttons.Count;
        for (int i = 0; i < buttons.Count; i++) buttons[i].BorderBrush = i == selected ? Brush(255, 132, 73) : Brush(110, 126, 149);
        buttons[selected].Focus();
    }
    public void Navigate(string action)
    {
        if (busy || !armed) return;
        if (action is "previous" or "up" or "left") Select(selected - 1);
        else if (action is "next" or "down" or "right") Select(selected + 1);
        else if (action is "confirm" or "start") Execute((string)buttons[selected].Tag);
        else if (action is "resume" or "reload") Execute("resume");
    }
    public void Shoot(Point screenPoint)
    {
        if (busy || !armed) return;
        foreach (var button in buttons)
        {
            Point p = button.PointFromScreen(screenPoint);
            if (p.X >= 0 && p.Y >= 0 && p.X < button.ActualWidth && p.Y < button.ActualHeight) { Execute((string)button.Tag); return; }
        }
    }
    private void Execute(string action)
    {
        if (busy || !armed) return; busy = true;
        foreach (var button in buttons) button.IsEnabled = false;
        command(action);
    }
}
