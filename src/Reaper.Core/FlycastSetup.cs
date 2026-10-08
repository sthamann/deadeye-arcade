namespace Reaper.Core;

public static class FlycastSetup
{
    public static bool IsFlycast(GameEntry game) => Path.GetFileName(game.Executable).Equals("flycast.exe", StringComparison.OrdinalIgnoreCase);
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
        foreach (var device in devices)
        {
            var binding = players.SingleOrDefault(b => (device.Kind == "mouse" ? b.MouseId : b.KeyboardId)?.Equals(device.Id, StringComparison.OrdinalIgnoreCase) == true);
            values["maple_" + DeviceId(device.Id, device.Kind)] = binding is null ? "-1" : (binding.Player - 1).ToString();
            if (binding is null || device.Kind != "keyboard") continue;
            string id = device.Id.StartsWith(@"\\?\HID#", StringComparison.OrdinalIgnoreCase) ? device.Id[8..] : device.Id;
            string name = names.GetValueOrDefault(device.Id, "Keyboard") + " [" + id.Split('#')[0] + "]";
            string filename = "RAW_" + name + "-" + DeviceId(device.Id, "keyboard");
            foreach (char invalid in "/\\:?*|\"<>") filename = filename.Replace(invalid, '-');
            foreach (string suffix in new[] { "", "_arcade" })
            {
                string path = Path.Combine(game.WorkingDirectory, "mappings", filename + suffix + ".cfg");
                WriteMapping(path, KeyboardMapping(binding));
            }
        }
        for (int p = 1; p <= 2; p++) values["device" + p] = players.Any(b => b.Player == p) ? "7" : "10";
        DolphinSetup.WriteMerged(Path.Combine(game.WorkingDirectory, "emu.cfg"), "input", values);
        DolphinSetup.WriteMerged(Path.Combine(game.WorkingDirectory, "emu.cfg"), "window", new Dictionary<string, string> { ["fullscreen"] = "yes" });
        return true;
    }
    private static string KeyboardMapping(GunBinding binding)
    {
        var actions = binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player, binding.SystemId);
        var binds = new List<string>();
        foreach (var pair in actions.Where(p => p.Key.StartsWith("key:")))
        {
            int key = int.Parse(pair.Key[4..]);
            int code = key switch { >=65 and <=90 => key - 65 + 4, >=49 and <=57 => key - 49 + 30, 48=>39, 13=>40, 32=>44, 37=>80, 38=>82, 39=>79, 40=>81, _=>-1 };
            string? action = pair.Value switch { "start"=>"btn_start", "coin"=>"btn_d", "up"=>"btn_dpad1_up", "down"=>"btn_dpad1_down", "left"=>"btn_dpad1_left", "right"=>"btn_dpad1_right", "secondary"=>"btn_b", _=>null };
            if (code >= 0 && action is not null) binds.Add($"bind{binds.Count} = {code}:{action}");
        }
        return "[digital]\n" + string.Join('\n', binds) + "\n\n[emulator]\nmapping_name = Deadeye RS3\nversion = 3\n";
    }
    private static void WriteMapping(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path) && !File.Exists(path + ".before-deadeye-input")) File.Copy(path, path + ".before-deadeye-input");
        File.WriteAllText(path + ".deadeye-new", text); File.Move(path + ".deadeye-new", path, true);
    }
}
