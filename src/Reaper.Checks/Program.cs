using System.Net;
using System.Text;
using System.Xml.Linq;
using Reaper.Core;

int checks = 0;
Check(I18n.Language == "en" && new AppSettings().Language == "en", "English is the default UI and settings language");
Check(System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}", JsonDefaults.Options)!.Language == "en", "Libraries without a language migrate to English");
I18n.Language = "en";
Check(I18n.T("Neu starten") == "Restart game" && I18n.F($"Gun meldet P{2}. Die Oberfläche unterstützt aktuell P1/P2.") == "Gun reports P2. The interface currently supports P1/P2.", "Native menu and formatted device errors use the English catalog");
Check(I18n.Normalize("fr") == "en" && I18n.Normalize("de") == "de", "Unsupported language values fall back to English");
I18n.Language = "de";
Check(I18n.T("Neu starten") == "Neu starten" && OverlayControls.ActionName("coin") == "Münze", "Switching to German updates native controls immediately");
var languageStore = new LibraryStore(Path.Combine(Path.GetTempPath(), "reaper-language-" + Guid.NewGuid()));
var languageLibrary = LibraryState.Empty with { Settings = new AppSettings(Language: "de") };
languageStore.Save(languageLibrary);
Check(languageStore.Load().Settings.Language == "de" && !languageStore.Load().Settings.StartWithWindows, "German language survives a cold reload without changing autostart");
Directory.Delete(languageStore.DirectoryPath, true);
// Existing profile assertions use the German catalog.
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
    Check(GunSystems.Identify("3A-3H Retro Shooter 1") == "rs3" && GunSystems.Identify("STM32 USB Serial") is null, "Product identity detects the RS3 family without treating generic STM devices as guns");
    Check(GunSystems.Identify("Sinden Lightgun") == "sinden" && GunSystems.Identify("X-Gunner") == "xgunner" && GunSystems.Identify("Blamcon Vyper") == "blamcon", "Known manufacturer product names select their own system adapters");
    Check(GunSystems.DefaultMap(1)["key:38"]=="up" && !GunSystems.DefaultMap(1).ContainsKey("key:85") && GunSystems.DefaultMap(2)["key:85"]=="up", "Reaper factory direction keys differ between P1 and P2");
    Check(GunSystems.DefaultMap(1)["key:81"]=="start" && GunSystems.DefaultMap(2)["key:78"]=="secondary", "Reaper stick press and side key follow the manufacturer PC diagram");
    Check(!GunSystems.DefaultMap(1,"sinden").ContainsKey("key:49"), "Other manufacturers do not inherit Reaper keyboard assumptions");
    string model2=Path.Combine(root,"model2"); Directory.CreateDirectory(model2);
    string model2config=Path.Combine(model2,"EMULATOR.INI"); File.WriteAllText(model2config,"[RomDirs]\nDir1=roms ; existing local path\nDir2=\n[Renderer]\nAutoFull=1\n");
    var model2game=scarlet with { Source="custom",Executable=Path.Combine(model2,"emulator_multicpu.exe"),WorkingDirectory=model2,SourcePath=Path.Combine(roms,"pointblank.zip") };
    Check(EmulatorSetup.ConfigurePaths(model2game)&&File.ReadAllText(model2config).Contains("Dir2="+roms),"Model 2 receives the explicit library ROM folder instead of searching an empty local ROM directory");
    Check(!EmulatorSetup.ConfigurePaths(model2game)&&File.Exists(model2config+".before-reaper-path-fix")&&File.ReadAllText(model2config).Contains("AutoFull=1"),"Model 2 repair is idempotent, backs up its original config and preserves renderer settings");
    Check(!EmulatorSetup.ConfigurePaths(model2game with {Executable=scarlet.Executable}),"ROM path repair does not change unrelated emulators");
    string ps2dir=Path.Combine(root,"portable-ps2"),ps2config=Path.Combine(ps2dir,"inis","PCSX2.ini");Directory.CreateDirectory(Path.GetDirectoryName(ps2config)!);
    var ps2game=model2game with {Executable=Path.Combine(ps2dir,"pcsx2.exe"),WorkingDirectory=ps2dir};
    File.WriteAllText(ps2config,"[Folders]\nMemoryCards = "+Path.Combine(root,"unavailable-drive","cards")+"\nBios = bios\n[USB1]\nType = guncon2\n");
    Check(EmulatorSetup.ConfigurePaths(ps2game)&&Directory.Exists(Path.Combine(ps2dir,"memcards"))&&File.ReadAllText(ps2config).Contains("Type = guncon2"),"PCSX2 repairs unavailable absolute memory-card storage without changing gun inputs");
    Check(!EmulatorSetup.ConfigurePaths(ps2game)&&File.Exists(ps2config+".before-reaper-storage-fix"),"PCSX2 keeps accessible storage and makes one original backup");

    string dualDir=Path.Combine(tp,"DualTitle"),dualMain=Path.Combine(dualDir,"bin","game.exe"),dualSecond=Path.Combine(dualDir,"service","daemon.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(dualMain)!);Directory.CreateDirectory(Path.GetDirectoryName(dualSecond)!);File.WriteAllText(dualMain,"");File.WriteAllText(dualSecond,"");
    string dualProfile=Path.Combine(tp,"UserProfiles","dual.xml");
    File.WriteAllText(dualProfile,new XDocument(new XElement("GameProfile",new XElement("GamePath",dualMain),new XElement("HasTwoExecutables","true"),new XElement("GamePath2",Path.Combine(root,"old-drive","DualTitle","service","daemon.exe")))).ToString());
    var dualGame=scarlet with {SourcePath=dualProfile};
    Check(LaunchRules.Issues(dualGame).Length==1,"Dual executable profiles validate the second launcher rather than falsely reporting file readiness");
    Check(LaunchRules.RepairTeknoParrotPath(dualGame)&&XDocument.Load(dualProfile).Descendants("GamePath2").Single().Value==dualSecond&&!LaunchRules.RepairTeknoParrotPath(dualGame),"Second launcher is rebased through a shared ancestor only when one existing file matches");
    var customGun = new GunBinding(1,mouse,ButtonMap:new(){["key:85"]="coin"});
    Check(GunSystems.UpgradeMap(customGun)["key:85"]=="coin", "Factory migration preserves custom button bindings");
    Check(GunSystems.ValidControl("sinden","pump") && !GunSystems.ValidControl("rs3","pump"), "Physical learning accepts only controls belonging to the selected hardware");
    string repairProfile=Path.Combine(tp,"UserProfiles","repair.xml"); File.WriteAllText(repairProfile,"<GameProfile><GamePath>missing.exe</GamePath><Keep>original</Keep></GameProfile>");
    var repair=scarlet with { SourcePath=repairProfile, RequiredFiles=[repairProfile,Path.Combine(tp,"game & 1.exe")] };
    Check(LaunchRules.RepairTeknoParrotPath(repair) && XDocument.Load(repairProfile).Descendants("GamePath").Single().Value==Path.Combine(tp,"game & 1.exe"), "TeknoParrot repair uses one explicit existing library binary and escapes XML paths");
    Check(File.Exists(repairProfile+".before-reaper-path-fix") && XDocument.Load(repairProfile).Descendants("Keep").Single().Value=="original" && !LaunchRules.RepairTeknoParrotPath(repair), "Profile repair retains all other settings, makes a backup and is idempotent");
    File.WriteAllText(repairProfile,"<GameProfile><GamePath>missing.exe</GamePath></GameProfile>");
    Check(!LaunchRules.RepairTeknoParrotPath(repair with {RequiredFiles=[Path.Combine(tp,"game & 1.exe"),Path.Combine(tp,"TeknoParrotUi.exe")]}), "Ambiguous game files never overwrite a profile");
    File.WriteAllText(repairProfile,"<GameProfile><ConfigValues><FieldInformation><FieldName>Input API</FieldName><FieldValue>XInput</FieldValue><FieldOptions><string>RawInput</string></FieldOptions></FieldInformation></ConfigValues><JoystickButtons><JoystickButtons><ButtonName>Player 1 Light Gun</ButtonName></JoystickButtons><JoystickButtons><ButtonName>Player 1 Gun Trigger</ButtonName></JoystickButtons><JoystickButtons><ButtonName>Player 1 Start</ButtonName></JoystickButtons><JoystickButtons><ButtonName>Player 1 Footpedal</ButtonName><BindName>Keep pedal</BindName></JoystickButtons></JoystickButtons></GameProfile>");
    Check(TeknoGunSetup.Configure(repair,[new(1,mouse,"keyboard-1")])>0,"TeknoParrot setup writes the selected gun's raw-input device into the actual profile");
    var configuredTp=XDocument.Load(repairProfile);
    Check(configuredTp.Descendants("DeviceType").Count(e=>e.Value=="Mouse")==2 && configuredTp.Descendants("KeyboardKey").Any(e=>e.Value=="D1"),"TeknoParrot maps aim/trigger to the gun mouse and Start to its keyboard");
    Check(configuredTp.Descendants("FieldValue").Single().Value=="RawInput" && configuredTp.Descendants("BindName").Any(e=>e.Value=="Keep pedal") && TeknoGunSetup.Configure(repair,[new(1,mouse,"keyboard-1")])==0,"Automatic input setup is idempotent and preserves title-specific pedal controls");
    Check(OverlayControls.Read(repair,root,[new(1,mouse,"keyboard-1")]).Rows.Any(r=>r.Function=="Player 1 Light Gun" && r.Input=="P1 · "+I18n.T("Zielen / Mausbewegung")),"TeknoParrot aim binding is shown as movement instead of an unassigned button");
    Throws(() => GunSystems.ValidateMap(new() { ["mouse:99"] = "shoot" }), "Binding rejects invalid physical inputs");
    Throws(() => GunSystems.ValidateMap(new() { ["key:49"] = "delete-all" }), "Binding rejects unsupported actions");
    Check(GunSystems.ReaperConfiguration(new(OffscreenReload:false, Aspect:"4:3")).SequenceEqual(new[] {"ZS", "ZM", "ZN", "ZB", "ZX"}), "RS3 configuration applies aspect and reload and exits external mode without firing recoil");
    var remapped = XDocument.Parse(LaunchRules.MameController([new(1, mouse, ButtonMap:new() { ["mouse:2"]="shoot", ["key:49"]="start" })]));
    Check(remapped.Descendants("port").Single(p => (string?)p.Attribute("type") == "P1_BUTTON1").Value == "GUNCODE_1_BUTTON2", "Learned trigger binding reaches the actual MAME controller output");
    Check(remapped.Descendants("port").Single(p => (string?)p.Attribute("type") == "START1").Value == "KEYCODE_1", "MAME receives keyboard start mapping independently of gun numbering");
    var savedGuns = new LibraryStore(Path.Combine(root, "gun-data"));
    var gunState = LibraryState.Empty; gunState.Bindings.Add(new(1, mouse, "keyboard", "COM3", "rs3", "physical-container", new() { ["mouse:2"]="shoot" }, new(Aspect:"4:3"), true, new(){["trigger"]="mouse:2"})); savedGuns.Save(gunState);
    Check(savedGuns.Load().Bindings.Single().ButtonMap!["mouse:2"] == "shoot" && savedGuns.Load().Bindings.Single().Feedback!.Aspect == "4:3" && savedGuns.Load().Bindings.Single().ControlMap!["trigger"] == "mouse:2", "Physical assignment, captured controls, learned bindings and feedback survive cold reload");
    var gesture = new ExitGesture(); var now = DateTimeOffset.UtcNow; gesture.Key(0x31, true, now); gesture.Key(0x35, true, now);
    Check(!gesture.Ready(now.AddSeconds(1)) && gesture.Ready(now.AddSeconds(2)), "Exit requires a held chord, not coin or start alone");
    gesture.Key(0x35, false, now.AddSeconds(2)); Check(!gesture.Ready(now.AddSeconds(4)), "Releasing chord cancels exit");
    gesture.Key(0x35, true, now.AddSeconds(4)); gesture.Consume();
    gesture.Key(0x31, true, now.AddSeconds(5)); gesture.Key(0x35, true, now.AddSeconds(5));
    Check(!gesture.Ready(now.AddSeconds(8)), "Held/repeated exit keys cannot also close the menu after ending a game");
    gesture.Key(0x31, false, now.AddSeconds(8)); gesture.Key(0x35, false, now.AddSeconds(8));
    gesture.Key(0x31, true, now.AddSeconds(9)); gesture.Key(0x35, true, now.AddSeconds(9));
    Check(gesture.Ready(now.AddSeconds(11)), "Releasing both keys rearms the gun exit for closing the app");
    var hold = new TriggerHold(); hold.Button("trigger", true, 0);
    Check(!hold.Ready(9999) && hold.Ready(10000), "Overlay opens at exactly ten seconds, never before");
    hold.Button("trigger", true, 9000);
    Check(hold.Ready(10000), "Repeated down events do not restart the ten-second trigger hold");
    hold.Consume(); Check(!hold.Ready(30000), "A consumed hold cannot reopen the overlay while still pressed");
    hold.Button("trigger", false, 30001); hold.Button("trigger", true, 30002);
    Check(!hold.Ready(40001) && hold.Ready(40002), "Release rearms a fresh full ten-second hold");
    hold.Reset(); hold.Button("trigger", true, 0); hold.Button("trigger", false, 9999);
    Check(!hold.Ready(20000), "An early release cancels instead of accumulating repeated shots");
    var secondHold = new TriggerHold(); secondHold.Button("P2", true, 5000);
    Check(!secondHold.Ready(14999) && secondHold.Ready(15000) && !hold.Ready(15000), "Players have isolated hold durations");
    secondHold.Reset(); Check(!secondHold.Ready(90000), "Game end or disconnect clears stale trigger state");
    string ctrlDir = Path.Combine(root, "overlay", "controllers"); Directory.CreateDirectory(ctrlDir);
    File.WriteAllText(Path.Combine(ctrlDir, "reaper.cfg"), LaunchRules.MameController([new(1, mouse, ButtonMap:new() { ["mouse:2"]="shoot", ["key:53"]="coin" })]));
    var mameControls = OverlayControls.Read(scarlet with { Source="mame" }, Path.Combine(root,"overlay"), []);
    Check(mameControls.Rows.Any(r => r.Player == 1 && r.Function == "Münze" && r.Input == "Taste 5") && mameControls.Rows.Any(r => r.Input == "P1 · Gun-Taste 2"), "Overlay reads the actual generated MAME bindings including a remapped trigger with readable labels");
    string tpOverlay = Path.Combine(root, "overlay.xml");
    File.WriteAllText(tpOverlay, new XDocument(new XElement("GameProfile", new XElement("ConfigValues", new XElement("FieldInformation", new XElement("FieldName", "Input API"), new XElement("FieldValue", "RawInput"))), new XElement("JoystickButtons", new XElement("JoystickButtons", new XElement("ButtonName", "Player 1 Start"), new XElement("InputMapping", "P1ButtonStart"), new XElement("RawInputButton", new XElement("DeviceType", "Keyboard"), new XElement("DevicePath", "keyboard"), new XElement("KeyboardKey", "D1"), new XElement("MouseButton", "None")))))).ToString());
    var tpControls = OverlayControls.Read(scarlet with { SourcePath=tpOverlay }, root, [new(1, mouse, "keyboard")]);
    Check(tpControls.Rows.Single().Player == 1 && tpControls.Rows.Single().Input == "P1 · D1", "Overlay retains TeknoParrot's title-specific start label and exact device assignment");
    Check(OverlayControls.Read(scarlet with { Source="pc" }, root, []).Rows.Length == 0, "Unknown game mappings stay unknown instead of becoming invented A/B assignments");
    string dolphin = Path.Combine(root,"Dolphin.exe"); File.WriteAllText(dolphin, ""); File.WriteAllText(Path.Combine(root,"portable.txt"), "");
    Directory.CreateDirectory(Path.Combine(root,"User","Config")); File.WriteAllText(Path.Combine(root,"User","Config","WiimoteNew.ini"), "[Wiimote1]\nButtons/A = `Click 1`\nButtons/B = `Click 2`\n[Wiimote2]\nButtons/A = `D2`\n");
    var dolphinControls = OverlayControls.Read(scarlet with { Source="pc", Executable=dolphin, Arguments=[] }, root, []);
    Check(dolphinControls.Rows.Any(r => r.Player == 1 && r.Function == "A" && r.Input == "`Click 1`") && dolphinControls.Rows.Any(r => r.Player == 2 && r.Input == "`D2`"), "Overlay reads separate Dolphin A/B mappings from the selected portable configuration");
    var calibration=new ReaperCalibration();
    Check(calibration.Trigger(true)==0xB0 && calibration.Trigger(true) is null && calibration.Step==0,"RS3 calibration starts once and ignores repeated trigger-down packets");
    foreach(byte command in new byte[]{0xB2,0xB4,0xB6,0xB8}) {calibration.Trigger(false);Check(calibration.Trigger(true)==command,"RS3 saves the next manufacturer target only after release");}
    Check(calibration.Saved && calibration.Cancel() is null && new ReaperCalibration().Cancel()==0xBA,"RS3 completion preserves the saved sequence; an unfinished sequence aborts without saving");
    Check(ReaperCalibration.Targets.SequenceEqual(new (double,double)[]{(.5,.5),(.1,.1),(.9,.9),(.9,.1)}),"RS3 calibration uses manufacturer target order and actual screen proportions");
    string wbfs=Path.Combine(root,"Dead Space.wbfs");byte[] disc=new byte[1024];Encoding.ASCII.GetBytes("WBFS").CopyTo(disc,0);disc[8]=9;disc[12]=1;Encoding.ASCII.GetBytes("RZJE69").CopyTo(disc,512);File.WriteAllBytes(wbfs,disc);
    var deadspace=scarlet with {Title="Dead Space Extraction",Executable=dolphin,Source="custom",Arguments=["-e",wbfs],WorkingDirectory=root};
    Check(DolphinSetup.DiscId(deadspace)=="RZJE69","Dolphin setup reads the actual WBFS disc ID instead of guessing from a filename");
    string pack=Path.Combine(root,"accuracy-pack");Directory.CreateDirectory(Path.Combine(pack,"GameSettings"));Directory.CreateDirectory(Path.Combine(pack,"Config","Profiles","Wiimote"));
    File.WriteAllText(Path.Combine(pack,"GameSettings","RZJE69.ini"),"[Controls]\nWiimoteProfile1 = DEADSPACE_P1\n");
    File.WriteAllText(Path.Combine(pack,"Config","Profiles","Wiimote","DEADSPACE_P1.ini"),"[Profile]\nIR/Total Yaw = 17.25\nIR/Total Pitch = 16.75\nExtension = Nunchuk\nButtons/A = OLD\nNunchuk/Buttons/C = LCONTROL\n");
    File.WriteAllText(Path.Combine(pack,"LICENSE"),"External fixture license");Directory.CreateDirectory(Path.Combine(root,"User","GameSettings"));
    string perGame=Path.Combine(root,"User","GameSettings","RZJE69.ini");File.WriteAllText(perGame,"[Core]\nCPUThread = False\n[Controls]\nWiimoteSource1 = 1\nWiimoteProfile1 = OTHER_GUN\n");
    Check(DolphinSetup.Configure(deadspace,pack,[new(1,"RS3")]),"Dolphin applies the separate RS3 title profile on the actual setup path");
    string managed=DolphinSetup.ProfilePath(deadspace)!;string managedText=File.ReadAllText(managed);
    Check(DolphinSetup.Value(managedText,"Profile","IR/Total Yaw")=="17.25" && DolphinSetup.Value(managedText,"Profile","Nunchuk/Buttons/C")=="`M` & !`Click 1`" && !managedText.Contains("LCONTROL"),"Dolphin retains title accuracy and replaces inaccessible RS3 Nunchuk buttons");
    Check(File.ReadAllText(perGame).Contains("CPUThread = False") && DolphinSetup.Value(File.ReadAllText(perGame),"Controls","WiimoteSource1")=="0" && File.Exists(perGame+".before-deadeye-input"),"Dolphin preserves unrelated overrides, backs up input settings and disables an unverified P2 device");
    var titleControls=OverlayControls.Read(deadspace,root,[new(1,"RS3")]);
    Check(titleControls.Rows.Any(r=>r.Function=="Stasis (C)"&&r.Input=="`M` & !`Click 1`") && titleControls.Rows.Any(r=>r.Function.StartsWith("Glow Worm")) && titleControls.Rows.All(r=>r.Player==1),"Game overlay reads the active per-game controls including stasis, shake and pending P2");
    string firstSettings=File.ReadAllText(perGame);DolphinSetup.Configure(deadspace,pack,[new(1,"RS3")]);Check(firstSettings==File.ReadAllText(perGame),"Dolphin title setup is idempotent");
    Check(!DolphinSetup.Configure(deadspace,pack,[new(1,"Sinden",SystemId:"sinden")]),"Dolphin never applies RS3 controls to another gun system");
    Check(DolphinSetup.ReaperButtons("RZJE69",new(1,"RS3",ControlMap:new(){["side"]="key:66"}))["Nunchuk/Buttons/C"]=="`B` & !`Click 1`","Dolphin honours a learned physical side-button mapping");
    string model=Path.Combine(root,"Supermodel.exe");File.WriteAllText(model,"");Directory.CreateDirectory(Path.Combine(root,"Config"));
    string modelIni=Path.Combine(root,"Config","Supermodel.ini");File.WriteAllText(modelIni,"[ Global ] ; existing global section\nInputGunX2 = JOY2_XAXIS\nSoundVolume = 61\n");
    InputDevice[] modelDevices=[new("p2mouse","RS3 P2","mouse",true),new("Root#RDP_MOU","remote","mouse",false),new("p1mouse","RS3 P1","mouse",true),new("p2keys","RS3 P2","keyboard",true),new("p1keys","RS3 P1","keyboard",true)];
    var modelGame=deadspace with{Executable=model};
    Check(SupermodelSetup.Configure(modelGame,[new(1,"p1mouse","p1keys"),new(2,"p2mouse","p2keys")],modelDevices),"Supermodel resolves live physical devices and configures two separate players");
    var modelText=File.ReadAllText(modelIni);
    Check(DolphinSetup.Value(modelText,"Global","InputGunX")=="\"MOUSE1_XAXIS\"" && DolphinSetup.Value(modelText,"Global","InputGunX2")=="\"MOUSE2_XAXIS\"" && DolphinSetup.Value(modelText,"Global","InputStart2")=="\"KEY2_2\"" && modelText.Contains("SoundVolume = 61"),"Supermodel uses the emulator's reversed enumeration and separate P2 keyboard without overwriting other settings");
    SupermodelSetup.Configure(modelGame,[new(1,"p1mouse","p1keys")],modelDevices);
    Check(DolphinSetup.Value(File.ReadAllText(modelIni),"Global","InputGunX2")=="\"NONE\"","Supermodel clears unavailable P2 routing instead of assigning an unrelated joystick");
    string ra = Path.Combine(root, "retroarch.exe"), raConfig = Path.Combine(root,"overlay-retroarch.cfg"), raExtra = Path.Combine(root,"overlay-extra.cfg");
    File.WriteAllText(raConfig, "input_player1_gun_start = \"enter\"\ninput_player2_b = \"v\"\n"); File.WriteAllText(raExtra, "input_player1_gun_start = \"1\"\n");
    var raControls = OverlayControls.Read(scarlet with { Source="pc", Executable=ra, Arguments=["--config",raConfig,"--appendconfig",raExtra] }, root, []);
    Check(raControls.Rows.Any(r => r.Player == 1 && r.Function == "Start" && r.Input == "Taste 1") && raControls.Rows.Any(r => r.Player == 2 && r.Function == "B" && r.Input == "Taste v"), "RetroArch appended config overrides the base mapping and preserves P2 B");
    using var http = new HttpClient(new FixtureHttp());
    var cover = await new CoverService(http, Path.Combine(root, "covers")).Download("Point Blank", "fixture", CancellationToken.None);
    Check(cover is not null && File.Exists(cover), "Cover service downloads an exact match and saves local artwork");
    using var ambiguousHttp = new HttpClient(new FixtureHttp(true));
    var ambiguous = await new CoverService(ambiguousHttp, Path.Combine(root, "covers")).Download("Point Blank", "fixture", CancellationToken.None);
    Check(ambiguous is null, "Ambiguous cover titles are not silently guessed");
    LibraryStore.Merge(state, [scarlet with { Status = "needs-setup" }]);
    Check(state.Games.Single(g => g.Id == scarlet.Id).Status == "needs-setup", "Reimport invalidates playable verdict when game files disappear");
    var discoveries = InstallationFinder.Find([root]);
    Check(discoveries.Count == 3 && discoveries.Single(d => d.Kind == "tekno").Path == scarlet.Executable && discoveries.Any(d=>d.Name=="Supermodel"), "Installation search finds nested TeknoParrot, Dolphin and Supermodel fixtures and deduplicates paths");
    var browse = InstallationFinder.Browse(tp, false, [".exe"]);
    Check(browse.Entries.Any(e => !e.Directory && e.Path == scarlet.Executable) && !browse.Entries.Any(e => e.Name.EndsWith(".xml")), "Arcade picker filters applications and retains navigable folders");
    var folderPage = InstallationFinder.Browse(tp, true, []);
    Check(folderPage.Entries.All(e => e.Directory) && folderPage.Parent == root, "Folder selection cannot offer files and exposes correct parent");
    Throws(() => InstallationFinder.Browse(Path.Combine(root, "absent"), true, []), "Picker rejects vanished directory");
    string missingAsset = Path.Combine(root, "required.bin");
    var incomplete = LaunchRules.Validate(scarlet with { RequiredFiles = [missingAsset], Status = "tested" });
    Check(incomplete.Status == "needs-setup" && incomplete.SetupIssues!.Length == 1, "Required assets block and invalidate a previously tested game");
    File.WriteAllText(missingAsset, "asset");
    Check(LaunchRules.Validate(incomplete).Status == "unverified", "Restored assets unlock only file readiness, never claim gameplay");
    string handoff = Path.Combine(root, "spiele.json");
    var options = new[] { new { target = Path.Combine(root, "missing.exe"), cwd = root, requires = new[] {missingAsset}, arguments_array = new[] {"bad"} },
        new { target = scarlet.Executable, cwd = tp, requires = new[] { scarlet.SourcePath, missingAsset }, arguments_array = scarlet.Arguments } };
    File.WriteAllText(handoff, System.Text.Json.JsonSerializer.Serialize(new { games = new[] { new { id = 316, title = "Fixture collection game", system = "Arcade", priority = 1, two_player = "2", dual_gun = "not tested", notes = "", launch_options = options } } }));
    var collection = CollectionImporter.Read(handoff);
    Check(collection.Games.Count == 1 && collection.Games[0].Executable == scarlet.Executable && collection.Games[0].Status == "unverified", "Collection chooses the complete alternative and validates the actual TP GamePath");
    Check(collection.Games[0].Id == "inventory-316" && collection.Games[0].Favorite && collection.Games[0].RequiredFiles!.Length == 2, "Collection preserves stable IDs, priority and runtime dependencies");
    File.Delete(missingAsset);
    Check(CollectionImporter.Read(handoff).Games[0].Status == "needs-setup", "Collection keeps unavailable titles visible but blocked");
    var shared = LibraryState.Empty;
    LibraryStore.Merge(shared, [scarlet with { Id = "inventory-a" }, scarlet with { Id = "inventory-b", Title = "Other title in shared collection" }]);
    Check(shared.Games.Count == 2, "Separate games sharing one launcher remain separate inventory entries");
    string chd = Path.Combine(roms, "carnevil"); Directory.CreateDirectory(chd);
    string archive = chd + ".zip", emulator = Path.Combine(root, "mame.exe"); File.WriteAllText(emulator, "fixture");
    File.WriteAllText(handoff, System.Text.Json.JsonSerializer.Serialize(new { games = new[] { new { id = 67, title = "CarnEvil", system = "MAME", c_paths = new[] { chd, archive, Path.Combine(roms, "other-clone.zip") }, launch_options = new[] { new { target = emulator, cwd = root, requires = new[] { chd }, arguments_array = new[] { "carnevil" } } } } } }));
    var chdGame = CollectionImporter.Read(handoff).Games.Single();
    Check(chdGame.SourcePath == archive && chdGame.RequiredFiles!.Length == 2 && chdGame.Status == "needs-setup", "MAME collection requires the ROM archive beside its data folder without requiring unrelated clones");
    File.WriteAllText(archive, "fixture");
    Check(CollectionImporter.Read(handoff).Games.Single().Status == "unverified", "MAME collection becomes file-ready only when archive and data folder both exist");
    string mediaRoot = Path.Combine(root, "media"); Directory.CreateDirectory(mediaRoot);
    string image = Path.Combine(mediaRoot, "cover #1.png"); File.WriteAllText(image, "fixture");
    Check(MediaPaths.Url(image, mediaRoot, "media.reaper.local") == "https://media.reaper.local/cover%20%231.png", "Media URL escapes real files under the dedicated folder");
    Check(MediaPaths.Url(scarlet.Executable, mediaRoot, "media.reaper.local") is null && MediaPaths.Url(Path.Combine(root, "outside.png"), mediaRoot, "media.reaper.local") is null, "Media URL refuses executables and paths outside its folder");
    string link = Path.Combine(mediaRoot, "escape.png"); File.CreateSymbolicLink(link, image);
    Check(MediaPaths.Url(link, mediaRoot, "media.reaper.local") is null, "Media URL refuses symbolic links");
    Check(RuntimeCatalog.For("MSVCR100.dll", "x86") == "vc2010-x86" && RuntimeCatalog.For("MSVCR100.dll", "x64") == "vc2010-x64", "Legacy VC dependency matches program architecture, not Windows architecture");
    Check(RuntimeCatalog.For("xinput1_3.dll", "x86") == "directx" && RuntimeCatalog.For("xinput1_4.dll", "x64") is null, "DirectX legacy libraries are distinguished from OS components");
    Check(RuntimeCatalog.Get("arbitrary-url") is null && RuntimeCatalog.Get("vc2010-arm64") is null, "Installer catalog refuses arbitrary or unsupported package identities");
    string depDir = Path.Combine(root, "deps"); Directory.CreateDirectory(depDir);
    string native = Path.Combine(depDir, "game.exe");
    WriteNative(native, false, "msvcr100.dll", "xinput1_3.dll");
    var parsed = NativeImports.Read(native);
    Check(parsed.Architecture == "x86" && parsed.Imports.Any(i => i.Name == "msvcr100.dll" && !i.Delayed) && parsed.Imports.Any(i => i.Name == "xinput1_3.dll" && i.Delayed), "PE parser reads ordinary and delay imports without executing code");
    var depGame = GameImporter.Pc(native, "Dependency fixture");
    var absent = DependencyScanner.Scan([depGame], Path.Combine(root, "windows"));
    string secondExe = Path.Combine(depDir, "amdaemon.exe"), runtimeProfile = Path.Combine(depDir, "two-executables.xml");
    WriteNative(secondExe, true, "msvcp110.dll");
    new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("GameProfile", new System.Xml.Linq.XElement("GamePath", native), new System.Xml.Linq.XElement("HasTwoExecutables", true), new System.Xml.Linq.XElement("GamePath2", secondExe))).Save(runtimeProfile);
    var profileGame = depGame with { Source = "teknoparrot", SourcePath = runtimeProfile };
    Check(DependencyScanner.Scan([profileGame], root).Findings.Any(f => f.Binary == secondExe && f.PackageId == "vc2012-x64" && f.Missing && f.Required), "TeknoParrot second executable participates in the required dependency scan");
    File.WriteAllText(runtimeProfile, File.ReadAllText(runtimeProfile).Replace("<HasTwoExecutables>true</HasTwoExecutables>", "<HasTwoExecutables>false</HasTwoExecutables>"));
    Check(!DependencyScanner.Scan([profileGame], root).Findings.Any(f => f.Binary == secondExe), "Unused TeknoParrot second executable does not block single-executable profiles");
    Check(absent.Findings.Single(f => f.Dll == "msvcr100.dll").Missing && absent.Findings.Single(f => f.Dll == "msvcr100.dll").Required && !absent.Findings.Single(f => f.Dll == "xinput1_3.dll").Required, "Scan identifies required missing VC and optional delayed DirectX separately");
    WriteNative(Path.Combine(depDir, "msvcr100.dll"), true);
    Check(DependencyScanner.Scan([depGame], root).Findings.Single(f => f.Dll == "msvcr100.dll").Missing, "Wrong-architecture local DLL cannot falsely satisfy a dependency");
    WriteNative(Path.Combine(depDir, "msvcr100.dll"), false);
    Check(!DependencyScanner.Scan([depGame], root).Findings.Single(f => f.Dll == "msvcr100.dll").Missing, "Recheck observes the installed compatible DLL");
    File.WriteAllText(Path.ChangeExtension(native, ".runtimeconfig.json"), "{\"runtimeOptions\":{\"framework\":{\"name\":\"Microsoft.WindowsDesktop.App\",\"version\":\"8.0.2\"}}}");
    Check(DependencyScanner.Scan([depGame], root, (_, _, _) => false).MissingPackages.Contains("dotnet8-x86"), "Managed runtime configuration selects the correct .NET generation and architecture");
    Check(!DependencyScanner.Scan([depGame], root, (_, _, _) => true).MissingPackages.Contains("dotnet8-x86"), "Managed runtime rescan observes an installed compatible framework");
    string legacy = Path.Combine(depDir, "legacy-helper.exe");
    WriteManaged(legacy, "v2.0.50727");
    var legacyGame = depGame with { Helpers = [new(legacy, [], depDir)] };
    Check(DependencyScanner.Scan([legacyGame], root, (_, _, _) => false).MissingPackages.Contains("netfx35"), "CLR 2 helper is detected without a modern runtimeconfig file");
    Check(!DependencyScanner.Scan([legacyGame], root, (_, _, _) => true).MissingPackages.Contains("netfx35"), "Classic framework rescan observes installation");
    File.WriteAllText(legacy + ".config", "<configuration><startup><supportedRuntime version=\"v4.0\"/></startup></configuration>");
    Check(!DependencyScanner.Scan([legacyGame], root, (_, _, _) => false).MissingPackages.Contains("netfx35"), "CLR 4 startup override avoids unnecessary CLR 2 installation");
    File.Delete(legacy + ".config"); WriteManaged(legacy, "v4.0.30319");
    Check(!DependencyScanner.Scan([legacyGame], root, (_, _, _) => false).MissingPackages.Contains("netfx35"), "CLR 4 executables do not require NetFx3");
    Check(RuntimeCatalog.Get("netfx35")?.Url.StartsWith("https://download.microsoft.com/") == true, "Classic framework catalog uses the official Microsoft installer");
    string optionalRoot = Path.Combine(depDir, "optional.exe"), requiredRoot = Path.Combine(depDir, "required.exe");
    WriteNative(optionalRoot, false, delayed: "shared.dll"); WriteNative(requiredRoot, false, "shared.dll");
    WriteNative(Path.Combine(depDir, "shared.dll"), false, "msvcr120.dll");
    var joined = GameImporter.Pc(optionalRoot, "Required after optional") with { RequiredFiles = [requiredRoot] };
    Check(DependencyScanner.Scan([joined], root).Findings.Any(f => f.Dll == "msvcr120.dll" && f.Missing && f.Required), "A required import reached after a delay import still blocks a broken launch");
    Throws(() => NativeImports.Read(handoff), "Malformed executable is rejected without loading it");
    byte[] updateBytes = new byte[2048]; new Random(7).NextBytes(updateBytes);
    string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(updateBytes)).ToLowerInvariant();
    string releaseJson = System.Text.Json.JsonSerializer.Serialize(new { draft = false, prerelease = false, tag_name = "v0.3.4", body = "Test changes", assets = new[] { new { name = "Deadeye-Arcade-0.3.4-Setup-x64.exe", browser_download_url = "https://github.com/sthamann/deadeye-arcade/releases/download/v0.3.4/Deadeye-Arcade-0.3.4-Setup-x64.exe", digest = "sha256:" + digest, size = updateBytes.Length } } });
    var release = AppUpdates.Read(releaseJson, new Version(0, 3, 3, 0))!;
    Check(release.Version == "0.3.4" && release.Sha256 == digest, "Updater selects a newer stable installer and its SHA-256 digest");
    Check(AppUpdates.Read(releaseJson, new Version(0, 3, 4, 0)) is null && AppUpdates.Read(releaseJson, new Version(0, 4, 0, 0)) is null, "Updater never offers the same version or a downgrade");
    Check(AppUpdates.Read(releaseJson.Replace("\"prerelease\":false", "\"prerelease\":true"), new Version(0, 3, 3, 0)) is null, "Automatic updates exclude prereleases");
    Throws(() => AppUpdates.Read(releaseJson.Replace("https://github.com/", "https://example.com/"), new Version(0, 3, 3, 0)), "Updater rejects foreign download addresses");
    Throws(() => AppUpdates.Read(releaseJson.Replace(digest, "invalid"), new Version(0, 3, 3, 0)), "Updater rejects missing or malformed digests");
    using var updateClient = new HttpClient(new UpdateFixture(updateBytes));
    string updateDirectory = Path.Combine(root, "updates");
    string downloaded = await AppUpdates.Download(updateClient, release, updateDirectory, null);
    Check(File.ReadAllBytes(downloaded).SequenceEqual(updateBytes), "Verified installer downloads reach the final file");
    using var corruptClient = new HttpClient(new UpdateFixture(new byte[updateBytes.Length]));
    bool corruptRejected = false;
    try { await AppUpdates.Download(corruptClient, release, updateDirectory, null); } catch (InvalidDataException) { corruptRejected = true; }
    Check(corruptRejected && !File.Exists(downloaded + ".partial") && File.ReadAllBytes(downloaded).SequenceEqual(updateBytes), "A corrupt download is rejected and preserves an earlier verified installer");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    bool cancelled = false;
    try { await AppUpdates.Download(updateClient, release, updateDirectory, null, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled && !File.Exists(downloaded + ".partial"), "Cancelled updates clean partial downloads");
    Check(new AppSettings().CheckForUpdates && System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}", JsonDefaults.Options)!.CheckForUpdates, "Automatic update checks default on for new and existing libraries");
    if (args.Length > 1)
    {
        var actual = CollectionImporter.Read(args[1]);
        using var fixture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]));
        var rows = fixture.RootElement.GetProperty("games").EnumerateArray().ToArray();
        Check(actual.Games.Count == rows.Length && actual.Games.Select(g => g.Id).Distinct().Count() == rows.Length, "Collection fixture imports every distinct entry, including absent media");
        Check(actual.Games.Count(g => g.Priority == 1) == rows.Count(r => r.TryGetProperty("priority", out var n) && n.GetInt32() == 1), "Collection fixture preserves priority selection");
        File.WriteAllText(Path.Combine(args[0], "collection-import-check.json"), System.Text.Json.JsonSerializer.Serialize(actual, JsonDefaults.Options));
    }
    var enriched = LibraryState.Empty;
    enriched.Games.Add(scarlet with { Favorite = true, Status = "tested", Description = "Old description" });
    var metadata = new GameEnrichment(scarlet.Id, scarlet.Title, scarlet.Platform, "Fight undead creatures in a cinematic arcade rail shooter.", 2018,
        "SEGA ALLS", ["https://segaarcade.com/games/house-of-the-dead-scarlet-dawn-theatre"]);
    LibraryEnrichment.Apply(enriched, new([metadata]));
    Check(enriched.Games[0].Description == metadata.Description && enriched.Games[0].ReleaseYear == 2018 && enriched.Games[0].Hardware == "SEGA ALLS", "Game information reaches the real library model");
    Check(enriched.Games[0].Arguments.SequenceEqual(scarlet.Arguments) && enriched.Games[0].Favorite && enriched.Games[0].Status == "tested", "Enrichment preserves launches, favourites and actual verification status");
    var enrichedStore = new LibraryStore(Path.Combine(root, "enriched")); enrichedStore.Save(enriched);
    Check(enrichedStore.Load().Games[0].MetadataSources!.Single() == metadata.MetadataSources![0], "Information and provenance survive a cold library reload");
    LibraryStore.Merge(enriched, [scarlet]);
    Check(enriched.Games[0].Description == metadata.Description && enriched.Games[0].ReleaseYear == 2018 && enriched.Games[0].Hardware == "SEGA ALLS", "Reimporting an old-format game preserves enriched information");
    var before = enriched.Games[0];
    Throws(() => LibraryEnrichment.Apply(enriched, new([metadata with { Description = "Must not partially apply" }, metadata with { Id = "wrong" }])), "Wrong game identity rejects the complete enrichment batch");
    Check(enriched.Games[0] == before, "A rejected batch does not partially mutate the library");
    Throws(() => LibraryEnrichment.Apply(enriched, new([metadata with { ReleaseYear = 9999 }])), "Impossible release year is rejected");
    Throws(() => LibraryEnrichment.Apply(enriched, new([metadata with { Cover = Path.Combine(root, "missing.jpg") }])), "Missing new artwork cannot silently enter the library");
    Throws(() => LibraryEnrichment.Apply(enriched, new([metadata with { MetadataSources = ["file:///private/source"] }])), "Private paths are not accepted as public provenance links");
    Console.WriteLine($"\n{checks} meaningful core checks passed.");
}
finally { Directory.Delete(root, true); }

