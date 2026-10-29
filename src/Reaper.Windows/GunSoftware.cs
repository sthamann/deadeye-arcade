using Reaper.Core;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Reaper.Windows;

public sealed class GunSetupActionRequiredException(string message) : InvalidOperationException(message);

public static class GunSoftware
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> preparation = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,DependencyReport> dependencyReports = new();
    public static void SetStatus(string id,string status)=>preparation[id]=status;
    private const string SindenHash = "60261a5a913c80d7a409336a5bc591272ac64ed7351fc6a726e8bd35fd7a8db5";
    private const string XgunnerHash = "4a5fe747e40ec9b21be371cc308eaee131c9445d79ef58469f3ca626fb520d94";
    private static string? SteamRoot()
    { using var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); return key?.GetValue("SteamPath") as string; }
    private static bool BlamconInstalled()
    {
        string? root=SteamRoot(); if(root is null)return false;
        var roots=new List<string>{root}; string vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");
        if(File.Exists(vdf)) roots.AddRange(Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s*\"([^\"]+)\"").Select(m=>m.Groups[1].Value.Replace("\\\\","\\")));
        return roots.Any(r=>File.Exists(Path.Combine(r,"steamapps","appmanifest_3324170.acf")));
    }
    public static bool IsPrepared(string id, string data) => id == "rs3" ? ReaperCalibrationPackage.Prepared(data) : id == "blamcon" ? BlamconInstalled() : id is "sinden" or "xgunner" && File.Exists(Executable(data, id));
    public static object[] Status(string data) => new[] { "rs3", "sinden", "xgunner", "blamcon" }.Select(id => (object)new
    {
        id, downloaded = IsPrepared(id,data),
        dependencies = dependencyReports.TryGetValue(id,out var report) ? new { report.CheckedBinaries, missing = report.Findings.Count(f=>f.Missing&&f.Required), packages = report.MissingPackages, uncheckedFiles = report.Unchecked.Length } : null,
        status = preparation.GetValueOrDefault(id) ?? (IsPrepared(id,data) ? I18n.T("Herstellerprogramm vorhanden · Hardwareprüfung offen") : I18n.T("Noch nicht heruntergeladen"))
    }).ToArray();
    private static string Executable(string data, string id) => Path.Combine(data, "gun-software", id, id == "sinden" ? "Lightgun.exe" : "XGunner-Config-V261009.exe");
    public static async Task<string> Prepare(string id, string data, Action<string> progress, bool launch = true)
    {
        if (id == "blamcon")
        {
            using var steam = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            string? root = steam?.GetValue("SteamPath") as string;
            if (root is null || !File.Exists(Path.Combine(root, "steam.exe"))) throw new InvalidOperationException(I18n.T("Blamcon ARC benötigt Steam. Installiere Steam; danach kann dieser Knopf die ARC-Installation öffnen."));
            bool installed = BlamconInstalled();
            if (!launch) return installed ? I18n.T("Blamcon ARC ist installiert. Hardwareprüfung offen.") : I18n.T("Blamcon ARC kann über den Einrichten-Knopf installiert werden.");
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
                foreach (var key in new[] { "chkAutoDetect", "chkAutoStart", "chkShowPrimaryBorder", "chkShowSecondaryBorder" })
                    doc.Descendants("add").FirstOrDefault(e => (string?)e.Attribute("key") == key)?.SetAttributeValue("value", "1");
                doc.Save(config);
            }
        }
        if (!File.Exists(exe)) throw new IOException(I18n.T("Herstellerprogramm fehlt nach dem Entpacken."));
        await PrepareDependencies(id,exe,data,progress);
        if (!launch) return I18n.T("Herstellerprogramm vorhanden · Hardwareprüfung offen");
        progress(I18n.T("Herstellerprogramm wird geöffnet …"));
        bool running = false;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
            using (process)
                try { running |= string.Equals(process.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase); } catch { }
        if (!running) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = folder });
        return I18n.T("Herstellerprogramm geöffnet. Erkennung und Bildschirmrand sind vorbereitet; Herstellerbestätigungen und Zielkalibrierung erfolgen dort. Die App meldet deshalb noch keinen abgeschlossenen Hardwaretest.");
    }
    private static async Task PrepareDependencies(string id,string executable,string data,Action<string> progress)
    {
        var program = new GameEntry("gun-software-"+id,id,"Gun software",executable,[],Path.GetDirectoryName(executable)!,"custom",executable);
        var report = await Task.Run(()=>RuntimeInstaller.Scan([program]));
        dependencyReports[id] = report;
        foreach (string packageId in report.Findings.Where(f=>f.Missing&&f.Required&&f.PackageId is not null).Select(f=>f.PackageId!).Distinct())
        {
            var package = RuntimeCatalog.Get(packageId); if(package is null) continue;
            int code = await RuntimeInstaller.Install(package,data,progress);
            if(code is not (0 or 3010)) throw new GunSetupActionRequiredException(I18n.T("Laufzeitinstallation nicht abgeschlossen: ")+package.Name);
        }
        report = await Task.Run(()=>RuntimeInstaller.Scan([program]));dependencyReports[id]=report;
        if(report.Findings.Any(f=>f.Missing&&f.Required)) throw new GunSetupActionRequiredException(I18n.T("Herstellerprogramm benötigt noch Laufzeiten. Einrichtung erneut starten."));
    }
}
