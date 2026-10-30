using System.Text;

namespace Reaper.Core;

public static class WindowsArguments
{
    // CommandLineToArgvW/CRT quoting, including trailing slashes and empty args.
    public static string Quote(string value)
    {
        if (value.Contains('\0')) throw new ArgumentException("An argument contains a null character.");
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
