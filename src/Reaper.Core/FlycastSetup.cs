namespace Reaper.Core;

public static class FlycastSetup
{
    public static bool IsFlycast(GameEntry game) => Path.GetFileName(game.Executable).Equals("flycast.exe", StringComparison.OrdinalIgnoreCase);
    public static GameEntry ResolveContent(GameEntry game,bool validateArchive=false)
    {
        if(!IsFlycast(game) || string.IsNullOrWhiteSpace(game.SourcePath) || !game.Platform.Contains("Naomi",StringComparison.OrdinalIgnoreCase) && !game.Platform.Contains("Atomiswave",StringComparison.OrdinalIgnoreCase))return game;
        string folder;
        try{folder=Path.TrimEndingDirectorySeparator(Path.GetFullPath(game.SourcePath,game.WorkingDirectory));}
        catch(Exception e) when(e is ArgumentException or NotSupportedException){return game;}
        string archive=folder+".zip";
        if(!Directory.Exists(folder)||!File.Exists(archive))return game;
        bool Matches(string argument)
        {
            if(argument.StartsWith('-'))return false;
            try{return Path.TrimEndingDirectorySeparator(Path.GetFullPath(argument,game.WorkingDirectory)).Equals(folder,StringComparison.OrdinalIgnoreCase);}
            catch(Exception e) when(e is ArgumentException or NotSupportedException){return false;}
        }
        if(!game.Arguments.Any(Matches))return game;
        // A Naomi CHD directory is a companion to the same-named MAME ZIP,
        // not the content file passed to Flycast. Never choose another set.
        // Fingerprints select the same path without parsing untrusted archives.
        // Validation belongs to the supervised per-game launch, where a bad
        // file becomes one failed game instead of aborting a resumed batch.
        if(validateArchive)
        {
            try
            {
                using var zip=System.IO.Compression.ZipFile.OpenRead(archive);
                if(!zip.Entries.Any(e=>e.Name.Length>0))throw new InvalidDataException("Empty archive.");
            }
            catch(InvalidDataException error){throw new IOException(I18n.T("Das passende Arcade Archiv kann nicht gelesen werden: ")+archive,error);}
        }
        return game with{Arguments=game.Arguments.Select(a=>Matches(a)?archive:a).ToArray(),SourcePath=archive,
            RequiredFiles=(game.RequiredFiles??[]).Append(folder).Append(archive).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()};
    }
    public static string DeviceId(string path, string kind)
    {
        string id = path.StartsWith(@"\\?\HID#", StringComparison.OrdinalIgnoreCase) ? path[8..] : path;
        return "raw_" + kind + "_" + id.Replace('=', '_').Replace('[', '_').Replace(']', '_');
    }
    public static bool Configure(GameEntry game, IEnumerable<GunBinding> bindings, IEnumerable<InputDevice> enumeration, IReadOnlyDictionary<string, string> names)
    {
        if (!IsFlycast(game)) return false;
        var devices = enumeration.Where(d => d.Kind is "mouse" or "keyboard").ToArray();
        var players = bindings.Where(b => b.Player is 1 or 2 && devices.Any(d => d.Kind == "mouse" && d.Id.Equals(b.MouseId, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (players.Length == 0) return false;
        var values = new Dictionary<string, string> { ["RawInput"] = "yes", ["maple_sdl_mouse"] = "-1", ["maple_sdl_keyboard"] = "-1" };
        var mappings=new List<(string Path,string Text)>();
        foreach (var device in devices)
        {
            var binding = players.SingleOrDefault(b => (device.Kind == "mouse" ? b.MouseId : b.KeyboardId)?.Equals(device.Id, StringComparison.OrdinalIgnoreCase) == true);
            values["maple_" + DeviceId(device.Id, device.Kind)] = binding is null ? "-1" : (binding.Player - 1).ToString();
            if (binding is null) continue;
            string id = device.Id.StartsWith(@"\\?\HID#", StringComparison.OrdinalIgnoreCase) ? device.Id[8..] : device.Id;
            string name = names.GetValueOrDefault(device.Id, device.Kind=="mouse"?"Mouse":"Keyboard") + " [" + id.Split('#')[0] + "]";
            string filename = "RAW_" + name + "-" + DeviceId(device.Id, device.Kind);
            foreach (char invalid in "/\\:?*|\"<>") filename = filename.Replace(invalid, '-');
            foreach (string suffix in new[] { "", "_arcade" })
            {
                string path = Path.Combine(game.WorkingDirectory, "mappings", filename + suffix + ".cfg");
                mappings.Add((path,Mapping(binding,device.Kind)));
            }
        }
        for (int p = 1; p <= 2; p++) values["device" + p] = players.Any(b => b.Player == p) ? "7" : "10";
        foreach(var mapping in mappings)WriteMapping(mapping.Path,mapping.Text);
        DolphinSetup.WriteMerged(Path.Combine(game.WorkingDirectory, "emu.cfg"), "input", values);
        DolphinSetup.WriteMerged(Path.Combine(game.WorkingDirectory, "emu.cfg"), "window", new Dictionary<string, string> { ["fullscreen"] = "yes" });
        return true;
    }
    private static string Mapping(GunBinding binding,string kind)
    {
        var binds = new List<string>();
        var assigned=new Dictionary<int,string>();
        // Flycast Mouse::Button reverses left/right numeric codes (2/1).
        // Its lightgun reload input is named "reload", not DC button B.
        // core/input/mouse.h, mapping.cpp and hw/maple/maple_devs.cpp upstream.
        foreach(var (action,native) in new[]{("shoot","btn_a"),("reload","reload"),("secondary","btn_b"),("start","btn_start"),("coin","btn_d"),("up","btn_dpad1_up"),("down","btn_dpad1_down"),("left","btn_dpad1_left"),("right","btn_dpad1_right")})
        {
            string? token=StartupControls.ActionToken(binding,action);
            if(token is null || !token.StartsWith(kind=="mouse"?"mouse:":"key:"))continue;
            int key=int.Parse(token[(token.IndexOf(':')+1)..]);
            int code=kind=="mouse"?key switch{1=>2,2=>1,>=3 and <=5=>key,_=>-1}:
                key switch { >=65 and <=90 => key - 65 + 4, >=49 and <=57 => key - 49 + 30, 48=>39, 13=>40, 32=>44, 37=>80, 38=>82, 39=>79, 40=>81, _=>-1 };
            if(code>=0)
            {
                if(assigned.TryGetValue(code,out var other))throw new InvalidOperationException(I18n.F($"P{binding.Player}: Flycast Aktionen {other} und {native} benötigen getrennte Gun Tasten."));
                assigned[code]=native;binds.Add($"bind{binds.Count} = {code}:{native}");
            }
        }
        return "[digital]\n" + string.Join('\n', binds) + "\n\n[emulator]\nmapping_name = Deadeye Gun\nversion = 3\n";
    }
    public static GameControls Read(GameEntry game,IEnumerable<GunBinding> bindings,IEnumerable<InputDevice> enumeration)
    {
        const string note="Flycast Basisprofile der Geräte. Spielspezifische Überschreibungen und Reaktionen im Spiel sind noch nicht geprüft.";
        string config=Path.Combine(game.WorkingDirectory,"emu.cfg"),folder=Path.Combine(game.WorkingDirectory,"mappings");
        if(!File.Exists(config)||!Directory.Exists(folder))return new([],I18n.T(note));
        string settings=File.ReadAllText(config);
        if(DolphinSetup.Value(settings,"input","RawInput")!="yes")return new([],I18n.T(note));
        var rows=new List<ControlRow>();var devices=enumeration.ToArray();
        bool? arcade=game.Platform.Contains("Dreamcast",StringComparison.OrdinalIgnoreCase)?false:game.Platform.Contains("Naomi",StringComparison.OrdinalIgnoreCase)||game.Platform.Contains("Atomiswave",StringComparison.OrdinalIgnoreCase)?true:null;
        foreach(var binding in bindings.Where(b=>b.Player is 1 or 2))
        {
            if(DolphinSetup.Value(settings,"input","device"+binding.Player)!="7")continue;
            foreach(var device in devices.Where(d=>(d.Kind=="mouse"?binding.MouseId:d.Kind=="keyboard"?binding.KeyboardId:null)?.Equals(d.Id,StringComparison.OrdinalIgnoreCase)==true))
            {
                string id=DeviceId(device.Id,device.Kind);
                if(DolphinSetup.Value(settings,"input","maple_"+id)!=(binding.Player-1).ToString())continue;
                if(device.Kind=="mouse")rows.Add(new(binding.Player,"Aim",I18n.T("Zielen / Mausbewegung"),"Flycast RawInput","P"+binding.Player+"|aim",true));
                var suffixes=arcade is null?new[]{"-"+id+".cfg","-"+id+"_arcade.cfg"}:new[]{"-"+id+(arcade.Value?"_arcade":"")+".cfg"};
                var files=Directory.GetFiles(folder,"RAW_*.cfg").Where(f=>suffixes.Any(s=>Path.GetFileName(f).EndsWith(s,StringComparison.OrdinalIgnoreCase))).ToArray();
                if(files.Length==0)continue;
                var profiles=files.Select(File.ReadAllText).Distinct().ToArray();if(profiles.Length!=1)continue;
                string text=profiles[0];
                for(int i=0;i<128;i++)
                {
                    string? bind=DolphinSetup.Value(text,"digital","bind"+i);if(string.IsNullOrWhiteSpace(bind))break;
                    var parts=bind.Split(':');if(parts.Length!=2||!int.TryParse(parts[0],out int code))break;
                    string? function=parts[1] switch{"btn_a"=>I18n.T("Abzug"),"reload"=>I18n.T("Nachladen"),"btn_b"=>"B","btn_start"=>"Start","btn_d" when arcade!=false=>I18n.T("Münze"),"btn_dpad1_up"=>I18n.T("Oben"),"btn_dpad1_down"=>I18n.T("Unten"),"btn_dpad1_left"=>I18n.T("Links"),"btn_dpad1_right"=>I18n.T("Rechts"),_=>null};
                    if(function is null)continue;
                    int key=device.Kind=="mouse"?code switch{2=>1,1=>2,>=3 and <=5=>code,_=>-1}:code switch{>=4 and <=29=>code-4+65,>=30 and <=38=>code-30+49,39=>48,40=>13,44=>32,80=>37,82=>38,79=>39,81=>40,_=>-1};
                    string? token=key<0?null:(device.Kind=="mouse"?"mouse:":"key:")+key;
                    rows.Add(new(binding.Player,function,token is null?I18n.T("Nicht belegt"):OverlayControls.InputName(token),"Flycast RawInput",token is null?null:"P"+binding.Player+"|"+token,SourceKey:parts[1]));
                }
            }
        }
        return new(rows.ToArray(),I18n.T(note));
    }
    private static void WriteMapping(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path) && !File.Exists(path + ".before-deadeye-input")) File.Copy(path, path + ".before-deadeye-input");
        File.WriteAllText(path + ".deadeye-new", text); File.Move(path + ".deadeye-new", path, true);
    }
}
