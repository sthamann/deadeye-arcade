using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Reaper.Core;

public static class I18n
{
    private static readonly Dictionary<string, string> English = Load();
    private static string language = "en";
    public static string Language { get => language; set => language = Normalize(value); }
    public static string Normalize(string? value) => value == "de" ? "de" : "en";
    public static string T(string source) => Language == "de" ? source : English.GetValueOrDefault(source, source);
    public static string F(FormattableString source) => string.Format(Language == "de" ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.GetCultureInfo("en-GB"), T(source.Format), source.GetArguments());
    private static Dictionary<string, string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Reaper.Core.English.json")
            ?? throw new InvalidOperationException("Missing English language catalog.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? throw new InvalidOperationException("Invalid English language catalog.");
    }
}
