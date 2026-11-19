using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGatewayFrontierTests
{
    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_FRONTIER_SAVE") ?? throw new InvalidOperationException("External native frontier required.");
        byte[] bytes = File.ReadAllBytes(path);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using (MemoryStream stream = new(bytes))
        using (Ff7Session source = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!))
        {
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(source).Observe();
            Ff7AutoPlayState state = source.AutoPlayState!;
            Ff7AutoPlayTarget target = observation.Events.First(target => target.Kind == Ff7AutoPlayTargetKind.Gateway && state.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.NavigationKey == observation.NavigationKey && guard.MovementFailure is not null));
            string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(source);
            string metadata = JsonSerializer.Serialize(state);
            Ff7AutoplayDriver driver = new(source);
            Ff7AutoPlayNativeRoute? release = driver.ProveNativeRoute(target, observation.Events, sourceRelease: (candidate, actual) => Ff7AutoPlayPolicy.CanReleaseNativeSource(state, observation, target, candidate, actual));
            Check(release is { SourceReleased: true, BattleBoundary: false, GatewayDeparture: null }, "The exact native recovery did not release its bounded source refusal.");
            Check(native == Ff7AutoplayDriver.ReadNativeRouteFingerprint(source) && metadata == JsonSerializer.Serialize(state), "Detached release proof mutated its native source/history.");
            Console.WriteLine($"SOURCE-RELEASE steps:{release!.Steps.Length}, target:{target.Id}, boundary:{driver.LastNativeRouteProbe}");
        }
        Run(installation, bytes, false);
        Run(installation, bytes, true);
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Original source bytes changed.");
    }

    private static void Run(Ff7Installation installation, byte[] bytes, bool cold)
    {
        using MemoryStream stream = new(bytes);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoPlayPolicy policy = CreatePolicy(session);
        bool sourceReleased = false;
        bool ordinaryAdvances = false;
        bool restored = false;
        Ff7AutoPlayActiveEvent? admitted = null;
        Ff7AutoPlayNativeRoute? admittedProof = null;
        Ff7SessionSave? penultimate = null;
        Ff7SessionSave? terminal = null;
        AutoPlayTravelGoal? goal = null;
        long start = session.Host.Process.Tick;
        int quanta = 0;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        Ff7AutoPlayController CreateController()
        {
            return new(session, driver =>
            {
                Ff7AutoplayInput input = policy.ChooseInput(driver);
                if (sourceReleased && !driver.PlanningPending && policy.State.NativeRoute is null)
                {
                    ordinaryAdvances |= driver.PlannedActionAdvances == true;
                }
                return input;
            }, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        }
        Ff7AutoPlayController controller = CreateController();
        try
        {
            controller.Start();
            while (elapsed.Elapsed < TimeSpan.FromMinutes(1) && quanta++ < 5000 && controller.Running && !ordinaryAdvances && session.Host.Process.Tick - start < 128)
            {
                controller.Advance(1);
                if (policy.State.NativeRoute is { SourceReleased: true } proof)
                {
                    admitted ??= policy.State.Active;
                    admittedProof ??= proof;
                    if (proof.Cursor == proof.Steps.Length - 1)
                    {
                        penultimate = session.Capture();
                    }
                    goal ??= policy.State.NavigationGoal;
                    Check(admitted is not null && Ff7AutoPlayPolicy.IsNativeContinuationGoalPending(policy.State, policy.State.NavigationGoal, admitted.NavigationKey!, admitted.EventId), "Recovery manufactured a consumed self-goal.");
                    var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == admitted!.VisitId).Events.Single(item => item.Event.Id == admitted!.EventId);
                    Check(admitted!.EntryAvailabilityFingerprint == coverage.AvailabilityFingerprint && !coverage.Unavailable, "Native source admission lost its stable entry ledger capability.");
                }
                if (admitted is not null && policy.State.NativeRoute is null)
                {
                    sourceReleased = true;
                    if (terminal is null)
                    {
                        terminal = session.Capture();
                        Ff7AutoPlayState awaiting = terminal.AutoPlay!.Clone();
                        awaiting.NativeRoute = admittedProof! with { Cursor = admittedProof!.Steps.Length };
                        terminal = terminal with { AutoPlay = awaiting };
                    }
                    Check(policy.State.Active is { Accepted: false, Returned: false, CompletionRecorded: false } active && active.EventId == admitted.EventId && active.ConditionFingerprint == admitted.ConditionFingerprint && active.NavigationKey == admitted.NavigationKey && policy.State.NavigationGoal == goal && active.EntryAvailabilityFingerprint == admitted.EntryAvailabilityFingerprint, "Source release completed an event or lost its original owner/goal/entry ledger.");
                }
                if (cold && !restored && policy.State.NativeRoute is { SourceReleased: true, Cursor: >= 6 })
                {
                    Ff7SessionSave saved = session.Capture();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, saved);
                    policy = CreatePolicy(session);
                    controller = CreateController();
                    controller.Start();
                    restored = true;
                }
            }
            Console.WriteLine($"RELEASE-CONTROLLER cold:{cold}, restored:{restored}, native:{session.Host.Process.Tick-start}, quanta:{quanta}, released:{sourceReleased}, ordinaryAdvancing:{ordinaryAdvances}, active:{policy.State.Active?.EventId}, goal:{policy.State.NavigationGoal}, error:{controller.Error}");
            Check(controller.Error is null && sourceReleased && ordinaryAdvances && (!cold || restored), "Normal/cold source release did not resume the genuine ordinary approach.");
            if (!cold)
            {
                CheckColdBoundary(installation, terminal!, penultimate!);
            }
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static void CheckColdBoundary(Ff7Installation installation, Ff7SessionSave terminal, Ff7SessionSave penultimate)
    {
        foreach (Ff7SessionSave source in new[] { penultimate, terminal })
        {
            using Ff7Session restored = Ff7Session.Restore(installation, source);
            using Ff7AutoPlayPolicy policy = CreatePolicy(restored);
            string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(restored);
            string owner = JsonSerializer.Serialize(policy.State.Active);
            AutoPlayTravelGoal? goal = policy.State.NavigationGoal;
            long tick = restored.Host.Process.Tick;
            Ff7AutoplayDriver driver = new(restored);
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            bool pendingLastCallback = source.AutoPlay!.NativeRoute!.Cursor < source.AutoPlay.NativeRoute.Steps.Length;
            if (pendingLastCallback)
            {
                driver.Apply(input);
                restored.AdvanceTick();
            }
            Check(policy.State.NativeRoute is null && JsonSerializer.Serialize(policy.State.Active) == owner && policy.State.NavigationGoal == goal, "Cold terminal release lost its exact original owner/goal or fabricated completion.");
            Check(restored.Host.Process.Tick == tick + (pendingLastCallback ? 1 : 0), "Cold terminal readiness advanced an extra neutral native callback.");
            Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(restored) == (pendingLastCallback ? source.AutoPlay.NativeRoute.After : native), "Cold terminal validation changed the authenticated native endpoint.");
        }
        Ff7AutoPlayActiveEvent active = terminal.AutoPlay!.Active!;
        foreach (Ff7AutoPlayActiveEvent wrong in new[] { active with { EventId = "other" }, active with { ConditionFingerprint = "other" }, active with { Generation = active.Generation + 1 }, active with { Entity = active.Entity + 1 }, active with { Script = active.Script + 1 }, active with { Kind = Ff7AutoPlayTargetKind.ActorTalk }, active with { NavigationKey = "other" }, active with { VisitId = "other" }, active with { DestinationField = 1 } })
        {
            Ff7AutoPlayState state = terminal.AutoPlay.Clone();
            state.Active = wrong;
            Reject(terminal with { AutoPlay = state });
        }
        Ff7AutoPlayState refusal = terminal.AutoPlay.Clone();
        using (Ff7Session actual = Ff7Session.Restore(installation, terminal))
        {
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(actual).Observe();
            Ff7AutoPlayTarget target = observation.Events.Single(target => target.Id == active.EventId);
            Ff7Actor player = actual.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            Ff7AutoPlayMovementFailure region = new(player.X4096, player.X4096, player.Y4096, player.Y4096, player.Z4096, player.Z4096);
            Ff7AutoplayDriver driver = new(actual);
            driver.ConfigureSpatialBoundaries(observation.Events);
            string fact = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint(driver.ReadPhysicalAvailabilityFingerprint(target), region, actual.Host.Process.Actors);
            refusal.NavigationGuards["new-refusal"] = new(active.VisitId, target.Entity, EventId: active.EventId, NavigationKey: active.NavigationKey, MovementFailure: region, MovementSourceInside: true);
            refusal.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId).UnavailableAttempts.Add(new(active.ConditionFingerprint, active.VisitId, "new-refusal", fact, active.EventId));
        }
        Reject(terminal with { AutoPlay = refusal });
        void Reject(Ff7SessionSave source)
        {
            using Ff7Session restored = Ff7Session.Restore(installation, source);
            using Ff7AutoPlayPolicy policy = CreatePolicy(restored);
            string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(restored);
            bool rejected = false;
            try
            {
                policy.ChooseInput(new(restored));
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected && policy.State.NativeRoute is null && native == Ff7AutoplayDriver.ReadNativeRouteFingerprint(restored), "A changed source owner/refusal was admitted at the same native terminal hash.");
            restored.Capture().Validate();
        }
        Console.WriteLine("PASS exact terminal/penultimate cold readiness without neutral tick, full owner tuple and new applicable refusal rejection");
    }

    private static Ff7AutoPlayPolicy CreatePolicy(Ff7Session session) => new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
