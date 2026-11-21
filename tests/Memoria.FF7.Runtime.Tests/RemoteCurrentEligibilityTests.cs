using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class RemoteCurrentEligibilityTests
{
    internal static void Run()
    {
        Check(Ff7Session.IsStablePersistentNoEffectProof(new(true, true, null, [new(FieldBank.Persistent0Word, 0, true)])), "A current persistent story guard was rejected.");
        foreach (var read in new FieldVariableRead[] { new(FieldBank.MapByte, 4, false), new(FieldBank.MapWord, 4, true), new(FieldBank.Persistent0Byte, 16, false), new(FieldBank.Persistent0Word, 15, true), new(FieldBank.Persistent3Byte, 6, false), new(FieldBank.Persistent3Word, 5, true) })
        {
            Check(!Ff7Session.IsStablePersistentNoEffectProof(new(true, true, null, [read])), "A foreign map, live clock or walking counter became a persistent eligibility fact.");
        }
        Check(!Ff7Session.IsStablePersistentNoEffectProof(new(false, true, null, [])) && !Ff7Session.IsStablePersistentNoEffectProof(new(true, false, null, [])), "An unknown or effectful entry was suppressed.");
        Console.WriteLine("Persistent remote eligibility rejects foreign map, clock, counter and unknown scopes.");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REMOTE_ELIGIBILITY_SAVE") ?? throw new InvalidOperationException("Select a retained-goal fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        var goal = policy.State.NavigationGoal!;
        string visit = policy.State.Coverage.NavigationVisits.Single(item => item.Key == goal.NavigationKey).VisitKey;
        var coverage = policy.State.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == goal.EventId);
        int entries = coverage.Conditions!.ConsumedEntries.Count;
        int routes = coverage.Routes.Count;
        string native = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        var proof = session.ReadCurrentPersistentEntryProof(visit, goal.EventId!);
        Check(proof.Complete && proof.HasNoEffect && Ff7Session.IsStablePersistentNoEffectProof(proof), "The actual current persistent guard did not prove a pure return: " + proof.Reason);
        Check(ReferenceEquals(proof, session.ReadCurrentPersistentEntryProof(visit, goal.EventId!)), "An unchanged persistent witness did not retain its proof.");
        string changedIdentity = visit[..(visit.LastIndexOf(':') + 1)] + new string('0', 64);
        Check(!session.ReadCurrentPersistentEntryProof(changedIdentity, goal.EventId!).Complete, "Another native program borrowed the retained proof.");
        Check(native == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }), "Remote inspection changed native VM, process, progress or input.");
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        controller.Advance(1);
        Check(controller.Error is null && policy.State.NavigationGoal != goal && coverage.Conditions.ConsumedEntries.Count == entries && coverage.Routes.Count == routes && !coverage.Conditions.HasExecutedEntry(goal.Conditions!), "A stale goal remained selected or its history was rewritten.");
        Check(SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The original retained-goal source changed.");
        Console.WriteLine($"Original retained-goal proof: complete={proof.Complete}, noEffect={proof.HasNoEffect}, reads={string.Join(';', proof.Reads)}, oldGoalReleased=True, nextNavigation={policy.State.NavigationGoal?.NavigationKey}, nextEvent={policy.State.NavigationGoal?.EventId}, status={policy.State.Status}, callbacks={controller.Steps}, source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
