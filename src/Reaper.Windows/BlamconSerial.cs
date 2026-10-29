using System.IO.Ports;
using Reaper.Core;

namespace Reaper.Windows;

internal static class BlamconSerial
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    public static async Task Send(PhysicalGun gun, params string[] commands)
    {
        if (gun.SystemId != "blamcon" || gun.Port is null || commands.Length == 0 || commands.Any(c => !BlamconSetup.ValidCommand(c)))
            throw new ArgumentException(I18n.T("Ungültiger Blamcon-Befehl."));
        await gate.WaitAsync();
        try
        {
            using var raw = new RawInput(); raw.Refresh();
            var current = GunDiscovery.Scan(raw.Devices).SingleOrDefault(g => g.Id == gun.Id && g.SystemId == "blamcon" && g.Port == gun.Port && g.DriverHealthy);
            if (current is null) throw new IOException(I18n.T("Die ausgewählte Blamcon ist nicht mehr verbunden."));
            // Manufacturer MAMEHooker examples use 9600,N,8,1 and '+' termination.
            using var port = new SerialPort(gun.Port, 9600, Parity.None, 8, StopBits.One) { WriteTimeout = 800, ReadTimeout = 800 };
            port.Open();
            try { await ReaperCommandDelivery.Send(commands.Select(c=>c+"+").ToArray(), port.Write, () => port.BytesToWrite); }
            finally
            {
                if (commands.Any(c=>c.StartsWith("SM.")) && port.IsOpen)
                    await ReaperCommandDelivery.Send(["FB.0.0+", "FB.1.0+", "ES+"], port.Write, () => port.BytesToWrite);
            }
        }
        finally { gate.Release(); }
    }
}
