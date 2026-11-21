using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePartialAnimationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PARTIAL_ANIMATION_SAVE") ?? throw new InvalidOperationException("Select a native partial-animation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var original = policy.State.Active ?? throw new InvalidOperationException("The fixture has no returned source.");
        var before = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        bool childReturned = false;
        session.NativeTickCompleted += tick => childReturned |= tick.ScriptReturns.Any(returned => returned.Entity == before.Entity && returned.Priority == 6);
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        for (int poll = 0; poll < 6 && controller.Error is null; poll++)
        {
            controller.Advance(1);
        }
        var vm = session.Machine.Capture();
        var actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
        bool consumed = coverage.Conditions?.HasExecutedEntry(original.ConditionFingerprint) == true;
        bool moved = actor.X4096 != before.X4096 || actor.Y4096 != before.Y4096 || actor.Z4096 != before.Z4096;
        Console.WriteLine($"Unlocked reaction continuation: callbacks={controller.Steps}, tick={vm.Tick}, sourceConsumed={consumed}, childReturned={childReturned}, moved={moved}, next={policy.State.Active?.EventId}, status={policy.State.Status}, error={controller.Error}");
        if (!consumed || !moved || childReturned || vm.Entities[before.Entity].Priority != 6 || vm.Entities[before.Entity].ProgramCounter != 0x0DC2 || !(vm.PendingRequests ?? []).Any(request => request.Entity == before.Entity && request.Priority == 6 && request.ProgramCounter == 0x0DC2) || policy.State.Active?.EventId == original.EventId || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The returned source did not release ordinary navigation while preserving its native pending cosmetic child and source fixture.");
        }
    }
}
