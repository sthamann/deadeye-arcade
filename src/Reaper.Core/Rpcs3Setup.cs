using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reaper.Core;

public static class Rpcs3Setup
{
    public static bool IsRpcs3(GameEntry game) => game.Source != "teknoparrot" && Path.GetFileName(game.Executable).Equals("rpcs3.exe", StringComparison.OrdinalIgnoreCase);
    public static string Root(GameEntry game)
    {
        string root=Path.GetDirectoryName(game.Executable)!;
        return Directory.Exists(Path.Combine(root,"portable"))?Path.Combine(root,"portable"):root;
    }
    // Windows RPCS3 fs::get_config_dir(true) adds config/ for input YAML.
    public static string InputRoot(GameEntry game) => Path.Combine(Root(game),"config");
    public static string? TitleId(GameEntry game)
    {
        string? boot = game.Arguments.FirstOrDefault(a => Path.GetFileName(a).Equals("EBOOT.BIN", StringComparison.OrdinalIgnoreCase));
        if (boot is null) return null;
        string path = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(boot, game.WorkingDirectory)))!, "PARAM.SFO");
        if (!File.Exists(path) || new FileInfo(path).Length > 1_048_576) return null;
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46535000) return null;
        uint keys = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)), data = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)), count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
        if (count > (bytes.Length - 20) / 16) return null;
        for (int i = 0; i < count; i++)
        {
            var entry = bytes.AsSpan(20 + i * 16, 16);
            long key = (long)keys + BinaryPrimitives.ReadUInt16LittleEndian(entry), value = (long)data + BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            if (key >= bytes.Length || value + length > bytes.Length) continue;
            int end = Array.IndexOf(bytes, (byte)0, (int)key);
            if (end < 0 || Encoding.UTF8.GetString(bytes, (int)key, end - (int)key) != "TITLE_ID") continue;
            string id = Encoding.UTF8.GetString(bytes, (int)value, (int)length).TrimEnd('\0');
            return Regex.IsMatch(id, "^[A-Z]{4}[0-9]{5}$") ? id : null;
        }
        return null;
    }
    public static bool SupportsRawMouse(string executable)
    {
        if (!File.Exists(executable)) return false;
        using var input = File.OpenRead(executable);
        byte[] buffer = new byte[262_144 + 16]; int retained = 0, read;
        while ((read = input.Read(buffer, retained, buffer.Length - retained)) > 0)
        {
            int size = read + retained;
            if (buffer.AsSpan(0, size).IndexOf("Raw Mouse"u8) >= 0) return true;
            retained = Math.Min(16, size); buffer.AsSpan(size - retained, retained).CopyTo(buffer);
        }
        return false;
    }
    // RPCS3 uses GetKeyNameText's scan-code format, not a Windows virtual key.
    public static int EncodeKey(int key)
    {
        uint scan = OperatingSystem.IsWindows() ? MapVirtualKey((uint)key, 4) : key switch {
            49 => 2, 50 => 3, 53 => 6, 54 => 7, 77 => 50, 78 => 49, 81 => 16, 83 => 31,
            38 => 0xe048, 40 => 0xe050, 37 => 0xe04b, 39 => 0xe04d, 85 => 22, 86 => 47, 87 => 17, 88 => 45, _ => 0
        };
        if (scan == 0) throw new ArgumentException("RPCS3 cannot map this keyboard control.");
        // Some Windows layouts return the non-extended scan for navigation keys.
        // Raw Mouse records E0 for these keys; preserve that bit explicitly.
        bool extended = (scan & 0xff00) != 0 || key is 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 45 or 46 or 111 or 163 or 165;
        return (int)(((scan & 0xff) | (extended ? 0x100u : 0)) << 16);
    }
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] private static extern uint MapVirtualKey(uint code, uint type);
    private static string RawToken(string token) => token.StartsWith("mouse:") ? "Button " + token[6..] : "Key " + EncodeKey(int.Parse(token[4..]));

    public static GameEntry Configure(GameEntry game, IEnumerable<GunBinding> bindings)
    {
        if (!IsRpcs3(game) || !SupportsRawMouse(game.Executable)) return game;
        var players = bindings.Where(b => b.Player is 1 or 2).ToArray();
        string root = Root(game); string? id = TitleId(game);
        foreach (string file in new[] { Path.Combine(root, "config.yml"), id is null ? null : Path.Combine(root, "config", "custom_configs", "config_" + id + ".yml") }.OfType<string>())
        {
            MergeYaml(file, ["Input/Output"], new() { ["Mouse"] = "Raw", ["Move"] = "Raw Mouse", ["Camera"] = "Fake", ["Camera type"] = "PS Eye", ["Background input enabled"] = "true", ["Show move cursor"] = "true" });
            MergeYaml(file, ["Video", "Vulkan"], new() { ["Adapter"] = "\"\"" });
            MergeYaml(file, ["Miscellaneous"], new() { ["Automatically start games after boot"] = "true", ["Start games in fullscreen mode"] = "true", ["Exit RPCS3 when process finishes"] = "true" });
        }
        for (int p = 1; p <= 4; p++)
        {
            var binding = players.FirstOrDefault(b => b.Player == p);
            var buttons = new Dictionary<string, string> { ["Device"] = JsonSerializer.Serialize(binding?.MouseId ?? ""), ["Mouse Acceleration"] = "100" };
            string[] controls = ["trigger", "reload", "magazine", "side", "stick", "start", "coin", "up"];
            var hardware = binding is null ? [] : StartupControls.Hardware(binding);
            for (int i = 0; i < controls.Length; i++)
            {
                string? token = hardware.FirstOrDefault(h => h.Id == controls[i])?.Token;
                buttons["Button " + (i + 1)] = JsonSerializer.Serialize(token is null ? "" : RawToken(token));
            }
            MergeYaml(Path.Combine(InputRoot(game), "raw_mouse.yml"), ["Player " + p], buttons);
            // Disable inherited chord mappings: each action has its own physical input.
            var move = new Dictionary<string, string> { ["Start"] = "Mouse Button 6", ["Select"] = "Mouse Button 7", ["Triangle"] = "Mouse Button 8", ["Circle"] = "Mouse Button 4", ["Cross"] = "Mouse Button 5", ["Square"] = "Mouse Button 3", ["Move"] = "Mouse Button 2", ["T"] = "Mouse Button 1", ["External Device"] = "Disconnected" };
            foreach (string action in new[] { "Combo", "Combo Start", "Combo Select", "Combo Triangle", "Combo Circle", "Combo Cross", "Combo Square", "Combo Move", "Combo T" }) move[action] = "\"\"";
            MergeYaml(Path.Combine(InputRoot(game), "gem_mouse.yml"), ["Player " + p], move);
        }
        return game with { Arguments = game.Arguments.Contains("--no-gui") ? game.Arguments : [.. game.Arguments, "--no-gui"] };
    }

    public static GameControls Read(GameEntry game, IEnumerable<GunBinding> bindings)
    {
        string root = InputRoot(game), raw = Path.Combine(root, "raw_mouse.yml"), gem = Path.Combine(root, "gem_mouse.yml");
        if (!File.Exists(raw) || !File.Exists(gem)) return new([], I18n.T("Die RPCS3-Raw-Mouse-Belegung ist noch nicht eingerichtet."));
        string rawText = File.ReadAllText(raw), gemText = File.ReadAllText(gem); var rows = new List<ControlRow>();
        foreach (var binding in bindings.Where(b => b.Player is 1 or 2))
        {
            string section = "Player " + binding.Player;
            if (YamlValue(rawText, [section], "Device") != binding.MouseId) continue;
            foreach (string function in new[] { "T", "Move", "Square", "Circle", "Cross", "Triangle", "Start", "Select" })
            {
                string? logical = YamlValue(gemText, [section], function);
                var match = Regex.Match(logical ?? "", "^Mouse Button ([1-8])$"); if (!match.Success) continue;
                string? actual = YamlValue(rawText, [section], "Button " + match.Groups[1].Value);
                string? token = StartupControls.Hardware(binding).Select(h => h.Token).OfType<string>().FirstOrDefault(t => RawToken(t) == actual);
                string label = function switch { "T" => I18n.T("Schießen (T)"), "Move" => I18n.T("Move / Deckung"), "Select" => "Select", _ => function };
                rows.Add(new(binding.Player, label, actual ?? "Unassigned", "RPCS3 · Raw Mouse", token is null ? null : "P" + binding.Player + "|" + token));
            }
        }
        return new(rows.ToArray(), I18n.T("Aktive RPCS3-Raw-Mouse-/PS-Move-Belegung. Vor dem Spielen die spielinterne Kalibrierung für jeden Spieler abschließen."));
    }

    // RPCS3's emitted YAML uses block mappings. Preserve unrelated settings and comments.
    public static string MergeYamlText(string text, string[] path, Dictionary<string, string> values)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList(); int start = -1, end = lines.Count, indent = 0;
        foreach (string section in path)
        {
            int found = Enumerable.Range(start + 1, end - start - 1).FirstOrDefault(i => Mapping(lines[i], indent, out string key, out _) && key == section, -1);
            if (found < 0) { lines.Insert(end, new string(' ', indent) + section + ":"); found = end; end++; }
            start = found; int boundary = start + 1;
            while (boundary < lines.Count && (string.IsNullOrWhiteSpace(lines[boundary]) || lines[boundary].TrimStart().StartsWith('#') || lines[boundary].TakeWhile(char.IsWhiteSpace).Count() > indent)) boundary++;
            end = boundary; indent += 2;
        }
        foreach (var value in values)
        {
            var indices = Enumerable.Range(start + 1, end - start - 1).Where(i => Mapping(lines[i], indent, out string key, out _) && key == value.Key).ToArray();
            string line = new string(' ', indent) + value.Key + ": " + value.Value;
            if (indices.Length == 0) { lines.Insert(end, line); end++; }
            else { lines[indices[0]] = line; foreach (int i in indices.Skip(1).Reverse()) { lines.RemoveAt(i); end--; } }
        }
        return string.Join(text.Contains("\r\n") ? "\r\n" : "\n", lines);
    }
    public static string? YamlValue(string text, string[] path, string key)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n'); int start = -1, end = lines.Length, indent = 0;
        foreach (string section in path)
        {
            start = Enumerable.Range(start + 1, end - start - 1).FirstOrDefault(i => Mapping(lines[i], indent, out string name, out _) && name == section, -1);
            if (start < 0) return null;
            end = start + 1; while (end < lines.Length && (string.IsNullOrWhiteSpace(lines[end]) || lines[end].TrimStart().StartsWith('#') || lines[end].TakeWhile(char.IsWhiteSpace).Count() > indent)) end++;
            indent += 2;
        }
        foreach (int i in Enumerable.Range(start + 1, end - start - 1)) if (Mapping(lines[i], indent, out string name, out string value) && name == key)
            return value.StartsWith('"') ? JsonSerializer.Deserialize<string>(value) : value.Trim('\'');
        return null;
    }
    private static bool Mapping(string line, int indent, out string key, out string value)
    {
        key = value = ""; if (line.TakeWhile(char.IsWhiteSpace).Count() != indent) return false;
        var match = Regex.Match(line.Trim(), "^([^#][^:]*):\\s*(.*)$"); if (!match.Success) return false;
        key = match.Groups[1].Value.Trim(); value = match.Groups[2].Value.Trim(); return true;
    }
    private static void MergeYaml(string file, string[] path, Dictionary<string, string> values)
    {
        string old = File.Exists(file) ? File.ReadAllText(file) : "", text = MergeYamlText(old, path, values); if (old == text) return;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        if (File.Exists(file) && !File.Exists(file + ".before-deadeye-input")) File.Copy(file, file + ".before-deadeye-input");
        File.WriteAllText(file + ".deadeye-new", text); File.Move(file + ".deadeye-new", file, true);
    }
}
