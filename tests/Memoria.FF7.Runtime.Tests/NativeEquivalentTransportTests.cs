using System.Diagnostics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEquivalentTransportTests
{
    internal static void Run()
    {
        string fingerprint = "native-field-transition:1:557:" + new string('A', 64);
        Ff7AutoPlayTarget original = new("original", 7, 4, Ff7AutoPlayTargetKind.LineTouch, "first", false, default) { EffectFingerprint = fingerprint };
        Ff7AutoPlayTarget peer = original with { Id = "peer", Entity = 9, ConditionFingerprint = "second" };
        AutoPlayEventRoute firstRoute = new("ff7:557:destination", "first", false, [], "original");
        AutoPlayEventRoute peerRoute = firstRoute with { ConditionSignature = "second", TargetId = "peer" };
        AutoPlayTravelGoal goal = new("remote", "frontier", "current");
        Ff7AutoPlayState state = new() { NavigationGoal = goal };
        state.Active = new("visit", "original", "first", 1, 7, 4, Ff7AutoPlayTargetKind.LineTouch, 557, 1, "source", NavigationKey: "nav", TravelRoute: firstRoute);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [original, peer], true, true) { NavigationKey = "nav" };
        AutoPlayEvent Event(Ff7AutoPlayTarget target) => new(target.Id, AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint) { EffectFingerprint = target.EffectFingerprint };
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> Coverage(Ff7AutoPlayTarget target, AutoPlayEventRoute route) => new() { Event = Event(target), Available = true, Completed = true, Conditions = new() { ConsumedEntries = new(StringComparer.Ordinal) { target.ConditionFingerprint } }, Routes = [route] };
        state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [Coverage(original, firstRoute), Coverage(peer, peerRoute)] });
        AutoPlayPlanStep plan = new("visit", Event(peer), true, peerRoute, Goal: goal);
        bool Certified(Ff7AutoPlayTarget candidate, AutoPlayPlanStep selected) => Ff7AutoPlayPolicy.IsCertifiedEquivalentTransport(state, state.Active, goal, observation, candidate, selected);
        string before = JsonSerializer.Serialize(state);
        Check(Certified(peer, plan) && before == JsonSerializer.Serialize(state), "A certified peer transport was not a pure same-goal alternative.");
        Check(!Ff7AutoPlayPolicy.TryAdoptOrdinaryAlternative(state, state.Active, goal, observation, peer, plan, plan, new(true, new HashSet<string> { peer.Id }, "blocked"), new Dictionary<string, string>(), 1, "source") && before == JsonSerializer.Serialize(state), "A certified peer bypassed actual physical availability.");
        Check(!Certified(peer with { ConditionFingerprint = "changed" }, plan), "A changed peer condition inherited its old route.");
        Check(!Certified(peer with { EffectFingerprint = fingerprint.Replace(":557:", ":558:") }, plan), "A different literal departure became an equivalent transport.");
        Check(!Certified(peer, plan with { Goal = new("other") }), "A different remote goal passed transport substitution.");
        state.NavigationGoal = new("other");
        Check(!Certified(peer, plan), "A replaced current goal was ignored.");
        state.NavigationGoal = goal;
        var peerCoverage = state.Coverage.Visits[0].Events[1];
        peerCoverage.Routes.Clear();
        Check(!Certified(peer, plan), "A peer without its own actual route was admitted.");
        peerCoverage.Routes.Add(peerRoute);
        peerCoverage.Conditions!.ConsumedEntries.Clear();
        Check(!Certified(peer, plan), "An unconsumed peer condition inherited completed history.");
        Console.WriteLine("PASS certified equivalent transport current receipts, exact goal, pure inspection and stale-condition rejection");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EQUIVALENT_TRANSPORT_SAVE") ?? throw new InvalidOperationException("Select an external transport fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoPlayActiveEvent old = policy.State.Active!;
        AutoPlayTravelGoal goal = policy.State.NavigationGoal!;
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> OldCoverage() => policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == old.VisitId).Events.Single(item => item.Event.Id == old.EventId);
        string History() => JsonSerializer.Serialize(new { OldCoverage().Completed, OldCoverage().Routes, OldCoverage().Conditions!.ConsumedEntries, OldCoverage().Conditions!.ConsumedExits });
        string history = History();
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        int admitted = -1;
        FieldHostRequest? departureRequest = null;
        bool typedDeparture = false;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (session.FieldGeneration == old.Generation && admission.Entity is 9 or 10 && admission.Script == 4 && admission.Accepted && admission.Started)
            {
                admitted = admission.Entity;
                Check(policy.State.NavigationGoal == goal, "Native peer admission replaced the original remote goal.");
            }
        };
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldTransitionCommand && session.FieldGeneration == old.Generation)
            {
                Check(reply.Completed && request.Entity == admitted && request.Priority == 1 && request.Command == new FieldTransitionCommand(557, -37, 2371, 50, 0), "The actual peer issued a different or unowned native transition.");
                departureRequest = request;
            }
        };
        session.NativeFieldDeparting += _ =>
        {
            Check(departureRequest is not null && session.Host.Process.GlobalRequestId == departureRequest.Id && session.Host.Process.Transition == departureRequest.Command && policy.State.Active is { Script: 4 } owner && owner.Entity == admitted && owner.EventId == $"line:{admitted - 3}:entity:{admitted}:script:4" && policy.State.NavigationGoal == goal, "Source departure lost its actual peer request, owner or remote goal before Load.");
            typedDeparture = true;
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int slice = 0; slice < 512 && controller.Running && session.FieldGeneration == old.Generation && elapsed.Elapsed < TimeSpan.FromSeconds(15); slice++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && admitted >= 0 && typedDeparture && session.MapId == 557 && session.FieldGeneration != old.Generation, $"Equivalent peer did not execute its native departure: {controller.Error}; {policy.Status}; admitted={admitted}; callbacks={controller.Steps}.");
        Check(history == History(), "Peer travel fabricated original transport completion/history.");
        Check(policy.State.NavigationGoal == goal, "Equivalent transport replaced the genuine remote goal.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The original transport source was changed.");
        Console.WriteLine($"PASS original certified peer entity{admitted} native557 departure; callbacks={controller.Steps}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; old history/remote goal/source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
