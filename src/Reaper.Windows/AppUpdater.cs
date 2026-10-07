using System.Diagnostics;
using System.Reflection;
using Reaper.Core;

namespace Reaper.Windows;

public sealed class AppUpdater : IDisposable
{
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly CancellationTokenSource lifetime = new();
    public static string Current => Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
    public AppUpdater()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ReaperArcade/" + Current);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    public Task<AppRelease?> Check() => AppUpdates.Check(client, AppUpdates.ParseVersion(Current), lifetime.Token);
    public Task<string> Download(AppRelease release, string data, IProgress<int> progress) => AppUpdates.Download(client, release, Path.Combine(data, "updates"), progress, lifetime.Token);
    public static void Install(string installer, string data)
    {
        string library = Path.Combine(data, "library.json");
        if (File.Exists(library)) File.Copy(library, Path.Combine(data, "library.before-update.json"), true);
        var info = new ProcessStartInfo(installer) { UseShellExecute = false, Arguments = "/S /RESTART /D=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) };
        _ = Process.Start(info) ?? throw new IOException("Update installer could not start.");
    }
    public void Dispose() { lifetime.Cancel(); client.Dispose(); lifetime.Dispose(); }
}
