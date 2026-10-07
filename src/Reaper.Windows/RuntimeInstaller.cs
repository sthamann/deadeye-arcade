using System.Diagnostics;
using System.IO;
using System.Text;
using Reaper.Core;

namespace Reaper.Windows;

public static class RuntimeInstaller
{
    public static DependencyReport Scan(IEnumerable<GameEntry> games) => DependencyScanner.Scan(games,
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), ManagedInstalled);
    private static bool ManagedInstalled(string architecture, string framework, Version required)
    {
        string folder = Environment.GetFolderPath(architecture == "x86" ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles);
        string shared = Path.Combine(folder, "dotnet", "shared", framework);
        if (!Directory.Exists(shared)) return false;
        return Directory.GetDirectories(shared).Any(d => Version.TryParse(Path.GetFileName(d), out var version) && version.Major == required.Major && version.Minor == required.Minor && version >= required && File.Exists(Path.Combine(d, framework == "Microsoft.WindowsDesktop.App" ? "PresentationFramework.dll" : "System.Private.CoreLib.dll")));
    }
    public static async Task<int> Install(RuntimePackage package, string dataDirectory, Action<string> progress, Action? showInstaller = null)
    {
        string directory = Path.Combine(dataDirectory, "installers"); Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, package.Id + "-" + Guid.NewGuid().ToString("N") + ".exe");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        Uri url = new(package.Url);
        progress(package.Name + I18n.T(" wird von Microsoft geladen …"));
        for (int redirects = 0; ; redirects++)
        {
            if (url.Scheme != "https" || url.Host is not ("aka.ms" or "download.microsoft.com" or "download.visualstudio.microsoft.com" or "builds.dotnet.microsoft.com")) throw new IOException(I18n.T("Die Download-Adresse gehört nicht zum freigegebenen Microsoft-Katalog."));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if ((int)response.StatusCode is >= 300 and < 400)
            { if (redirects >= 5 || response.Headers.Location is null) throw new IOException(I18n.T("Ungültige Download-Weiterleitung.")); url = new Uri(url, response.Headers.Location); continue; }
            response.EnsureSuccessStatusCode();
            await using var output = File.Create(file); await using var input = await response.Content.ReadAsStreamAsync();
            byte[] buffer = new byte[81920]; long bytes = 0; int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            { bytes += read; if (bytes > 100_000_000) throw new IOException(I18n.T("Der Installer ist unerwartet groß.")); await output.WriteAsync(buffer.AsMemory(0, read)); }
            break;
        }
        progress(I18n.T("Microsoft-Signatur wird geprüft …"));
        // Windows validates the entire Authenticode signature including its trust chain and timestamp.
        string script = "$s=Get-AuthenticodeSignature -LiteralPath '" + file.Replace("'", "''") + "'; if($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch '(^|, )O=Microsoft Corporation(,|$)'){exit 1}; exit 0";
        var verify = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) verify.ArgumentList.Add(arg);
        using (var process = Process.Start(verify) ?? throw new IOException(I18n.T("Signaturprüfung startet nicht.")))
        { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2)); if (process.ExitCode != 0) throw new IOException(I18n.T("Die gültige Microsoft-Signatur konnte nicht bestätigt werden. Installation abgebrochen.")); }
        progress(package.Name + I18n.T(": Bitte den Microsoft-Installer abschließen. Lizenz und Windows-Abfrage erscheinen dort."));
        // Interactive vendor UI keeps acceptance of the license with the user.
        showInstaller?.Invoke();
        using var installer = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, WorkingDirectory = directory }) ?? throw new IOException(I18n.T("Der Installer startet nicht."));
        await installer.WaitForExitAsync(); return installer.ExitCode;
    }
}
