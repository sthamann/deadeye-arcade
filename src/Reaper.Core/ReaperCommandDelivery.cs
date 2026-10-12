namespace Reaper.Core;

// Keep the RS3 transport's timing testable independently of a physical COM port.
public static class ReaperCommandDelivery
{
    public static async Task Send(IEnumerable<string> commands, Action<string> write,
        Func<int> queuedBytes, Func<int, Task>? delay = null, Func<long>? clock = null)
    {
        delay ??= milliseconds => Task.Delay(milliseconds);
        clock ??= () => Environment.TickCount64;
        bool sent = false;
        foreach (string command in commands)
        {
            await delay(120);
            write(command);
            sent = true;
            long deadline = clock() + 800;
            while (queuedBytes() > 0)
            {
                if (clock() >= deadline) throw new IOException("RS3 serial output did not drain before its timeout.");
                await delay(20);
            }
        }
        // Allow the final command, especially normal-mode restore ZX, to settle
        // before closing. A drained host buffer alone does not acknowledge the
        // firmware. No recoil command is added.
        if (sent) await delay(120);
    }
}
