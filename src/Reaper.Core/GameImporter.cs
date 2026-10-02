using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Reaper.Core;

public static partial class GameImporter
{
    // Only curated gun titles are inferred from TeknoParrot names. A racing profile is not a lightgun game.
    private static readonly (string Title, string[] Aliases)[] TeknoCatalog = [
        ("House of the Dead: Scarlet Dawn", ["hotdsd", "scarletdawn"]),
        ("The House of the Dead 4", ["hotd4"]), ("The House of the Dead III", ["hotd3"]),
        ("Time Crisis 5", ["timecrisis5", "tc5"]), ("Time Crisis 4", ["timecrisis4", "tc4"]),
        ("Time Crisis 3", ["timecrisis3", "tc3"]), ("Jurassic Park Arcade", ["jurassicpark"]),
        ("Aliens: Armageddon", ["aliensarmageddon"]), ("Aliens: Extermination", ["aliensextermination"]),
        ("Terminator Salvation", ["terminatorsalvation"]), ("Operation G.H.O.S.T.", ["og", "operationghost"]),
        ("Transformers: Human Alliance", ["tha", "transformershumanalliance"]),
        ("Transformers: Shadows Rising", ["transformersshadowsrising"]), ("Rambo", ["rambo"]),
        ("Let's Go Jungle!", ["lgj", "letsgojungle"]),
        ("Let's Go Island!", ["lgi", "letsgoisland"]), ("Sega Golden Gun", ["segagoldengun", "goldengun"]),
        ("Luigi's Mansion Arcade", ["luigismansion"]), ("Lost Land Adventure", ["lostlandadventure"]),
        ("Ghost Squad Evolution", ["ghostsquadevolution"]), ("Virtua Cop 3", ["virtuacop3", "vcop3"]),
        ("House of the Dead EX", ["hotdex"]), ("2 Spicy", ["2spicy"]), ("Friction", ["friction"]),
        ("Big Buck Hunter Pro", ["bigbuckhunterpro"]), ("Silent Hill: The Arcade", ["silenthill"])
    ];
    public static string Normalize(string value) => NonAlpha().Replace(value.ToLowerInvariant(), "");
    [GeneratedRegex("[^a-z0-9]")] private static partial Regex NonAlpha();
    private static XDocument ReadXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        return XDocument.Load(reader);
    }
    private static string Value(XDocument doc, string name) => doc.Descendants()
        .FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
    public static ImportResult TeknoParrot(string root)
    {
        root = Path.GetFullPath(root);
        string exe = Path.Combine(root, "TeknoParrotUi.exe");
        string profiles = Path.Combine(root, "UserProfiles");
        if (!File.Exists(exe)) throw new InvalidDataException("TeknoParrotUi.exe fehlt im ausgewählten Ordner.");
        if (!Directory.Exists(profiles)) throw new InvalidDataException("Keine UserProfiles gefunden. Richte zunächst ein Spiel in TeknoParrot ein.");
        List<GameEntry> games = []; List<string> warnings = [];
        foreach (var path in Directory.EnumerateFiles(profiles, "*.xml"))
        {
            try
            {
                var doc = ReadXml(path);
                string name = Path.GetFileNameWithoutExtension(path);
                string title = Value(doc, "GameName");
                var match = TeknoCatalog.FirstOrDefault(c => c.Aliases.Any(a => Normalize(a) == Normalize(name))
                    || Normalize(c.Title) == Normalize(title));
                if (match.Title is null) { warnings.Add($"{name}: kein zugeordnetes Lightgun-Profil; ausgelassen."); continue; }
                string gamePath = Value(doc, "GamePath");
                string fullGamePath = string.IsNullOrWhiteSpace(gamePath) ? "" : Path.GetFullPath(gamePath, root);
                // The UI command requires a matching GameProfiles file even when UserProfiles exists.
                bool usable = File.Exists(fullGamePath) && File.Exists(Path.Combine(root, "GameProfiles", Path.GetFileName(path)));
                games.Add(new(Identity.For("TeknoParrot", path), match.Title, "TeknoParrot", exe,
                    ["--profile=" + Path.GetFileName(path)], root, "teknoparrot", path, usable ? "unverified" : "needs-setup"));
                if (!usable) warnings.Add($"{match.Title}: Spielpfad oder Basisprofil fehlt.");
            }
            catch (Exception e) when (e is XmlException or IOException or ArgumentException)
            { warnings.Add(Path.GetFileName(path) + ": " + e.Message); }
        }
        return new(games, warnings);
    }
    public static Dictionary<string, string> MameGunCatalog(Stream input)
    {
        var catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "machine")
            {
                var node = (XElement)XNode.ReadFrom(reader);
                string? name = (string?)node.Attribute("name");
                bool gun = node.Descendants("control").Any(c => (string?)c.Attribute("type") == "lightgun");
                bool device = (string?)node.Attribute("isdevice") == "yes" || (string?)node.Attribute("runnable") == "no";
                if (gun && !device && name is not null) catalog[name] = node.Element("description")?.Value ?? name;
            }
            else reader.Read();
        }
        return catalog;
    }
    public static ImportResult Mame(string exe, string romDirectory, Dictionary<string, string> catalog)
    {
        if (!File.Exists(exe)) throw new FileNotFoundException("MAME wurde nicht gefunden.", exe);
        List<GameEntry> games = []; List<string> warnings = [];
        foreach (var path in Directory.EnumerateFiles(romDirectory))
        {
            if (Path.GetExtension(path).ToLowerInvariant() is not (".zip" or ".7z")) continue;
            string key = Path.GetFileNameWithoutExtension(path);
            if (!catalog.TryGetValue(key, out var title)) continue;
            games.Add(new(Identity.For("MAME", path), title, "MAME", Path.GetFullPath(exe),
                [key, "-rompath", Path.GetFullPath(romDirectory), "-lightgun", "-lightgunprovider", "rawinput", "-multimouse", "-skip_gameinfo"],
                Path.GetDirectoryName(Path.GetFullPath(exe))!, "mame", path, Aspect: "4:3"));
        }
        return new(games, warnings);
    }
    public static GameEntry Pc(string exe, string? title = null)
    {
        if (!File.Exists(exe) || !Path.GetExtension(exe).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Bitte eine vorhandene Windows-Spielanwendung auswählen.");
        return new(Identity.For("PC", exe), title ?? Path.GetFileNameWithoutExtension(exe), "Windows",
            Path.GetFullPath(exe), [], Path.GetDirectoryName(Path.GetFullPath(exe))!, "pc", Path.GetFullPath(exe));
    }
}
