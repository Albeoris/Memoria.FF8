using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCurrentTouchOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RETURNED_LINE_SAVE") ?? throw new InvalidOperationException("Select a native callback fixture.");
        using FileStream input = File.OpenRead(path);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        if (session.HasCurrentNoEffectTouchCallback(8, 4, new(Held: 0x100, Pressed: 0x100, RawHeld: 0x100, RawPressed: 0x100), false))
        {
            throw new InvalidOperationException("A button with an additional session-helper publication acquired a VM-only callback certificate.");
        }
        if (!session.HasCurrentNoEffectTouchCallback(8, 4, new(), false) || before != JsonSerializer.Serialize(session.Capture()))
        {
            throw new InvalidOperationException("The actual neutral callback did not certify its no-effect entry without changing source VM, RNG, shared progress or process state.");
        }
        Console.WriteLine("Original current-callback proof preserves full source state and rejects the uncovered help-button publication.");
    }
}
