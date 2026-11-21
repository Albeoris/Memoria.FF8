using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAltarInputOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_ALTAR_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native altar input fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        HashSet<int> pressedSites = [];
        var controller = new Ff7AutoPlayController(session, driver =>
        {
            var result = policy.ChooseInput(driver);
            if (result.Held == 0x20 && (session.Machine.Input.RawHeld & 0x20) == 0)
            {
                int source = session.Machine.Capture().Entities[0].ProgramCounter;
                pressedSites.Add(source);
                Console.WriteLine($"Native altar input: tick{session.Machine.Tick}, source{source:X4}, held{result.Held:X4}");
            }
            return result;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 1500 && timer.Elapsed.TotalSeconds < 20 && controller.Error is null && session.Host.Process.Movie?.PreparedId != 50; polls++)
        {
            controller.Advance(1);
        }
        bool knownBoundary = controller.Failure is FieldExecutionException { Context: { Entity: 3, Priority: 6, ProgramCounter: 0x05B6, Opcode: 0x74 } };
        Console.WriteLine($"Altar input continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, story={session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, tick={session.Machine.Tick}, pc={session.Machine.Capture().Entities[0].ProgramCounter:X4}, movie={session.Host.Process.Movie?.PreparedId}, nextNativeBoundary={knownBoundary}");
        if (pressedSites.Count != 5 || session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) != 671 || !knownBoundary && session.Host.Process.Movie?.PreparedId != 50 || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal altar input continuation did not reach its next authenticated native boundary or changed its original fixture.");
        }
    }
}
