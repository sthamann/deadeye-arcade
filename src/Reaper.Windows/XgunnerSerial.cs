using System.IO.Ports;
using Reaper.Core;

namespace Reaper.Windows;

// Commands documented in the vendor's X-GUNNER manual. Never send RS3 commands
// to this receiver, and never infer a port from its COM number alone.
internal static class XgunnerSerial
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    public static async Task Send(PhysicalGun gun, params string[] commands)
    {
        if (gun.SystemId != "xgunner" || gun.Port is null ||
            commands.Any(c => c is not ("G" or "V" or "Q" or "E" or "S6" or "F0" or "F1")))
            throw new ArgumentException(I18n.T("Ungültiger XGunner-Befehl."));
        await gate.WaitAsync();
        try
        {
            using var raw = new RawInput(); raw.Refresh();
            if (!GunDiscovery.Scan(raw.Devices).Any(g => g.Id == gun.Id && g.SystemId == "xgunner" && g.Port == gun.Port && g.DriverHealthy))
                throw new IOException(I18n.T("Der XGunner-Empfänger ist nicht mehr verbunden."));
            using var port = new SerialPort(gun.Port, 115200, Parity.None, 8, StopBits.One) { WriteTimeout = 800, ReadTimeout = 800 };
            port.Open();
            try { await ReaperCommandDelivery.Send(commands, port.Write, () => port.BytesToWrite); }
            finally
            {
                if (commands.Contains("S6") && port.IsOpen)
                    await ReaperCommandDelivery.Send(["E"], port.Write, () => port.BytesToWrite);
            }
        }
        finally { gate.Release(); }
    }
}
