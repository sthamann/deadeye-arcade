using System.Text.RegularExpressions;

namespace Reaper.Core;

public static class RetroArchSetup
{
    public static bool IsRetroArch(GameEntry game) => Path.GetFileName(game.Executable).Contains("retroarch", StringComparison.OrdinalIgnoreCase);
    public static string ProfilePath(GameEntry game, string data) => Path.Combine(data, "input-profiles", Regex.Replace(game.Id, "[^a-zA-Z0-9_-]", "_") + ".cfg");

    // WinRaw in the installed 1.14/1.19 builds uses the original OS enumeration,
    // zero based, including the RDP mouse and unnamed devices.
    public static GameEntry Configure(GameEntry game, string data, IEnumerable<GunBinding> bindings, IReadOnlyList<string> mouseOrder)
    {
        if (!IsRetroArch(game)) return game;
        string? core = null;
        for (int i = 0; i + 1 < game.Arguments.Length; i++) if (game.Arguments[i] == "-L") core = Path.GetFileName(game.Arguments[i + 1].Replace('\\', '/')).ToLowerInvariant();
        if (core is null) return game;
        var ports = new Dictionary<int, (int Device, int Player)>();
        var options = new Dictionary<string, string>();
        switch (core)
        {
            case "mednafen_psx_libretro.dll": case "mednafen_psx_hw_libretro.dll":
                ports[1] = (260, 1); ports[2] = (260, 2); options["beetle_psx_gun_input_mode"] = "lightgun"; break;
            case "mednafen_saturn_libretro.dll":
                ports[1] = (516, 1); ports[2] = (516, 2); options["beetle_saturn_virtuagun_input"] = "Lightgun"; break;
            case "flycast_libretro.dll":
                ports[1] = (4, 1); ports[2] = (4, 2); break;
            case "fceumm_libretro.dll":
                ports[1] = (1, 1); ports[2] = (258, 1); options["fceumm_zapper_mode"] = "lightgun"; break;
            case "snes9x_libretro.dll":
                ports[1] = (1, 1); ports[2] = (260, 1); options["snes9x_lightgun_mode"] = "Lightgun"; break;
            case "genesis_plus_gx_libretro.dll":
                if (game.Platform.Contains("Master", StringComparison.OrdinalIgnoreCase)) { ports[1] = (260, 1); ports[2] = (260, 2); }
                else { bool justifier = game.Title.Contains("Lethal Enforcers", StringComparison.OrdinalIgnoreCase) || game.Title.Contains("Snatcher", StringComparison.OrdinalIgnoreCase);
                    ports[1] = (1, 1); ports[2] = (justifier ? 772 : 516, 1); if (justifier) ports[3] = (0, 2); }
                options["genesis_plus_gx_gun_input"] = "lightgun"; break;
            case "opera_libretro.dll":
                ports[1] = (4, 1); ports[2] = (4, 2); break;
            default: return game; // Unsupported cores retain their existing settings.
        }
        string root = Path.GetDirectoryName(game.Executable)!;
        var values = new Dictionary<string, string> { ["input_driver"] = "raw", ["config_save_on_exit"] = "false", ["video_fullscreen"] = "true",
            ["input_enable_hotkey"] = "f11", ["input_enable_hotkey_btn"] = "nul", ["input_enable_hotkey_mbtn"] = "nul", ["input_enable_hotkey_axis"] = "nul", ["input_exit_emulator"] = "escape" };
        var players = bindings.Where(b => b.Player is 1 or 2).ToDictionary(b => b.Player);
        for (int port = 1; port <= 4; port++)
        {
            var assignment = ports.GetValueOrDefault(port);
            players.TryGetValue(assignment.Player, out var binding);
            int index = binding is null ? -1 : Enumerable.Range(0, mouseOrder.Count).FirstOrDefault(i => mouseOrder[i].Equals(binding.MouseId, StringComparison.OrdinalIgnoreCase), -1);
            string prefix = "input_player" + port + "_";
            values["input_libretro_device_p" + port] = (index < 0 ? 0 : assignment.Device).ToString();
            values[prefix + "mouse_index"] = index < 0 ? "-1" : index.ToString();
            values["deadeye_player" + port + "_physical"] = assignment.Player.ToString();
            // Remove inherited joypad buttons/axes; they can duplicate gun actions.
            foreach (string function in new[] { "gun_trigger", "gun_reload", "gun_aux_a", "gun_aux_b", "gun_aux_c", "gun_start", "gun_select", "gun_offscreen_shot", "gun_dpad_up", "gun_dpad_down", "gun_dpad_left", "gun_dpad_right", "start", "select", "a", "b", "up", "down", "left", "right" })
                foreach (string suffix in new[] { "", "_mbtn", "_btn", "_axis" }) values[prefix + function + suffix] = "nul";
            if (binding is null || index < 0) continue;
            string? Token(string action) => (binding.ButtonMap ?? GunSystems.DefaultMap(binding.Player, binding.SystemId)).FirstOrDefault(p => p.Value == action).Key;
            void Bind(string function, string? token)
            {
                if (token?.StartsWith("mouse:") == true) values[prefix + function + "_mbtn"] = token[6..];
                else if (token?.StartsWith("key:") == true && int.TryParse(token[4..], out int key))
                {
                    string? text = key switch { >= 48 and <= 57 => "num" + (char)key, >= 65 and <= 90 => ((char)key).ToString().ToLowerInvariant(), 13 => "enter", 32 => "space", 37 => "left", 38 => "up", 39 => "right", 40 => "down", _ => null };
                    if (text is not null) values[prefix + function] = text;
                }
            }
            Bind("gun_trigger", Token("shoot")); Bind("gun_reload", Token("reload"));
            Bind("gun_start", Token("start")); Bind("start", Token("start"));
            Bind("gun_select", Token("coin")); Bind("select", Token("coin"));
            var hardware = StartupControls.Hardware(binding);
            Bind("gun_aux_a", hardware.FirstOrDefault(c => c.Id == "magazine")?.Token);
            Bind("gun_aux_b", hardware.FirstOrDefault(c => c.Id == "side")?.Token);
            foreach (string direction in new[] { "up", "down", "left", "right" }) { var token = hardware.FirstOrDefault(c => c.Id == direction)?.Token; Bind("gun_dpad_" + direction, token); Bind(direction, token); }
        }
        string path = ProfilePath(game, data); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (options.Count > 0)
        {
            string optionPath = Path.ChangeExtension(path, ".opt");
            string existing = Path.Combine(root, "retroarch-core-options.cfg");
            var merged = new Dictionary<string, string>();
            if (File.Exists(existing)) foreach (string line in File.ReadLines(existing)) { var pair = line.Split('=', 2); if (pair.Length == 2 && !pair[0].TrimStart().StartsWith('#')) merged[pair[0].Trim()] = pair[1].Trim().Trim('"'); }
            foreach (var pair in options) merged[pair.Key] = pair.Value;
            Write(optionPath, merged); values["core_options_path"] = optionPath.Replace('\\', '/'); values["game_specific_options"] = "false";
        }
        Write(path, values);
        var args = new List<string>(); var append = new List<string>();
        for (int i = 0; i < game.Arguments.Length; i++)
            if (game.Arguments[i] == "--appendconfig" && i + 1 < game.Arguments.Length) append.AddRange(game.Arguments[++i].Split('|'));
            else args.Add(game.Arguments[i]);
        append.Add(path); args.Add("--appendconfig"); args.Add(string.Join('|', append.Distinct(StringComparer.OrdinalIgnoreCase)));
        return game with { Arguments = args.ToArray() };
    }
    private static void Write(string path, Dictionary<string, string> values) => File.WriteAllLines(path, values.Select(p => p.Key + " = \"" + p.Value + "\""));
}
