using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePostIvyFrontierTests
{
    private sealed class SourceBoundaryReachedException() : Exception("Source gateway boundary reached for bounded verification.");

    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_POST_IVY_SAVE") ?? throw new InvalidOperationException("External native frontier required.");
        byte[] bytes = File.ReadAllBytes(path);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using (MemoryStream stream = new(bytes))
        using (Ff7Session source = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!))
        {
            int index = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_PROBE_INDEX") ?? throw new InvalidOperationException("One native gateway probe index is required."));
            var observation = new Ff7AutoPlayDiscovery(source).Observe();
            var target = observation.Events.Single(target => target.GatewayIndex == index);
            string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(source);
            string state = System.Text.Json.JsonSerializer.Serialize(source.AutoPlayState);
            Ff7AutoplayDriver driver = new(source);
            Ff7AutoPlayNativeRoute? proof = driver.ProveNativeRoute(target, observation.Events);
            Check(before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(source) && state == System.Text.Json.JsonSerializer.Serialize(source.AutoPlayState), "One gateway query changed the native source/ledger/task.");
            Check(Ff7AutoPlayPolicy.ReadPendingSourceRevalidations(source.AutoPlayState!, observation).Any(debt => debt.EventId == target.Id), "A real remote gateway movement refusal remained outside the supported domain.");
            Console.WriteLine($"ONE-GATEWAY index:{index}, proof:{proof?.Steps.Length}, boundary:{driver.LastNativeRouteProbe}; unchanged source; honest remote source debt present");
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
        Ff7AutoPlayController controller = CreateController(session, policy);
        Ff7SessionSave? pending = null;
        Ff7NativeGatewayDeparture? accepted = null;
        bool restored = false;
        long start = session.Host.Process.Tick;
        int quanta = 0;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        void ObserveDeparture(Ff7NativeGatewayDeparture? receipt)
        {
            Check(receipt is not null && session.HasNativeGatewayDeparture(receipt), "The selected native boundary has no accepted exact gateway receipt.");
            accepted = receipt;
            pending = session.Capture();
            Check(pending.FieldGeneration == receipt!.Generation && pending.FieldName == receipt.Field && pending.Process.Transition == receipt.Transition, "Source observation ran after destination load.");
            throw new SourceBoundaryReachedException();
        }
        session.NativeFieldDeparting += ObserveDeparture;
        try
        {
            controller.Start();
            while (elapsed.Elapsed < TimeSpan.FromMinutes(2) && quanta++ < 20000 && controller.Running && session.Battle is null && pending is null && session.Host.Process.Tick - start < 2048)
            {
                controller.Advance(1);
                if (quanta % 128 == 0)
                {
                    Console.WriteLine($"GATEWAY-PROGRESS cold:{cold}, elapsed:{elapsed.Elapsed.TotalSeconds:F1}, native:{session.Host.Process.Tick-start}, quanta:{quanta}, phase:{policy.NativePlanningPhase}, active:{policy.State.Active?.EventId}, goal:{policy.State.NavigationGoal}, task:{policy.State.NativeRevalidation?.EventId}");
                }
                if (cold && !restored && session.Host.Process.Tick - start >= 10 && pending is null && session.Battle is null)
                {
                    var save = session.Capture();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, save);
                    policy = CreatePolicy(session);
                    session.NativeFieldDeparting += ObserveDeparture;
                    controller = CreateController(session, policy);
                    controller.Start();
                    restored = true;
                }
            }
            Console.WriteLine($"GATEWAY-BOUNDARY cold:{cold}, restored:{restored}, native:{session.Host.Process.Tick-start}, quanta:{quanta}, accepted:{accepted}, battle:{session.Battle is not null}, error:{controller.Error}");
            Check(session.Battle is not null || pending is not null, "No genuine native source departure/battle boundary reached.");
            Check(!cold || restored, "Cold actual continuation was not exercised.");
            if (pending is not null)
            {
                Check(controller.Error?.Contains(nameof(SourceBoundaryReachedException), StringComparison.Ordinal) == true, "The source boundary was not the controlled test interruption.");
                CheckPendingBoundary(installation, pending);
            }
            else
            {
                Check(controller.Error is null, "The ordinary controller failed before its real battle boundary.");
            }
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static void CheckPendingBoundary(Ff7Installation installation, Ff7SessionSave source)
    {
        using Ff7Session paused = Ff7Session.Restore(installation, source);
        var machine = paused.Machine;
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(paused);
        int published = 0;
        paused.NativeTickCompleted += _ => published++;
        paused.AdvanceNavigationTick();
        Check(ReferenceEquals(machine, paused.Machine) && paused.FieldGeneration == source.FieldGeneration && paused.Host.Process.Transition == source.Process.Transition && published == 0 && before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(paused), "Detached source suspension loaded a VM, replayed source state or published a post-load callback.");
        using Ff7Session legacy = Ff7Session.Restore(installation, source with { GatewayDeparture = null });
        var oldMachine = legacy.Machine;
        legacy.AdvanceTick(false);
        Check(legacy.MapId == source.Process.Transition!.Map && legacy.FieldGeneration == source.FieldGeneration + 1 && !ReferenceEquals(oldMachine, legacy.Machine) && legacy.GatewayDeparture is null, "An old pending transition without optional metadata did not load normally.");
        Console.WriteLine("PASS exact source suspension/no-loader/no-replay and ordinary legacy pending loader");
    }

    private static Ff7AutoPlayPolicy CreatePolicy(Ff7Session session) => new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
    private static Ff7AutoPlayController CreateController(Ff7Session session, Ff7AutoPlayPolicy policy) => new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
