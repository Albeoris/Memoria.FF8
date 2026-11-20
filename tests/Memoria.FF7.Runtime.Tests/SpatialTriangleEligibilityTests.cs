using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class SpatialTriangleEligibilityTests
{
    internal static void Run()
    {
        Memoria.FF7.Formats.PcWalkmeshTriangle Face(int index, short adjacent) => new(index, default, default, default, adjacent, -1, -1);
        Memoria.FF7.Formats.PcWalkmeshTriangle[] mesh = [Face(0, 1), Face(1, 2), Face(2, -1), Face(3, -1)];
        byte[] blocked = [2];
        var reachable = Ff7AutoplayDriver.ReachableTriangles(mesh, blocked, 0);
        Check(!Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(1, true, reachable) && !Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(2, true, reachable) && !Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(3, true, reachable), "Blocked or disconnected triangle-only sources stayed offered for ordinary approach.");
        var actualBlocked = Ff7AutoplayDriver.ReachableTriangles(mesh, blocked, 1);
        Check(Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(1, true, actualBlocked) && Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(2, true, actualBlocked) && !Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(0, true, actualBlocked), "Actual blocked-side placement or directed outgoing native edges were lost.");
        Check(Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(3, false, reachable) && Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(3, true, null), "Unknown/non-triangle-only physical contracts became negative proofs.");
        blocked[0] = 0;
        reachable = Ff7AutoplayDriver.ReachableTriangles(mesh, blocked, 0);
        Check(Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(2, true, reachable), "Changed native masks did not immediately reoffer reachable spatial sources.");
        Check(!Ff7AutoPlayDiscovery.CanOfferSpatialTriangle(3, true, reachable), "An unblocked disconnected component was mistaken for a native route.");
        Console.WriteLine("PASS optimistic directed spatial graph eligibility, actual blocked source, changed masks and unknown preservation");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_TRIANGLE_SAVE") ?? throw new InvalidOperationException("Select an original spatial-triangle fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        string oldId = save.AutoPlay?.Active?.EventId ?? throw new InvalidDataException("The original has no selected spatial source.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoPlayDiscovery discovery = new(session);
        Ff7AutoPlayObservation observed = discovery.Observe();
        Check(!observed.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial) && observed.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.ActorContact), "Original graph-unreachable spatial sources were not withheld while reachable interaction types remained offered.");
        int SpatialConsumed() => policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id.StartsWith("spatial:", StringComparison.Ordinal)).Sum(item => item.Conditions?.ConsumedEntries.Count ?? 0);
        int before = SpatialConsumed();
        bool admitted = false;
        bool returned = false;
        int? admittedEntity = null;
        FieldHostRequest? departure = null;
        session.NativeFieldDeparting += _ =>
        {
            if (session.FieldGeneration == save.FieldGeneration)
            {
                departure = session.Host.Capture().Receipts.SingleOrDefault(receipt => receipt.Request.Id == session.Host.Process.GlobalRequestId)?.Request;
            }
        };
        session.NativeInteractionScriptRequested += admission =>
        {
            if (session.FieldGeneration == save.FieldGeneration && admission.Accepted && admission.Started && admission.Script == 2)
            {
                admitted = true;
                admittedEntity = admission.Entity;
            }
        };
        session.Machine.ScriptReturned += receipt => returned |= session.FieldGeneration == save.FieldGeneration && admittedEntity == receipt.Entity;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        for (int poll = 0; poll < 512 && controller.Running && !returned && session.FieldGeneration == save.FieldGeneration && elapsed.Elapsed < TimeSpan.FromSeconds(15); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        bool departed = session.FieldGeneration != save.FieldGeneration && departure?.Command is FieldTransitionCommand transition && transition.Map == session.MapId;
        Check(controller.Error is null && (admitted && returned || departed) && SpatialConsumed() == before && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), $"Original reachable continuation failed: {controller.Error}; admitted={admittedEntity}, returned={returned}, departed={departed}, triangle={player.Triangle}, steps={controller.Steps}, active={policy.State.Active?.EventId}");
        Console.WriteLine($"PASS original reachable native continuation: callbacks={controller.Steps}, tick={session.Machine.Tick}, actualTriangle={player.Triangle}, admittedEntity={admittedEntity}, returned={returned}, departed={departed}, departureOwner={departure?.Entity}/p{departure?.Priority}/pc{departure?.ProgramCounter:X4}, spatial credit unchanged, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
