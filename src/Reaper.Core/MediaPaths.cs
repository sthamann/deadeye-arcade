namespace Reaper.Core;

public static class MediaPaths
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".mp4", ".webm", ".m4v" };
    public static string? Url(string? file, string root, string host)
    {
        if (string.IsNullOrWhiteSpace(file) || !Extensions.Contains(Path.GetExtension(file))) return null;
        string full = Path.GetFullPath(file), directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(directory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || !File.Exists(full)) return null;
        string relative = Path.GetRelativePath(root, full);
        // Do not serve links that escape the explicitly mapped media folder.
        string current = full;
        while (current.Length >= directory.TrimEnd(Path.DirectorySeparatorChar).Length)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return null;
            current = Path.GetDirectoryName(current) ?? "";
        }
        return "https://" + host + "/" + string.Join("/", relative.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));
    }
}