void WriteManaged(string path, string version)
{
    var metadata = new System.Reflection.Metadata.Ecma335.MetadataBuilder();
    metadata.AddModule(0, metadata.GetOrAddString("fixture"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
    var pe = new System.Reflection.PortableExecutable.ManagedPEBuilder(
        new System.Reflection.PortableExecutable.PEHeaderBuilder(),
        new System.Reflection.Metadata.Ecma335.MetadataRootBuilder(metadata, version),
        new System.Reflection.Metadata.BlobBuilder());
    var blob = new System.Reflection.Metadata.BlobBuilder(); pe.Serialize(blob); File.WriteAllBytes(path, blob.ToArray());
}

void WriteNative(string path, bool x64, string? imported = null, string? delayed = null)
{
    byte[] bytes = new byte[1024]; using var memory = new MemoryStream(bytes); using var writer = new BinaryWriter(memory);
    void U16(int offset, ushort value) { memory.Position = offset; writer.Write(value); }
    void U32(int offset, uint value) { memory.Position = offset; writer.Write(value); }
    U16(0, 0x5a4d); U32(0x3c, 0x80); U32(0x80, 0x4550); U16(0x84, x64 ? (ushort)0x8664 : (ushort)0x14c); U16(0x86, 1);
    int opt = 0x98, optSize = x64 ? 240 : 224; U16(0x94, (ushort)optSize); U16(0x96, 0x102); U16(opt, x64 ? (ushort)0x20b : (ushort)0x10b);
    U32(opt + 32, 0x1000); U32(opt + 36, 0x200); U32(opt + 56, 0x2000); U32(opt + 60, 0x200); U16(opt + 68, 3);
    int directory = opt + (x64 ? 112 : 96); U32(directory - 4, 16);
    int section = opt + optSize; memory.Position = section; writer.Write(Encoding.ASCII.GetBytes(".rdata\0\0"));
    U32(section + 8, 0x200); U32(section + 12, 0x1000); U32(section + 16, 0x200); U32(section + 20, 0x200); U32(section + 36, 0x40000040);
    if (imported is not null) { U32(directory + 8, 0x1000); U32(directory + 12, 40); U32(0x20c, 0x1080); memory.Position = 0x280; writer.Write(Encoding.ASCII.GetBytes(imported + "\0")); }
    if (delayed is not null) { U32(directory + 13 * 8, 0x1040); U32(directory + 13 * 8 + 4, 64); U32(0x240, 1); U32(0x244, 0x10b0); memory.Position = 0x2b0; writer.Write(Encoding.ASCII.GetBytes(delayed + "\0")); }
    File.WriteAllBytes(path, bytes);
}

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

sealed class UpdateFixture(byte[] bytes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(bytes) });
    }
}
