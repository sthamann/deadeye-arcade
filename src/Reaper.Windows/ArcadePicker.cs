using Reaper.Core;

namespace Reaper.Windows;

// The same large buttons used by the arcade menu also choose files and folders.
public sealed class ArcadePicker(Action<string, object> send)
{
    private TaskCompletionSource<string?>? pending;
    private string title = "";
    private bool folders;
    private string[] extensions = [];
    public bool Active => pending is not null;
    public Task<string?> Open(string heading, bool foldersOnly, string[]? accepted = null)
    {
        if (Active) throw new InvalidOperationException("Die Dateiauswahl ist bereits geöffnet.");
        title = heading; folders = foldersOnly; extensions = accepted ?? [".exe"];
        pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try { Browse(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); }
        catch { Cancel(); throw; }
        return pending.Task;
    }
    public void Browse(string path)
    {
        if (!Active) return;
        var page = InstallationFinder.Browse(path, folders, extensions);
        var shortcuts = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(d => new BrowseEntry(d.Name, d.RootDirectory.FullName, true)).ToList();
        foreach (var special in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.UserProfile })
        {
            string folder = Environment.GetFolderPath(special);
            if (Directory.Exists(folder)) shortcuts.Add(new(Path.GetFileName(folder), folder, true));
        }
        send("picker", new { title, foldersOnly = folders, page, shortcuts });
    }
    public void Choose(string path)
    {
        if (!Active) return;
        if (folders ? !Directory.Exists(path) : !File.Exists(path) || !extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Bitte eine passende Datei oder einen vorhandenen Ordner auswählen.");
        var result = pending; pending = null; send("picker", new { closed = true }); result!.TrySetResult(Path.GetFullPath(path));
    }
    public void Cancel() { var result = pending; pending = null; send("picker", new { closed = true }); result?.TrySetResult(null); }
}
