using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class WorldDestinationGoalTests
{
    internal static void Run()
    {
        const string world = "ff7-world:0:authored";
        const string navigation = world + ":nav:world";
        AutoPlayEvent desired = new("desired-entry", AutoPlayEventKind.Transition, ConditionSignature: "desired-condition");
        AutoPlayEvent other = new("unrelated-entry", AutoPlayEventKind.Transition, ConditionSignature: "other-condition");
        Ff7AutoPlayState state = new() { DestinationField = 300, NavigationGoal = new("unrelated-field", "optional-exit") };
        state.WorldVisits[world] = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new(world, "authored"), [other, desired]);
        planner.ObserveNavigation(world, navigation, [other, desired]);
        Ff7AutoPlayObservation observation = new(world, "authored", 1, [], true, true) { NavigationKey = navigation };
        HashSet<string> candidates = [desired.Id];
        AutoPlayEventAvailability Read(AutoPlayEvent target) => new(false, [], target.ConditionSignature);
        HashSet<string> avoid = Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, candidates, Read);
        AutoPlayPlanStep? next = planner.PlanNavigation(navigation, preferredGoal: state.NavigationGoal, avoidFirstEvents: avoid);
        Check(next?.Event.Id == desired.Id && avoid.SetEquals([other.Id]) && state.DestinationField == 300, "An explicit world destination was replaced by an unrelated historical field goal or ordinary frontier.");
        Ff7AutoPlayState cold = state.Clone();
        Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(cold, observation, candidates, Read);
        Check(cold.NavigationGoal == state.NavigationGoal && cold.DestinationField == 300, "Cold restore lost the authenticated destination entry goal.");
        ExpectUnsupported(() => Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, candidates, _ => new(true, [desired.Id], "native-refusal")), "A physically refused destination silently fell back to unrelated exploration.");
        Check(state.NavigationGoal?.EventId == desired.Id, "An explicit physical boundary discarded its authenticated destination intent.");
        planner.Completed(world, desired.Id, entrySignature: desired.ConditionSignature, entrySignatureAuthoritative: true);
        ExpectUnsupported(() => Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, candidates, Read), "A consumed no-op was rearmed without an actual destination transfer.");
        planner.Completed(world, desired.Id, "ff7:300:native", observedStoryChanged: false, entrySignature: desired.ConditionSignature, entrySignatureAuthoritative: true);
        string conditions = JsonSerializer.Serialize(state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == desired.Id).Conditions);
        Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, candidates, Read);
        Check(state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == desired.Id).RouteNeedsRediscovery && conditions == JsonSerializer.Serialize(state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == desired.Id).Conditions), "Explicit native travel erased consumption instead of retaining its actual route.");
        AutoPlayEvent mode = new("mode:desired-entry", AutoPlayEventKind.Interaction, ConditionSignature: "rider-condition");
        planner.Observe(new(world, "authored"), [other, desired, mode]);
        planner.ObserveNavigation(world, navigation, [other, desired, mode]);
        Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, new HashSet<string> { mode.Id }, Read);
        Check(state.NavigationGoal?.EventId == mode.Id, "An admitted native mode prerequisite could not retain the requested destination.");
        planner.Completed(world, mode.Id, entrySignature: mode.ConditionSignature, entrySignatureAuthoritative: true);
        ExpectUnsupported(() => Ff7AutoPlayPolicy.ResolveWorldDestinationGoal(state, observation, new HashSet<string> { mode.Id }, Read), "A positive causal projection rearmed a consumed interaction without an actual native transfer receipt.");
        Check(!state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == mode.Id).RouteNeedsRediscovery, "An unchanged consumed interaction was marked for replay without native transfer evidence.");
    }

    internal static void RunOriginal()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_DESTINATION_SAVE") ?? throw new InvalidOperationException("Select an external world destination fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        ushort destination = policy.State.DestinationField ?? throw new InvalidOperationException("The fixture must retain its native destination intent.");
        HashSet<string> sources = session.ReadAutoPlayWorldEntranceSources(session.World!.Resources.World).Where(source => source.Destination == destination).Select(source => source.EventId).ToHashSet(StringComparer.Ordinal);
        long generation = session.FieldGeneration;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 24 && controller.Running && policy.State.NavigationGoal is null; step++)
        {
            controller.Advance(1);
        }
        string? selected = policy.State.Active?.WorldModeGoal?.TargetEventId ?? policy.State.Active?.EventId ?? policy.State.NavigationGoal?.EventId;
        bool boarding = false;
        if (policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldModel } active)
        {
            Ff7AutoPlayWorldModelScriptObservation contact = session.World!.ReadAutoPlayModelFrontiers().Single(item => item.Id == active.EventId);
            string native = JsonSerializer.Serialize(session.Capture());
            foreach (Ff7AutoPlayWorldEntranceSource source in session.ReadAutoPlayWorldEntranceSources(session.World.Resources.World).Where(source => source.Destination == destination))
            {
                Ff7AutoPlayWorldScriptObservation? target = session.World.ReadAutoPlayFrontierCatalog(source.ChunkX, source.ChunkZ).Scripts.SingleOrDefault(item => item.Script == source.Script);
                if (target is not null && session.World.ReadAutoPlayBoardingAdvice(contact, target).Connected)
                {
                    boarding = true;
                    break;
                }
            }
            Check(native == JsonSerializer.Serialize(session.Capture()), "Read-only boarding advice changed native VM, process, input cache, progress or RNG state.");
            Ff7AutoPlayWorldEntranceSource fixtureSource = session.ReadAutoPlayWorldEntranceSources(session.World.Resources.World).First(source => source.Destination == destination);
            Ff7AutoPlayWorldScriptObservation targetSource = session.World.ReadAutoPlayFrontierCatalog(fixtureSource.ChunkX, fixtureSource.ChunkZ).Scripts.Single(item => item.Script == fixtureSource.Script);
            Ff7AutoPlayWorldModelScriptObservation unrelated = contact with { Analysis = contact.Analysis with { Transitions = [] } };
            Check(!session.World.ReadAutoPlayBoardingAdvice(unrelated, targetSource).Connected && native == JsonSerializer.Serialize(session.Capture()), "An unrelated model interaction acquired a boarding route or changed native state.");
        }
        Check(controller.Error is null && selected is not null && (sources.Contains(selected) || boarding), $"The native world planner did not select its requested source or proved boarding prerequisite: {controller.Error ?? policy.Status}");
        Check(session.World is not null && session.FieldGeneration == generation && policy.State.DestinationField == destination, "Destination planning intentionally reentered an unrelated field.");
        Check(policy.State.Clone().NavigationGoal == policy.State.NavigationGoal && bytes.SequenceEqual(File.ReadAllBytes(path)), "The native world goal failed cold serialization or changed its standalone fixture.");
        Console.WriteLine($"PASS original world destination source/prerequisite {selected}; retained destination {destination}; {policy.Status}; source unchanged");
    }

    private static void ExpectUnsupported(Action action, string message)
    {
        try
        {
            action();
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
