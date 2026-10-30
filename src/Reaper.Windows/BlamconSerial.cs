using System.IO.Ports;
using Reaper.Core;

namespace Reaper.Windows;

internal static class BlamconSerial
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static SerialPort Open(PhysicalGun gun)
    {
        using var raw = new RawInput(); raw.Refresh();
        if (!GunDiscovery.Scan(raw.Devices).Any(g=>g.Id==gun.Id && g.SystemId=="blamcon" && g.Port==gun.Port && g.DriverHealthy))
            throw new IOException(I18n.T("Die ausgewählte Blamcon ist nicht mehr verbunden."));
        var port=new SerialPort(gun.Port!,9600,Parity.None,8,StopBits.One){WriteTimeout=800,ReadTimeout=600,DtrEnable=true,RtsEnable=true};
        try {port.Open();port.DiscardInBuffer();return port;}catch{port.Dispose();throw;}
    }
    private static int Read(SerialPort port,int index)
    {
        port.WriteLine("ZD."+index);
        if(!int.TryParse(port.ReadLine().Trim(),out int value))throw new IOException("Invalid Vyper settings response.");
        return value;
    }
    public static async Task<BlamconDeviceSettings> ReadConfiguration(PhysicalGun gun, bool independentMagazine = false)
    {
        await gate.WaitAsync();
        try {return await Task.Run(()=>{
            using var port=Open(gun);
            int firmware=Read(port,1019);
            if(firmware is < 0x20100 or >= 0x30000)throw new IOException("Automatic Vyper settings require supported firmware 2.x.");
            // Each emulation mode has a different stored key map. Select mouse mode before reading keys.
            if(Read(port,3)!=0){port.WriteLine("ZY.3.0.0");port.WriteLine("ZY");Thread.Sleep(120);if(Read(port,3)!=0)throw new IOException("Vyper mouse mode was not accepted.");}
            var values=BlamconDeviceSettings.Indices.ToDictionary(i=>i,i=>Read(port,i));
            // Use independent magazine and mouse channels for the frontend and game adapters.
            bool changed=false;
            foreach(var setting in new[]{(Index:3,Value:0),(Index:6,Value:0)})
                if((setting.Index!=6 || independentMagazine) && values[setting.Index]!=setting.Value){port.WriteLine($"ZY.{setting.Index}.{setting.Value}.0");changed=true;}
            if(changed){port.WriteLine("ZY");Thread.Sleep(120);foreach(int index in new[]{3,6})values[index]=Read(port,index);}
            var snapshot=new BlamconDeviceSettings(firmware,values);snapshot.Validate();
            if(independentMagazine && snapshot.Get(6)!=0)throw new IOException("Vyper magazine configuration was not accepted.");
            return snapshot;
        });}finally{gate.Release();}
    }
    public static async Task Send(PhysicalGun gun, params string[] commands)
    {
        if(gun.SystemId!="blamcon"||gun.Port is null||commands.Length==0||commands.Any(c=>!BlamconSetup.ValidCommand(c)))
            throw new ArgumentException(I18n.T("Ungültiger Blamcon-Befehl."));
        await gate.WaitAsync();
        try {await Task.Run(async()=>{
            using var port=Open(gun);
            int? firmware=null;
            try{firmware=Read(port,1019);}catch(TimeoutException){port.DiscardInBuffer();port.Write("+");await Task.Delay(40);port.DiscardInBuffer();}
            if(firmware is >=0x20100 and <0x30000)
            {
                foreach(string command in BlamconDeviceSettings.Commands(commands)){port.WriteLine(command);await Task.Delay(40);}
                return;
            }
            if(firmware is >=0x30000)throw new IOException("Unsupported Vyper firmware version.");
            // Legacy firmware retains the published MAMEHooker protocol.
            try {await ReaperCommandDelivery.Send(commands.Select(c=>c+"+").ToArray(),port.Write,()=>port.BytesToWrite);}
            finally {if(commands.Any(c=>c.StartsWith("SM."))&&port.IsOpen)await ReaperCommandDelivery.Send(["FB.0.0+","FB.1.0+","ES+"],port.Write,()=>port.BytesToWrite);}
        });}finally{gate.Release();}
    }
}
