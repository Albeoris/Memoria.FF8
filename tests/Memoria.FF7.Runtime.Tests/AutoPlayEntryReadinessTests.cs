using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayEntryReadinessTests
{
    public static void Run()
    {
        Ff7AutoPlayEntryReadiness readiness = new();
        Check(!readiness.Observe(1, 0, "initial", false), "An incomplete native initial cycle admitted its field catalog.");
        Check(!readiness.Observe(1, 1, "unblocked-entry", true), "The first transient native entry witness admitted its field catalog.");
        Check(!readiness.Observe(1, 2, "blocked-eight", true), "A changed native entry configuration inherited the previous transient witness.");
        Check(readiness.Observe(1, 3, "blocked-eight", true), "A native entry configuration stable across two completed ticks was not admitted.");
        Check(readiness.Observe(1, 3, "later-event", false) && readiness.Observe(1, 4, "changed-after-admission", true), "A completed entry admission was reset by ordinary later field activity.");
        Check(!readiness.Observe(2, 0, "blocked-eight", false) && !readiness.Observe(2, 1, "blocked-eight", true), "A new field generation inherited a previous entry admission.");
        Check(readiness.Observe(2, 2, "blocked-eight", true), "A new field generation failed to establish its own stable entry witness.");
        CheckEntryWitnessScope();
        CheckSameTickAndColdObservation();
        CheckBusyReset();
        CheckObservationBudget();
        CheckOriginalEntryReadiness();
    }

    private static void CheckEntryWitnessScope()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 1, Model = 2, PlayerControlled = true };
        Ff7Actor background = new() { Slot = 1, Entity = 3, Model = 4, TalkEnabled = true, CollisionEnabled = true };
        string witness = Ff7AutoPlayDiscovery.ReadEntryWitness("program", "component", [player, background]);
        string ambientChanged = Ff7AutoPlayDiscovery.ReadEntryWitness("program", "component", [player, background with { TalkEnabled = false, CollisionEnabled = false, X4096 = 4096, TalkRadius = 200 }]);
        Check(witness == ambientChanged, "Ambient actor geometry or interaction flags reset native entry readiness.");
        Ff7AutoPlayEntryReadiness readiness = new();
        Check(!readiness.Observe(1, 1, witness, true) && readiness.Observe(1, 2, ambientChanged, true), "A free player with stable topology failed to settle while ambient field activity continued.");
        Check(witness != Ff7AutoPlayDiscovery.ReadEntryWitness("other-program", "component", [player, background]), "A changed native program retained entry ownership.");
        Check(witness != Ff7AutoPlayDiscovery.ReadEntryWitness("program", "other-component", [player, background]), "A changed navigation component retained entry readiness.");
        foreach (Ff7Actor replacement in new[] { player with { Slot = 2 }, player with { Entity = 2 }, player with { Model = 3 }, player with { EntityBound = false }, player with { PlayerControlled = false } })
        {
            Check(witness != Ff7AutoPlayDiscovery.ReadEntryWitness("program", "component", [replacement, background]), "A changed native player binding retained entry ownership.");
        }
    }

    private static void CheckOriginalEntryReadiness()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ENTRY_READINESS_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7AutoPlayActiveEvent incoming = source.AutoPlay?.Active ?? throw new InvalidOperationException("The fixture requires its native incoming gateway owner.");
        string originalGoal = source.AutoPlay?.NavigationGoal?.EventId ?? throw new InvalidOperationException("The fixture requires its transient entry frontier goal.");
        string sourceNavigation = incoming.NavigationKey ?? throw new InvalidOperationException("The fixture requires its incoming source navigation identity.");
        Ff7Installation installation = Ff7Installation.Discover(game);
        Ff7AutoPlayObservation[] observations = new Ff7AutoPlayObservation[4];
        using (Ff7Session inspect = Ff7Session.Restore(installation, source))
        {
            Ff7AutoPlayDiscovery discovery = new(inspect);
            for (int tick = 0; tick < observations.Length; tick++)
            {
                string unchanged = JsonSerializer.Serialize(inspect.Capture());
                observations[tick] = discovery.Observe();
                Ff7AutoPlayObservation repeated = discovery.Observe();
                Check(repeated.CanAcceptInput == observations[tick].CanAcceptInput && repeated.NavigationKey == observations[tick].NavigationKey, "Repeated discovery within one original native tick admitted a transient entry catalog.");
                Check(JsonSerializer.Serialize(inspect.Capture()) == unchanged, "Original entry discovery changed native PCs, timers, variables, resources or RNG.");
                if (tick + 1 < observations.Length)
                {
                    inspect.AdvanceTick(advanceClock: false);
                }
            }
        }
        Check(!observations[0].CanAcceptInput && !observations[1].CanAcceptInput && !observations[2].CanAcceptInput && observations[3].CanAcceptInput, "Original entry discovery admitted the initial/transient catalog or failed to admit its stable third native callback.");
        Check(observations[1].NavigationKey != observations[2].NavigationKey && observations[2].NavigationKey == observations[3].NavigationKey, "The original entry fixture did not reproduce its native first-to-second callback topology change and subsequent stability.");
        string settledNavigation = observations[3].NavigationKey;
        AutoPlayNavigationRoute[] transientEdges = source.AutoPlay!.Coverage.NavigationRoutes.Where(route => route.SourceKey == sourceNavigation && route.EventId == incoming.EventId && route.Route.ConditionSignature == incoming.ConditionFingerprint && route.DestinationKey != settledNavigation).ToArray();
        Check(incoming.Kind == Ff7AutoPlayTargetKind.Gateway && incoming.Generation != source.FieldGeneration && transientEdges.Length != 0, "The original fixture did not retain its incoming gateway's transient destination component receipt.");
        using Ff7Session session = Ff7Session.Restore(installation, source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded original entry check must stop before new choice exploration."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 128 && controller.Running && session.FieldGeneration == source.FieldGeneration; step++)
        {
            controller.Advance(1);
            if (session.FieldGeneration == source.FieldGeneration && session.Host.Process.Tick < 3)
            {
                Check(policy.State.Active?.EventId != originalGoal, "The original transient entry frontier was selected before its native topology settled.");
            }
        }
        Check(controller.Failure is null, $"The bounded original entry continuation failed: {controller.Error}");
        Check(session.FieldGeneration != source.FieldGeneration, "The original entry continuation failed to reach its native exit within the bounded ordinary steps.");
        Check(policy.State.Coverage.NavigationRoutes.Any(route => route.SourceKey == sourceNavigation && route.EventId == incoming.EventId && route.DestinationKey == settledNavigation), "The incoming native gateway did not record the settled destination navigation component.");
        foreach (AutoPlayNavigationRoute transient in transientEdges)
        {
            string recorded = JsonSerializer.Serialize(transient);
            Check(!policy.State.Coverage.NavigationRoutes.Any(route => JsonSerializer.Serialize(route) == recorded) && policy.State.Coverage.NavigationRetirements.Any(retired => JsonSerializer.Serialize(retired.Route) == recorded && retired.ActualDestinationKey == settledNavigation), "A transient incoming destination edge remained active instead of retaining its exact correction receipt.");
        }
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "Original entry readiness queued a replay or changed its standalone source bytes.");
        Console.WriteLine($"PASS original stable entry admission, corrected incoming gateway topology and native exit after {controller.Steps} ordinary steps; source unchanged.");
    }

    private static void CheckSameTickAndColdObservation()
    {
        Ff7AutoPlayEntryReadiness readiness = new();
        Check(!readiness.Observe(1, 1, "entry", true), "The first current tick admitted native entry readiness.");
        for (int observation = 0; observation < Ff7AutoPlayNativeReadiness.ObservationLimit; observation++)
        {
            Check(!readiness.Observe(1, 1, "entry", true), "Repeated observation of the same native tick admitted entry readiness.");
        }
        Check(!readiness.Observe(1, 1, "changed-in-tick", true), "A changed witness within one native tick admitted entry readiness.");
        Check(readiness.Observe(1, 2, "changed-in-tick", true), "Repeated same-tick reads consumed the distinct native tick budget or lost the latest witness.");
        Ff7AutoPlayEntryReadiness cold = new();
        Check(!cold.Observe(1, 2, "changed-in-tick", true) && !cold.Observe(1, 2, "changed-in-tick", true), "A fresh helper invented a readiness proof from the saved current tick.");
        Check(cold.Observe(1, 3, "changed-in-tick", true), "A fresh helper could not establish entry readiness through ordinary subsequent observation.");
    }

    private static void CheckBusyReset()
    {
        Ff7AutoPlayEntryReadiness readiness = new();
        Check(!readiness.Observe(1, 1, "entry", true), "The first eligible witness admitted field entry.");
        Check(!readiness.Observe(1, 2, "entry", false), "Busy native setup admitted field entry.");
        Check(!readiness.Observe(1, 3, "entry", true), "A native busy boundary preserved an earlier pending readiness witness.");
        Check(!readiness.Observe(1, 4, "changed-entry", true), "A changed native witness failed to reset the pending entry proof.");
        Check(readiness.Observe(1, 5, "changed-entry", true), "A stable post-busy witness did not admit native field entry.");
        Ff7AutoPlayEntryReadiness busy = new();
        for (int tick = 0; tick < Ff7AutoPlayNativeReadiness.ObservationLimit * 2; tick++)
        {
            Check(!busy.Observe(1, tick, "busy", false), "Busy ticks were mistaken for eligible stable entry observations.");
        }
        Check(!busy.Observe(1, 10000, "ready", true) && busy.Observe(1, 10001, "ready", true), "Busy native ticks exhausted the eligible unstable-entry budget.");
    }

    private static void CheckObservationBudget()
    {
        Ff7AutoPlayEntryReadiness readiness = new();
        for (int tick = 0; tick < Ff7AutoPlayNativeReadiness.ObservationLimit - 1; tick++)
        {
            Check(!readiness.Observe(1, tick, $"changing:{tick}", true), "An unstable native entry witness was admitted before the observation bound.");
        }
        bool bounded = false;
        try
        {
            readiness.Observe(1, Ff7AutoPlayNativeReadiness.ObservationLimit - 1, "still-changing", true);
        }
        catch (NotSupportedException)
        {
            bounded = true;
        }
        Check(bounded, "Eligible changing native entry observations continued past the existing readiness budget.");
        Check(!readiness.Observe(2, 0, "new-generation", true) && readiness.Observe(2, 1, "new-generation", true), "An exhausted native entry observation budget leaked into another field generation.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
