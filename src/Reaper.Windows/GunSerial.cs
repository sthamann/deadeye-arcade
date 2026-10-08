using Reaper.Core;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace Reaper.Windows;

public sealed class GunSerial
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,SemaphoreSlim> gates=new(StringComparer.OrdinalIgnoreCase);
    public Task<int> Probe(string port) => Command(port, 0);
    public async Task<int> Command(string port, int expectedPlayer, params string[] commands)
    {
        if (!SerialPort.GetPortNames().Contains(port, StringComparer.OrdinalIgnoreCase)) throw new IOException(I18n.T("Der ausgewählte Gun-Port ist nicht angeschlossen."));
        var gate=gates.GetOrAdd(port,_=>new(1,1));
        await gate.WaitAsync();
        try
        {
            return await Task.Run(async () =>
            {
                using var serial = new SerialPort(port, 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 800, WriteTimeout = 800 };
                serial.Open(); serial.DiscardInBuffer();
                string response = "";
                for(int attempt=0;attempt<3&&!Regex.IsMatch(response,@"id=[1-4]");attempt++)
                {
                    // A USB serial endpoint can take a moment to settle after opening.
                    await Task.Delay(120);serial.Write("ID");var end=DateTime.UtcNow.AddMilliseconds(900);
                    while(DateTime.UtcNow<end) {await Task.Delay(90);response+=serial.ReadExisting();if(Regex.IsMatch(response,@"id=[1-4]"))break;}
                }
                var match = Regex.Match(response, @"id=([1-4])");
                if (!match.Success) throw new IOException(I18n.T("Keine RS3-ID-Antwort. Der Port bleibt unverändert."));
                int player = int.Parse(match.Groups[1].Value);
                if (expectedPlayer > 0 && player != expectedPlayer) throw new IOException(I18n.F($"Gun meldet Spieler {player}; erwartet wurde {expectedPlayer}. DIP-Spielerzuordnung prüfen."));
                bool external = false;
                try
                {
                    foreach (var command in commands) { await Task.Delay(120); serial.Write(command); if (command == "ZS") external = true; if (command == "ZX") external = false; }
                }
                finally
                {
                    if (external && serial.IsOpen) try { await Task.Delay(120); serial.Write("ZX"); } catch (IOException) { }
                }
                return player;
            });
        }
        finally { gate.Release(); }
    }
}
