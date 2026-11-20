using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDirectTransitionAliasTests
{
    internal static void Run()
    {
        string fingerprint = "native-field-transition:1:557:" + new string('A', 64);
        AutoPlayEvent target = new("unexecuted", AutoPlayEventKind.Interaction, ConditionSignature: "target") { EffectFingerprint = fingerprint };
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> sibling = new()
        {
            Event = new("executed", AutoPlayEventKind.Interaction, ConditionSignature: "current") { EffectFingerprint = fingerprint },
            Available = true,
            Completed = true,
            Conditions = new() { ConsumedEntries = new(StringComparer.Ordinal) { "current" } },
            Routes = [new("ff7:557:resource", "current", false, [], "executed")]
        };
        Ff7AutoPlayState state = new();
        state.Coverage.Visits.Add(new() { Visit = new("visit", "region"), Events = [new() { Event = target, Available = true }, sibling] });
        bool Suppressed() => Ff7AutoPlayPolicy.HasConsumedEffectAlias(state, "visit", target);
        string before = JsonSerializer.Serialize(state);
        Check(Suppressed() && before == JsonSerializer.Serialize(state), "A current consumed owned transition did not provide pure derived alias availability.");
        AutoPlayEventRoute correct = sibling.Routes[0];
        foreach (AutoPlayEventRoute wrong in new[]
        {
            correct with { DestinationVisitKey = "ff7:558:resource" },
            correct with { ConditionSignature = "old" },
            correct with { TargetId = "other" },
            correct with { ObservedStoryChanged = true },
            correct with { Selections = [new("choice", 1)] }
        })
        {
            sibling.Routes = [wrong];
            Check(!Suppressed(), "A differing route receipt provided direct-transition alias authority.");
        }
        sibling.Routes.Clear();
        sibling.ObservedConditionSignature = "current";
        Check(!Suppressed(), "Completion alone replaced the required actual owned route.");
        sibling.Routes.Add(correct);
        sibling.Conditions!.ConsumedEntries.Clear();
        sibling.Conditions.ConsumedEntries.Add("old");
        Check(!Suppressed(), "An old entry condition authorized the current transition alias.");
        sibling.Conditions.ConsumedEntries.Add("current");
        sibling.Completed = false;
        Check(!Suppressed(), "A recorded route without completed native execution authorized an alias.");
        sibling.Completed = true;
        sibling.Event = sibling.Event with { EffectFingerprint = "native-field-transition:1:557:" + new string('B', 64) };
        Check(!Suppressed(), "A differing full transition/guard proof became an alias.");
        sibling.Event = sibling.Event with { EffectFingerprint = null };
        Check(!Suppressed(), "Legacy absent effect metadata authorized a new transition alias.");
        Ff7AutoPlayActiveEvent planned = new("visit", "a", "a", 1, 8, 4, Ff7AutoPlayTargetKind.LineTouch, null, 641, "before");
        Ff7AutoPlayTarget expected = new("a", 8, 4, Ff7AutoPlayTargetKind.LineTouch, "a", false, Vector3.Zero) { EffectFingerprint = fingerprint };
        Ff7AutoPlayTarget actual = expected with { Id = "b", Entity = 9, ConditionFingerprint = "b" };
        Check(Ff7AutoPlayPolicy.AdoptEffectAlias(planned, expected, actual, new(9, 4, 1, true, true), 1, 1) is null, "Direct aliases accidentally widened live cross-entity owner adoption.");
        Console.WriteLine("PASS direct-transition derived availability requires current native entry and matching owned route; old adoption remains strict");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DIRECT_TRANSITION_SAVE") ?? throw new InvalidOperationException("Select an external equivalent-exit fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        Ff7AutoPlayActiveEvent original = save.AutoPlay!.Active!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        string nativeBefore = JsonSerializer.Serialize(session.CaptureAutoPlayNavigation());
        Stopwatch elapsed = Stopwatch.StartNew();
        FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(original.Entity, original.Script, session.Machine.Input, directTransition: true);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(target => target.Id == original.EventId);
        var siblings = observation.Events.Where(candidate => candidate.Id != target.Id && candidate.EffectFingerprint is not null && candidate.EffectFingerprint == target.EffectFingerprint).ToArray();
        Console.WriteLine($"direct proof={proof.Complete}; transition={proof.DirectTransition}; reason={proof.Reason}; sibling-count={siblings.Length}");
        Check(proof.Complete && proof.DirectTransition is not null && target.EffectFingerprint is not null && siblings.Length > 0, "The actual native guard did not authenticate equivalent direct exits.");
        Check(nativeBefore == JsonSerializer.Serialize(session.CaptureAutoPlayNavigation()), "Direct transition proof/discovery changed native state, RNG or hosts.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Equivalent-exit selection reached an unexpected choice."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        bool originalAdmitted = false;
        session.NativeInteractionScriptRequested += admission => originalAdmitted |= session.FieldGeneration == save.FieldGeneration && admission.Entity == original.Entity && admission.Script == original.Script && admission.Accepted && admission.Started;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        bool changedTarget = false;
        for (int slice = 0; slice < 32 && controller.Running && elapsed.Elapsed < TimeSpan.FromSeconds(10); slice++)
        {
            controller.Advance(1);
            changedTarget = policy.State.Active is { } selected && selected.EventId != original.EventId || session.FieldGeneration != save.FieldGeneration;
            if (changedTarget)
            {
                break;
            }
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> originalCoverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
        Check(controller.Error is null && changedTarget, $"The normal planner retained its duplicate exit: {controller.Error}; {policy.Status}.");
        Check(!originalAdmitted && !originalCoverage.Completed && originalCoverage.Routes.Count == 0 && originalCoverage.Conditions?.ConsumedEntries.Count == 0, "Equivalent exit planning fabricated original-owner execution or completion.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Equivalent exit continuation changed its authoritative source.");
        Console.WriteLine($"PASS original equivalent direct native exit replan without original-owner credit; callbacks={controller.Steps}; active={policy.State.Active?.EventId}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
