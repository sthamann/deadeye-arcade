namespace Reaper.Core;

public record RawInputRoute(ushort Page, ushort Usage, uint Flags, nint Window);

public static class InputRecovery
{
    public static ushort[] RequiredUsages => [2, 6, 4, 5];
    public static bool OwnsRegistration(IEnumerable<RawInputRoute> routes, nint window)
    {
        var current = routes.ToArray();
        return window != 0 && RequiredUsages.All(usage => current.Any(r =>
            r.Page == 1 && r.Usage == usage && r.Window == window && (r.Flags & 0x100) == 0x100));
    }
    public static string? Reason(int message, long parameter) => (message, parameter) switch
    {
        (0x2B1, 1) => "console-connect",
        (0x2B1, 3) => "remote-connect",
        (0x2B1, 4) => "remote-disconnect",
        (0x2B1, 5) => "session-logon",
        (0x2B1, 8) => "session-unlock",
        (0x2B1, 9) => "remote-control-change",
        (0x218, 6 or 7 or 18) => "power-resume",
        _ => null
    };
}
