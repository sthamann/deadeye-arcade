using Reaper.Core;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Reaper.Windows;

public static class GunSoftware
{
    private const string SindenHash = "60261a5a913c80d7a409336a5bc591272ac64ed7351fc6a726e8bd35fd7a8db5";
    private const string XgunnerHash = "e337815747a289fd75c7f7a95343cc39681951f2a6f2513d9050a4a3891099ca";
    public static bool IsPrepared(string id, string data) => id is "sinden" or "xgunner" && File.Exists(Executable(data, id));
    public static object[] Status(string data) => new[] { "sinden", "xgunner" }.Select(id => (object)new
    {
        id, downloaded = File.Exists(Executable(data, id)),
        status = File.Exists(Executable(data, id)) ? I18n.T("Herstellerprogramm vorhanden · Hardwareprüfung offen") : I18n.T("Noch nicht heruntergeladen")
    }).ToArray();
    private static string Executable(string data, string id) => Path.Combine(data, "gun-software", id, id == "sinden" ? "Lightgun.exe" : "XGunner-Config-V260808.exe");
    public static async Task<string> Prepare(string id, string data, Action<string> progress)
    {
        if (id == "blamcon")
        {
            using var steam = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string? root = steam?.GetValue("SteamPath") as string;
            if (root is null || !File.Exists(Path.Combine(root, "steam.exe"))) throw new InvalidOperationException(I18n.T("Blamcon ARC benötigt Steam. Installiere Steam; danach kann dieser Knopf die ARC-Installation öffnen."));
            bool installed = File.Exists(Path.Combine(root, "steamapps", "appmanifest_3324170.acf"));
            Process.Start(new ProcessStartInfo(Path.Combine(root, "steam.exe")) { UseShellExecute = true, Arguments = installed ? "steam://run/3324170" : "steam://install/3324170" });
            return installed ? I18n.T("Blamcon ARC wird geöffnet. Gun anschließen und Kalibrierung in ARC prüfen.") : I18n.T("Steam-Installation von Blamcon ARC geöffnet. Steam-Anmeldung oder Installationsbestätigung gegebenenfalls in Steam abschließen.");
        }
        if (id is not ("sinden" or "xgunner")) throw new ArgumentException(I18n.T("Unbekanntes Softwarepaket."));
        string exe = Executable(data, id), folder = Path.GetDirectoryName(exe)!;
        if (!File.Exists(exe))
        {
            progress(I18n.T("Offizielle ") + (id == "sinden" ? "Sinden" : "X-Gunner") + I18n.T("-Software wird heruntergeladen …"));
            Directory.CreateDirectory(folder);
            string package = Path.Combine(folder, id == "sinden" ? "package.zip" : "package.exe");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ReaperArcade/0.3");
            string url = id == "sinden" ? "https://www.sindenlightgun.com/software/SindenLightgunSoftwareReleaseV2.08b.zip" : "https://hwhxg.com/?xgunner_dl=gui";
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != "https" || response.RequestMessage.RequestUri.Host is not ("www.sindenlightgun.com" or "sindenlightgun.com" or "hwhxg.com")) throw new IOException(I18n.T("Unerwartete Download-Quelle."));
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var target = File.Create(package))
            {
                byte[] buffer = new byte[65536]; long total = 0; int count;
                while ((count = await source.ReadAsync(buffer)) > 0) { total += count; if (total > 350_000_000) throw new IOException(I18n.T("Herstellerpaket ist unerwartet groß.")); await target.WriteAsync(buffer.AsMemory(0, count)); }
            }
            await using (var stream = File.OpenRead(package))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(stream)).Equals(id == "sinden" ? SindenHash : XgunnerHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(I18n.T("Das Herstellerpaket hat sich verändert. Vor Installation muss die neue Version geprüft werden."));
            if (id == "xgunner") File.Move(package, exe, true);
            else
            {
                progress(I18n.T("Sinden Windows-Software wird eingerichtet …"));
                using var zip = ZipFile.OpenRead(package);
                const string prefix = "SindenLightgunSoftwareReleaseV2.08b/SindenLightgunWindowsV2.08/SindenLightgun/";
                long expanded = 0;
                foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    string relative = entry.FullName[prefix.Length..]; if (relative == "") continue;
                    string target = Path.GetFullPath(Path.Combine(folder, relative));
                    if (!target.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000) throw new IOException(I18n.T("Ungültiger Pfad im Softwarepaket."));
                    expanded += entry.Length; if (expanded > 150_000_000) throw new IOException(I18n.T("Softwarepaket entpackt unerwartet viele Daten."));
                    if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(target);
                    else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target, true); }
                }
                // Preserve manufacturer license and recoil confirmations. Only prepare documented detection/border defaults.
                string config = exe + ".config"; var doc = XDocument.Load(config);
                foreach (var key in new[] { "chkAutoDetect", "chkShowPrimaryBorder", "chkShowSecondaryBorder" })
                    doc.Descendants("add").FirstOrDefault(e => (string?)e.Attribute("key") == key)?.SetAttributeValue("value", "1");
                doc.Save(config);
            }
        }
        if (!File.Exists(exe)) throw new IOException(I18n.T("Herstellerprogramm fehlt nach dem Entpacken."));
        progress(I18n.T("Herstellerprogramm wird geöffnet …"));
        bool running = false;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
            using (process)
                try { running |= string.Equals(process.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase); } catch { }
        if (!running) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = folder });
        return I18n.T("Herstellerprogramm geöffnet. Erkennung und Bildschirmrand sind vorbereitet; Herstellerbestätigungen und Zielkalibrierung erfolgen dort. Die App meldet deshalb noch keinen abgeschlossenen Hardwaretest.");
    }
}
