using System.Net;
using System.Text;
using System.Xml.Linq;
using Reaper.Core;

int checks = 0;
void Check(bool pass, string label) { if (!pass) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
void Throws(Action action, string label) { try { action(); throw new Exception("Expected error: " + label); } catch (Exception e) when (!e.Message.StartsWith("Expected error")) { checks++; Console.WriteLine("PASS: " + label); } }
string root = Path.Combine(args.FirstOrDefault() ?? Path.GetTempPath(), "reaper-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string tp = Path.Combine(root, "TeknoParrot"); Directory.CreateDirectory(Path.Combine(tp, "UserProfiles")); Directory.CreateDirectory(Path.Combine(tp, "GameProfiles"));
    File.WriteAllText(Path.Combine(tp, "TeknoParrotUi.exe"), ""); File.WriteAllText(Path.Combine(tp, "game & 1.exe"), "");
    File.WriteAllText(Path.Combine(tp, "GameProfiles", "HOTDSD.xml"), "<GameProfile/>");
    File.WriteAllText(Path.Combine(tp, "UserProfiles", "HOTDSD.xml"), "<GameProfile><GamePath>game &amp; 1.exe</GamePath></GameProfile>");
    File.WriteAllText(Path.Combine(tp, "UserProfiles", "MarioKart.xml"), "<GameProfile><GamePath>game &amp; 1.exe</GamePath></GameProfile>");
    File.WriteAllText(Path.Combine(tp, "UserProfiles", "TC5.xml"), "<GameProfile><GamePath>missing.exe</GamePath></GameProfile>");
    File.WriteAllText(Path.Combine(tp, "UserProfiles", "HOTD4.xml"), "<broken>");
    var import = GameImporter.TeknoParrot(tp);
    Check(import.Games.Count == 2, "TeknoParrot imports gun profiles, excludes racer and broken XML");
    var scarlet = import.Games.Single(g => g.Title.Contains("Scarlet"));
    Check(scarlet.Status == "unverified", "Existing profile is unverified, never falsely tested");
    Check(import.Games.Single(g => g.Title.Contains("Crisis")).Status == "needs-setup", "Missing game file produces needs-setup");
    Check(import.Warnings.Count == 3, "Ignored, incomplete and broken profiles report their cause");
    var launch = LaunchRules.Prepare(scarlet);
    Check(launch.ArgumentList.Single() == "--profile=HOTDSD.xml" && launch.WorkingDirectory == tp, "TeknoParrot uses profile basename and correct working directory");
    Throws(() => LaunchRules.Prepare(import.Games.Single(g => g.Title.Contains("Crisis"))), "Incomplete profile cannot launch");
    Throws(() => LaunchRules.Prepare(scarlet with { Source = "demo" }), "Preview entry cannot launch");
    const string xml = "<!DOCTYPE mame [<!ELEMENT mame ANY>]><mame><machine name='pointblank'><description>Point Blank</description><input><control type='lightgun'/></input></machine><machine name='sf2'><description>Street Fighter II</description><input><control type='joystick'/></input></machine><machine name='area51'><description>Area 51</description><input><control type='lightgun'/></input></machine><machine name='device' isdevice='yes'><input><control type='lightgun'/></input></machine></mame>";
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)); var catalog = GameImporter.MameGunCatalog(stream);
    Check(catalog.Count == 2 && catalog.ContainsKey("area51"), "MAME streaming parser preserves consecutive machines and excludes non-gun devices");
    string roms = Path.Combine(root, "roms"); Directory.CreateDirectory(roms); File.WriteAllText(Path.Combine(roms, "pointblank.zip"), ""); File.WriteAllText(Path.Combine(roms, "sf2.zip"), "");
    var mame = GameImporter.Mame(scarlet.Executable, roms, catalog);
    Check(mame.Games.Count == 1 && mame.Games[0].Title == "Point Blank", "MAME combines actual ROM files with emulator gun catalog");
    Check(mame.Games[0].Arguments.Contains("-lightgunprovider") && mame.Games[0].Aspect == "4:3", "MAME imports raw-input profile and 4:3 format");
    var state = LibraryState.Empty; LibraryStore.Merge(state, import.Games); var old = state.Games.Single(g => g.Id == scarlet.Id);
    state.Games[state.Games.IndexOf(old)] = old with { Favorite = true, Status = "tested", Cover = "mine.png" };
    LibraryStore.Merge(state, import.Games);
    Check(state.Games.Count == 2 && state.Games.Single(g => g.Id == scarlet.Id).Favorite, "Reimport deduplicates and retains favorite");
    Check(state.Games.Single(g => g.Id == scarlet.Id).Status == "tested" && state.Games.Single(g => g.Id == scarlet.Id).Cover == "mine.png", "Unchanged launch retains user verdict and cover");
    LibraryStore.Merge(state, [scarlet with { Arguments = ["--profile=changed.xml"] }]);
    Check(state.Games.Single(g => g.Id == scarlet.Id).Status == "unverified", "Changed launch invalidates prior playable verdict");
    var store = new LibraryStore(Path.Combine(root, "data")); store.Save(state); store.Save(state); var cold = store.Load();
    Check(cold.Games.Count == 2 && cold.Games.Single(g => g.Id == scarlet.Id).Favorite, "Library survives cold reload");
    Check(File.Exists(Path.Combine(root, "data", "library.json.bak")), "Saving preserves the previous library snapshot");
    File.WriteAllText(Path.Combine(root, "data", "library.json"), "broken"); Throws(() => store.Load(), "Damaged library fails explicitly and is not overwritten");
    string mouse = @"\\?\HID#VID_1234&PID_4321#player1";
    var controller = XDocument.Parse(LaunchRules.MameController([new(1, mouse)]));
    Check(controller.Descendants("mapdevice").Single().Attribute("device")!.Value == mouse, "MAME mapping retains and XML-escapes exact device identity");
    var gesture = new ExitGesture(); var now = DateTimeOffset.UtcNow; gesture.Key(0x31, true, now); gesture.Key(0x35, true, now);
    Check(!gesture.Ready(now.AddSeconds(1)) && gesture.Ready(now.AddSeconds(2)), "Exit requires a held chord, not coin or start alone");
    gesture.Key(0x35, false, now.AddSeconds(2)); Check(!gesture.Ready(now.AddSeconds(4)), "Releasing chord cancels exit");
    using var http = new HttpClient(new FixtureHttp());
    var cover = await new CoverService(http, Path.Combine(root, "covers")).Download("Point Blank", "fixture", CancellationToken.None);
    Check(cover is not null && File.Exists(cover), "Cover service downloads an exact match and saves local artwork");
    using var ambiguousHttp = new HttpClient(new FixtureHttp(true));
    var ambiguous = await new CoverService(ambiguousHttp, Path.Combine(root, "covers")).Download("Point Blank", "fixture", CancellationToken.None);
    Check(ambiguous is null, "Ambiguous cover titles are not silently guessed");
    LibraryStore.Merge(state, [scarlet with { Status = "needs-setup" }]);
    Check(state.Games.Single(g => g.Id == scarlet.Id).Status == "needs-setup", "Reimport invalidates playable verdict when game files disappear");
    var discoveries = InstallationFinder.Find([root]);
    Check(discoveries.Count == 1 && discoveries[0].Kind == "tekno" && discoveries[0].Path == scarlet.Executable, "Installation search finds nested TeknoParrot and deduplicates paths");
    var browse = InstallationFinder.Browse(tp, false, [".exe"]);
    Check(browse.Entries.Any(e => !e.Directory && e.Path == scarlet.Executable) && !browse.Entries.Any(e => e.Name.EndsWith(".xml")), "Arcade picker filters applications and retains navigable folders");
    var folderPage = InstallationFinder.Browse(tp, true, []);
    Check(folderPage.Entries.All(e => e.Directory) && folderPage.Parent == root, "Folder selection cannot offer files and exposes correct parent");
    Throws(() => InstallationFinder.Browse(Path.Combine(root, "absent"), true, []), "Picker rejects vanished directory");
    Console.WriteLine($"\n{checks} meaningful core checks passed.");
}
finally { Directory.Delete(root, true); }

sealed class FixtureHttp(bool ambiguous = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath;
        HttpContent content = path.Contains("autocomplete") ? new StringContent(ambiguous ? "{\"data\":[{\"id\":1,\"name\":\"Point Blank\"},{\"id\":2,\"name\":\"Point Blank\"}]}" : "{\"data\":[{\"id\":1,\"name\":\"Point Blank\"}]}") :
         path.Contains("grids") ? new StringContent("{\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/fixture.png\"}]}") : new ByteArrayContent([137, 80, 78, 71]);
        if (!path.Contains("api")) content.Headers.ContentType = new("image/png");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
