using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeStoneContinuationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_STONE_SAVE") ?? throw new InvalidOperationException("Select a native stone continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        HashSet<int> stones = [];
        session.NativeInteractionScriptRequested += admission =>
        {
            if (session.MapId == 646 && session.Machine.Input.RawPressed != 0 && admission is { Script: 4, Accepted: true, Started: true, Entity: >= 12 and <= 16 })
            {
                stones.Add(admission.Entity);
                Console.WriteLine($"Actual stone entry: entity{admission.Entity}, tick{session.Machine.Tick}, rawPressed={session.Machine.Input.RawPressed:X4}");
            }
        };
        var proof = session.ReadNavigationEntryProof(12, 4, session.Machine.Input);
        var demand = session.Machine.ReadEntryInputDemand(12, 4);
        Console.WriteLine($"Stone entry: invariantProof={proof.Complete}/{proof.HasNoEffect}, currentDemand={demand?.Kind}/{demand?.Mask:X4}/PC{demand?.ProgramCounter:X4}");
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 1000 && session.MapId != 647 && timer.Elapsed.TotalSeconds < 30 && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Stone continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}, status={policy.State.Status}, error={controller.Error}");
        if (session.MapId != 647 || stones.Count != 5 || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal stone continuation did not reach its native destination without changing the original fixture.");
        }
    }
}
