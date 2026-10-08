using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Reaper.Core;

// Dolphin's native DSU backend accepts float axes and per-device buttons.
// Carry absolute gun aim on two float axes, avoiding shared cursor movement
// and the precision loss of an eight-bit virtual joystick. Loopback only.
public sealed class DolphinGunBridge : IAsyncDisposable
{
    public const string DeviceName = "DeadeyeGuns";
    private const double Gravity = 9.80665;
    private readonly UdpClient socket = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource cancel = new();
    private readonly GunBinding[] bindings;
    private readonly Dictionary<int, (double X, double Y, HashSet<string> Down)> states = [];
    private readonly Dictionary<(IPEndPoint Client, int Player), DateTime> clients = [];
    private readonly object sync = new();
    private readonly Task receiving, sending;
    private uint counter;
    private bool paused;
    private readonly long[] subscriptions = new long[4], frames = new long[4];
    public int Port => ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    public DolphinGunBridge(IEnumerable<GunBinding> players)
    {
        bindings = players.Where(b => b.Player is 1 or 2).ToArray();
        foreach (var b in bindings) states[b.Player] = (0, 0, []);
        receiving = Receive(); sending = SendFrames();
    }
    public void Pause(bool value) { lock (sync) paused = value; }
    public void Mouse(string device, bool absolute, int x, int y, int buttons)
    {
        lock (sync) foreach (var b in bindings.Where(b => b.MouseId.Equals(device, StringComparison.OrdinalIgnoreCase)))
        {
            var state = states[b.Player];
            if (absolute) { state.X = Math.Clamp(x, 0, 65535) / 65535.0 * 2 - 1; state.Y = Math.Clamp(y, 0, 65535) / 65535.0 * 2 - 1; }
            for (int i = 0; i < 5; i++)
            {
                string token = "mouse:" + (i + 1);
                if ((buttons & (1 << (2 * i))) != 0) state.Down.Add(token);
                if ((buttons & (1 << (2 * i + 1))) != 0) state.Down.Remove(token);
            }
            states[b.Player] = state;
        }
    }
    public void Key(string device, int key, bool down)
    {
        lock (sync) foreach (var b in bindings.Where(b => b.KeyboardId?.Equals(device, StringComparison.OrdinalIgnoreCase) == true))
        { if (down) states[b.Player].Down.Add("key:" + key); else states[b.Player].Down.Remove("key:" + key); }
    }
    public object Snapshot() { lock (sync) return new { port = Port, subscriptions = subscriptions.ToArray(), frames = frames.ToArray() }; }
    public static string? Input(string physical) => physical switch
    { "trigger" => "L2", "reload" => "Cross", "magazine" => "Square", "side" => "Circle", "stick" => "R3", "start" => "Options", "coin" => "Share", "up" => "Pad N", "down" => "Pad S", "left" => "Pad W", "right" => "Pad E", _ => null };
    public static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte item in bytes) { crc ^= item; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u); }
        return ~crc;
    }
    private static byte[] Message(int length, uint type)
    {
        byte[] bytes = new byte[length]; "DSUS"u8.CopyTo(bytes); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 1001);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)(length - 16)); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0xdead0001);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), type); return bytes;
    }
    private byte[] PortInfo(int player, bool data)
    {
        byte[] bytes = Message(data ? 100 : 32, data ? 0x100002u : 0x100001u);
        bytes[20] = (byte)(player - 1); bytes[21] = states.ContainsKey(player) ? (byte)2 : (byte)0; bytes[22] = 2; bytes[23] = 1;
        bytes[24] = 0x02; bytes[25] = 0xde; bytes[26] = 0xad; bytes[29] = (byte)player; bytes[30] = 0x05; bytes[31] = data ? (byte)1 : (byte)0;
        return bytes;
    }
    public byte[] Frame(int player)
    {
        lock (sync)
        {
            byte[] bytes = PortInfo(player, true); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), ++counter);
            bytes[40] = bytes[41] = bytes[42] = bytes[43] = 128;
            if (states.TryGetValue(player, out var state))
            {
                var binding = bindings.First(b => b.Player == player);
                bool Held(string physical) => !paused && StartupControls.Hardware(binding).Any(h => h.Id == physical && h.Token is not null && state.Down.Contains(h.Token));
                if (Held("coin")) bytes[36] |= 1; if (Held("stick")) bytes[36] |= 4; if (Held("start")) bytes[36] |= 8;
                foreach (var (physical, offset) in new (string, int)[] { ("left", 44), ("down", 45), ("right", 46), ("up", 47), ("magazine", 48), ("reload", 49), ("side", 50), ("trigger", 55) }) if (Held(physical)) bytes[offset] = 255;
                // DSU's axis scale in Dolphin is 1 / gravity; signs follow its names.
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(76), (float)(-state.X / Gravity));
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(80), (float)(state.Y / Gravity));
            }
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(68), (ulong)(Environment.TickCount64 * 1000));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), Crc(bytes)); return bytes;
        }
    }
    private async Task Send(byte[] bytes, IPEndPoint client)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), Crc(bytes));
        try { await socket.SendAsync(bytes, client, cancel.Token); }
        catch(SocketException e) when(e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused) { }
    }
    private async Task Receive()
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try { packet=await socket.ReceiveAsync(cancel.Token); }
                catch(SocketException e) when(e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused) {continue;}
                byte[] bytes = packet.Buffer;
                if (!IPAddress.IsLoopback(packet.RemoteEndPoint.Address) || bytes.Length < 20 || !bytes.AsSpan(0, 4).SequenceEqual("DSUC"u8) || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)) + 16 != bytes.Length) continue;
                uint crc = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0); if (Crc(bytes) != crc) continue;
                uint type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
                if (type == 0x100000) { var reply = Message(24, type); BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(20), 1001); await Send(reply, packet.RemoteEndPoint); }
                else if (type == 0x100001 && bytes.Length >= 24)
                {
                    uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)); if (count > 4 || bytes.Length < 24 + count) continue;
                    foreach (byte pad in bytes.AsSpan(24, (int)count).ToArray()) if (pad < 4) await Send(PortInfo(pad + 1, false), packet.RemoteEndPoint);
                }
                else if (type == 0x100002 && bytes.Length == 28)
                {
                    int flags = bytes[20], pad = bytes[21];
                    lock (sync) for (int p = 1; p <= 4; p++)
                        if (states.ContainsKey(p) && (flags == 0 || (flags & 1) != 0 && pad == p - 1 || (flags & 2) != 0 && bytes[27] == p))
                        { clients[(packet.RemoteEndPoint, p)] = DateTime.UtcNow; subscriptions[p - 1]++; }
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException || e is SocketException && cancel.IsCancellationRequested) { }
    }
    private async Task SendFrames()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(4));
            while (await timer.WaitForNextTickAsync(cancel.Token))
            {
                (IPEndPoint Client, int Player)[] destinations;
                lock (sync) { foreach (var client in clients.Where(c => DateTime.UtcNow - c.Value > TimeSpan.FromSeconds(6)).Select(c => c.Key).ToArray()) clients.Remove(client); destinations = clients.Keys.ToArray(); }
                foreach (var destination in destinations)
                {
                    try {await socket.SendAsync(Frame(destination.Player), destination.Client, cancel.Token);lock(sync)frames[destination.Player-1]++;}
                    catch(SocketException e) when(e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused) {lock(sync)clients.Remove(destination);}
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException || e is SocketException && cancel.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        cancel.Cancel(); socket.Dispose(); await Task.WhenAll(receiving, sending); cancel.Dispose();
    }
}
