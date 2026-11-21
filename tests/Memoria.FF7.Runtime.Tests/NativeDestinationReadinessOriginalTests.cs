using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDestinationReadinessOriginalTests
{
    internal static void RunBackgroundContinuation()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DESTINATION_READINESS_SAVE") ?? throw new InvalidOperationException("Select a native background destination fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The destination continuation must not explore optional choices."));
        ushort destination = policy.State.DestinationField ?? throw new InvalidOperationException("The source has no retained destination intent.");
        int source = session.MapId;
        var vm = session.Machine.Capture();
        int npc = Enumerable.Range(0, vm.Entities.Length).First(entity => vm.Entities[entity].Priority < 7 && session.Host.Process.Actors.Any(actor => actor.Entity == entity && actor.EntityBound && !actor.PlayerControlled));
        var proof = session.Machine.AnalyzeScriptAtProgramCounter(npc, vm.Entities[npc].ProgramCounter);
        Console.WriteLine($"Original background body: entity={npc}, complete={proof.Complete}, independentLoop={proof.IndependentActorMotionLoop}, reason={proof.Reason}");
        var initialPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var initialNpc = session.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == npc);
        bool moved = false;
        bool backgroundAdvanced = false;
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 3000 && timer.Elapsed.TotalSeconds < 45 && controller.Error is null && session.MapId != destination; polls++)
        {
            controller.Advance(1);
            if (session.MapId == source)
            {
                var player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                var actor = session.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == npc);
                moved |= player.X4096 != initialPlayer.X4096 || player.Y4096 != initialPlayer.Y4096;
                backgroundAdvanced |= actor.X4096 != initialNpc.X4096 || actor.Y4096 != initialNpc.Y4096;
            }
        }
        Console.WriteLine($"Original background destination: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Host.Process.Tick}, destination={destination}, playerMoved={moved}, backgroundAdvanced={backgroundAdvanced}, active={policy.State.Active?.EventId}, status={policy.State.Status}, error={controller.Error}");
        if (!proof.Complete || !proof.IndependentActorMotionLoop || !moved || !backgroundAdvanced || session.MapId != destination || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Independent native background motion still prevented actual destination travel.");
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DESTINATION_READINESS_SAVE") ?? throw new InvalidOperationException("Select a destination readiness fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        var saved = CombinedSessionCodec.Read(input);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved.Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The destination readiness regression reached a new choice."));
        bool moved = false;
        var controller = new Ff7AutoPlayController(session, driver =>
        {
            var result = policy.ChooseInput(driver);
            moved |= result.Horizontal != 0 || result.Vertical != 0 || result.Talk;
            return result;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 10000 && timer.Elapsed.TotalSeconds < 30 && !moved && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Original destination readiness: callbacks={controller.Steps}, tick={session.Machine.Tick}, moved={moved}, active={policy.State.Active?.EventId}, destination={policy.State.DestinationField}, status={policy.State.Status}, error={controller.Error}");
        if (!moved || policy.State.Active is null || policy.State.DestinationField != saved.Ff7!.AutoPlay!.DestinationField || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Ambient work still blocked normal destination movement or the original source changed.");
        }
    }
}
