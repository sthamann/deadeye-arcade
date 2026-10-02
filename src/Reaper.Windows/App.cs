using System.Windows;

namespace Reaper.Windows;

public static class App
{
    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(true, "Local\\ReaperArcade-v1", out bool first);
        if (!first) { MessageBox.Show("Reaper Arcade ist bereits geöffnet.", "Reaper Arcade"); return; }
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "Reaper Arcade"); e.Handled = true; };
        try { app.Run(new ArcadeWindow()); }
        catch (Exception e) { MessageBox.Show(e.Message, "Reaper Arcade konnte nicht starten"); }
    }
}
