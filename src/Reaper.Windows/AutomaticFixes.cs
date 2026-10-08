using System.IO;
using System.Security.Principal;
using Reaper.Core;

namespace Reaper.Windows;

internal static class AutomaticFixes
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    public static async Task<FixReport> Run(IEnumerable<GameEntry> games, string data)
    {
        await gate.WaitAsync();
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            bool admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            return await Task.Run(() => KnownFixes.Apply(games, data, admin, package => Prepare(package, data)));
        }
        finally { gate.Release(); }
    }
    private static async Task<string> Prepare(FixPackage package, string data)
    {
        string folder = Path.Combine(data, "repair-packages"); Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, package.Id + ".zip");
        if (KnownFixes.ValidPackage(file, package)) return file;
        string temporary = file + ".download";
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            Uri url = new(package.Url);
            for (int redirects = 0; ; redirects++)
            {
                // Only reviewed vendor download hosts and GitHub release asset redirects.
                if (url.Scheme != "https" || url.Host is not ("openal-soft.org" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new IOException(I18n.T("Die Reparaturquelle gehört nicht zum geprüften Herstellerkatalog."));
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is >= 300 and < 400)
                { if (redirects >= 5 || response.Headers.Location is null) throw new IOException("Invalid repair package redirect."); url = new Uri(url, response.Headers.Location); continue; }
                response.EnsureSuccessStatusCode();
                await using (var output = File.Create(temporary))
                await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
                {
                    byte[] buffer = new byte[81920]; long total = 0; int count;
                    while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
                    { total += count; if (total > package.MaxBytes) throw new IOException("Repair package exceeds its reviewed size limit."); await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token); }
                }
                if (!KnownFixes.ValidPackage(temporary, package)) throw new IOException(I18n.T("Das Reparaturpaket stimmt nicht mit der geprüften Herstellerdatei überein."));
                if (File.Exists(file)) File.Move(file, file + ".invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                File.Move(temporary, file); return file;
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
