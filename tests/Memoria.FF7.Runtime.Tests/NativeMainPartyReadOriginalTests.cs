using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMainPartyReadOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_MAIN_PARTY_SAVE") ?? throw new InvalidOperationException("Select a native main-party query fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        var saved = CombinedSessionCodec.Read(input);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved.Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 1500 && timer.Elapsed.TotalSeconds < 20 && controller.Error is null && session.Host.Process.Movie?.PreparedId != 50 && session.MapId == 647; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native main-party continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, director={session.Machine.Capture().Entities[0].ProgramCounter:X4}, movie={session.Host.Process.Movie?.PreparedId}, value={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE5)}, expected={saved.Ff7!.Progress.Party[1]}, error={controller.Error}");
        bool sourceUnchanged = hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)));
        if (!sourceUnchanged || controller.Error is not null || session.Host.Process.Movie?.PreparedId != 50 || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE5) != saved.Ff7!.Progress.Party[1])
        {
            throw new InvalidOperationException("The native main-party read did not preserve its exact byte and continue to the movie boundary, or its original fixture changed.");
        }
    }
}
