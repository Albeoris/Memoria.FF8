using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeOwnedRouteTests
{
    public static void Run()
    {
        foreach (int mode in Enumerable.Range(0, 4))
        {
            Check(Ff7AutoplayDriver.BackwardLadderKey(mode) == new ushort[] { 0x1000, 0x4000, 0x8000, 0x2000 }[mode], "Backward input lost its native axis/reversal.");
        }
        Check(Ff7AutoplayDriver.IsNativeClockAccess(new(FieldBank.Persistent0Word, 15, true)), "The lower word alias escaped the clock dependency.");
        Check(Ff7AutoplayDriver.IsNativeClockAccess(new(FieldBank.Persistent0Word, 23, true)), "The upper word alias escaped the clock dependency.");
        Check(!Ff7AutoplayDriver.IsNativeClockAccess(new(FieldBank.Persistent0Byte, 15, false)) && !Ff7AutoplayDriver.IsNativeClockAccess(new(FieldBank.Persistent0Word, 24, true)) && !Ff7AutoplayDriver.IsNativeClockAccess(new(FieldBank.Persistent1Word, 16, true)), "Clock dependency spilled into a neighboring byte/bank.");
        Ff7AutoPlayState compatible = JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!;
        compatible.Validate();
        Check(compatible.NativeRoute is null, "Older observational state acquired an invented native route.");
        Ff7AutoPlayNativeRoute route = new("target", "condition", 1, [new(new('A', 64), default)], new('B', 64));
        new Ff7AutoPlayState { NativeRoute = route with { Cursor = 1 } }.Validate();
        foreach (Ff7AutoPlayNativeRoute invalid in new[] { route with { Cursor = -1 }, route with { Cursor = 2 }, route with { Steps = [] }, route with { Steps = new Ff7AutoPlayRouteStep[257] }, route with { After = "bad" } })
        {
            bool rejected = false;
            try
            {
                new Ff7AutoPlayState { NativeRoute = invalid }.Validate();
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected, "Malformed saved native navigation was accepted.");
        }
        Console.WriteLine("PASS native ladder direction, clock aliases, absent-state compatibility and malformed continuation rejection");
    }

    public static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_OWNED_ROUTE_SAVE") ?? throw new InvalidOperationException("An external native navigation fixture is required.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        string nativeBefore = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoplayDriver driver = new(session);
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => session.AutoPlayState!.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.NavigationKey == observation.NavigationKey && guard.MovementFailure is not null)))
        {
            Ff7AutoPlayNativeRoute? route = driver.ProveNativeRoute(target, observation.Events);
            Console.WriteLine($"PROBE {target.Id}: {driver.LastNativeRouteProbe}; steps {route?.Steps.Length}");
            if (route is not null)
            {
                if (nativeBefore != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
                {
                    throw new InvalidOperationException("Detached native planning changed the source.");
                }
                RunBoundary(root, source, false);
                RunBoundary(root, source, true);
                RunBoundary(root, source, false, failFinal: true);
                if (!source.SequenceEqual(File.ReadAllBytes(path)))
                {
                    throw new InvalidOperationException("The external source was changed.");
                }
                return;
            }
        }
        throw new InvalidOperationException("No positive complete owned native route was proved.");
    }

    private static void RunBoundary(string root, byte[] source, bool cold, bool failFinal = false)
    {
        Ff7Installation installation = Ff7Installation.Discover(root);
        using MemoryStream stream = new(source);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Navigation requested a choice checkpoint."));
        void Advance()
        {
            if (failFinal && policy.State.NativeRoute is { } route && route.Cursor == route.Steps.Length)
            {
                throw new InvalidOperationException("Injected failure before the final native callback.");
            }
            session.AdvanceTick();
        }
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: Advance, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        string guards = JsonSerializer.Serialize(policy.State.NavigationGuards.Select(entry => new { entry.Key, entry.Value.VisitId, entry.Value.EventId, entry.Value.NavigationKey, entry.Value.MovementFailure }));
        bool restored = false;
        bool owned = false;
        int callbacks = 0;
        try
        {
            controller.Start();
            while (callbacks++ < 128 && controller.Running && session.Battle is null)
            {
                controller.Advance(1);
                owned |= policy.State.NativeRoute is not null;
                if (policy.State.NativeRoute is not null)
                {
                    Ff7AutoPlayActiveEvent active = policy.State.Active ?? throw new InvalidOperationException("A native continuation has no active event.");
                    var admitted = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId);
                    Check(!admitted.Unavailable && active.EntryAvailabilityFingerprint == admitted.AvailabilityFingerprint, "Resumed native admission lost its positive stable availability.");
                }
                if (cold && !restored && policy.State.NativeRoute is { Cursor: >= 6 })
                {
                    Ff7SessionSave save = session.Capture();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, save);
                    policy = new(session, (_, _) => throw new InvalidOperationException("Navigation requested a choice checkpoint."));
                    controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
                    controller.Start();
                    restored = true;
                }
            }
            Console.WriteLine($"CONTROLLER cold:{cold}, restored:{restored}, callbacks:{callbacks - 1}, owned:{owned}, battle:{session.Battle is not null}, triangle:{session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}, error:{controller.Error}");
            if (failFinal)
            {
                Check(controller.Error?.Contains("Injected failure", StringComparison.Ordinal) == true, "The final-callback failure was not injected.");
                using Ff7Session recovery = Ff7Session.Restore(installation, session.Capture());
                recovery.AutoPlayState!.Validate();
                using Ff7AutoPlayPolicy recoveryPolicy = new(recovery, (_, _) => throw new InvalidOperationException());
                try
                {
                    recoveryPolicy.ChooseInput(new(recovery));
                }
                catch (NotSupportedException)
                {
                }
                Check(recoveryPolicy.State.NativeRoute is null, "A consumed divergent continuation survived recovery.");
                recovery.AutoPlayState!.Validate();
                Console.WriteLine("PASS final-callback failure retained a restorable native checkpoint");
                return;
            }
            if (!owned || session.Battle is null || controller.Error is not null || policy.State.NativeRoute is not null || cold && !restored)
            {
                throw new InvalidOperationException("The normal controller did not reach the authenticated native battle boundary.");
            }
            string after = JsonSerializer.Serialize(policy.State.NavigationGuards.Select(entry => new { entry.Key, entry.Value.VisitId, entry.Value.EventId, entry.Value.NavigationKey, entry.Value.MovementFailure }));
            if (guards != after)
            {
                throw new InvalidOperationException("Native continuation changed retained movement guards.");
            }
            CheckFingerprintAliases(session.Capture());
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static void CheckFingerprintAliases(Ff7SessionSave state)
    {
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(state);
        byte[] variables = state.Vm.PersistentVariables.ToArray();
        byte[] bank = state.Battle!.AiSaveBank1!.ToArray();
        variables[16] ^= 1;
        bank[16] ^= 1;
        Ff7SessionSave clockAlias = state with { Vm = state.Vm with { PersistentVariables = variables }, Battle = state.Battle with { AiSaveBank1 = bank } };
        Check(before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(clockAlias), "The authenticated initial battle clock copy was not recognized.");
        bank[15] ^= 1;
        Check(before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(clockAlias), "The battle clock exclusion spilled into native byte15.");
        bank[15] ^= 1;
        bank[24] ^= 1;
        Check(before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(clockAlias), "The battle clock exclusion spilled into native byte24.");
        bank[24] ^= 1;
        bank[16] ^= 1;
        Check(before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(clockAlias), "A nonmatching AI bank clock was normalized.");
        bank[16] ^= 1;
        Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(state with { Battle = state.Battle with { Tick = 1 } }) != Ff7AutoplayDriver.ReadNativeRouteFingerprint(clockAlias with { Battle = clockAlias.Battle! with { Tick = 1 } }), "Running battle AI inputs were normalized.");
        Check(before != Ff7AutoplayDriver.ReadNativeRouteFingerprint(state with { Progress = state.Progress with { Gil = state.Progress.Gil + 1 } }), "Native economy escaped authentication.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
