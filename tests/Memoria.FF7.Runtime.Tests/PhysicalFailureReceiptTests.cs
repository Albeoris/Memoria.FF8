using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class PhysicalFailureReceiptTests
{
    internal static void Run()
    {
        FieldMachine machine = new(new FieldProgram([0, 0], [new ushort[32]]));
        machine.Initialize();
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 10, Y4096 = 20, Z4096 = 30, Triangle = 0, Direction = 64 };
        Ff7SessionSave native = new() { ResourceProfile = "authored", Language = "en", FieldName = "authored", FieldGeneration = 1, Progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [] }, Vm = machine.Capture(), Process = new() { Actors = [actor] } };
        const string context = "target:goal|destination:2|guide:1,2,3|run:true|speed:4096";
        Ff7AutoPlayPhysicalFailureReceipt receipt = Ff7AutoplayDriver.CreatePhysicalFailureReceipt(native, context, 19);
        string Source(Ff7SessionSave state) => Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(state, "physical-search-source:1");
        Check(Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(receipt, Source(native), context), "An identical local native search did not reuse its exhausted receipt.");
        byte[] map = native.Vm.MapVariables.ToArray();
        map[0] ^= 1;
        foreach (Ff7SessionSave changed in new[] { native with { Vm = native.Vm with { Tick = native.Vm.Tick + 1 } }, native with { Vm = native.Vm with { MapVariables = map } }, native with { Vm = native.Vm with { Input = new(Held: 1) } }, native with { Process = native.Process with { Actors = [actor with { X4096 = 11 }] } }, native with { Process = native.Process with { Actors = [actor with { Direction = 65 }] } }, native with { Progress = native.Progress with { FieldRandomIndex = 1 } } })
        {
            Check(!Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(receipt, Source(changed), context), "A different native pose/heading/tick/VM/input/RNG reused a local search receipt.");
        }
        foreach (string changed in new[] { context.Replace("goal", "other"), context.Replace("destination:2", "destination:3"), context.Replace("guide:1,2,3", "guide:2,2,3"), context.Replace("run:true", "run:false"), context.Replace("speed:4096", "speed:1024") })
        {
            Check(!Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(receipt, Source(native), changed), "A changed goal/destination/guide/mode/speed reused local exhaustion.");
        }
        Check(!Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(receipt with { Revision = 0 }, Source(native), context), "An unsupported local search algorithm revision remained authoritative.");
        string guideContext = Ff7AutoplayDriver.ReadPhysicalFailureGuideContext(context, [2, 1, 0], [(0, 1), (1, 2)]);
        Ff7AutoPlayPhysicalFailureReceipt guideReceipt = Ff7AutoplayDriver.CreatePhysicalFailureReceipt(native, guideContext, 19);
        Check(Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(guideReceipt, Source(native), Ff7AutoplayDriver.ReadPhysicalFailureGuideContext(context, [2, 1, 0], [(1, 2), (0, 1)])), "Equivalent advice order changed the exact guide contract.");
        Check(!Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(guideReceipt, Source(native), Ff7AutoplayDriver.ReadPhysicalFailureGuideContext(context, [3, 1, 0], [(0, 1), (1, 2)])) && !Ff7AutoplayDriver.MatchesPhysicalFailureReceipt(guideReceipt, Source(native), Ff7AutoplayDriver.ReadPhysicalFailureGuideContext(context, [2, 1, 0], [(0, 2)])), "Changed route distances or alternative-guide advice reused an exhausted search bundle.");
        CheckPlannerScope(receipt);
        Console.WriteLine("Physical failure receipt exact-source/contract, planner scope and old-save checks passed.");
    }

    private static void CheckPlannerScope(Ff7AutoPlayPhysicalFailureReceipt receipt)
    {
        AutoPlayEvent target = new("gateway", AutoPlayEventKind.Transition, ConditionSignature: "condition");
        Ff7AutoPlayState state = new();
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = new() { Event = target, Available = true };
        state.Coverage.Visits.Add(new() { Visit = new("visit", "program"), Events = [coverage] });
        AutoPlayNavigationVisit source = new("source", "visit", [target]);
        state.Coverage.NavigationVisits.Add(source);
        Ff7AutoPlayMovementFailure region = new(0, 10, 0, 10);
        string key = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("source", target.Id, "physical", region);
        Ff7AutoPlayNavigationGuard guard = new("visit", -1, EventId: target.Id, NavigationKey: "source", MovementFailure: region, MovementSourceInside: true);
        Check(!JsonSerializer.Serialize(guard).Contains("PhysicalFailure", StringComparison.Ordinal) && JsonSerializer.Deserialize<Ff7AutoPlayNavigationGuard>(JsonSerializer.Serialize(guard))!.PhysicalFailure is null, "Older absent receipt data changed its nonprogress default/serialization.");
        Check(JsonSerializer.Deserialize<Ff7AutoPlayNavigationGuard>("{\"VisitId\":\"visit\",\"Entity\":1,\"PhysicalFailure\":null}")!.PhysicalFailure is null, "Explicit-null old receipt data failed its nonprogress default.");
        state.NavigationGuards[key] = guard;
        coverage.UnavailableAttempts.Add(new("condition", "visit", key, "failed", target.Id));
        Dictionary<string, string> facts = new() { [key] = "failed" };
        foreach (Ff7AutoPlayPhysicalFailureReceipt? local in new Ff7AutoPlayPhysicalFailureReceipt?[] { null, receipt })
        {
            state.NavigationGuards[key] = guard with { PhysicalFailure = local };
            string history = JsonSerializer.Serialize(state);
            Check(!Ff7AutoPlayPolicy.ReadComponentAvailability(state, source, target, facts).Unavailable, "A local guide's exhaustion was extrapolated to every pose/event in its box.");
            Check(history == JsonSerializer.Serialize(state), "Applicability removed historical observations or changed progress/coverage.");
        }
        string structuralKey = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("source", target.Id, "physical", null);
        state.NavigationGuards[structuralKey] = guard with { MovementFailure = null };
        coverage.UnavailableAttempts.Add(new("condition", "visit", structuralKey, "structural", target.Id));
        facts[structuralKey] = "structural";
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(state, source, target, facts).Unavailable, "A structural no-contact failure lost its native eligibility contract.");
        state.NavigationGuards.Remove(structuralKey);
        coverage.UnavailableAttempts.RemoveAt(1);
        string cycleKey = Ff7AutoPlayPolicy.ReadMovementFailureGuardKey("source", target.Id, "movement-cycle", region);
        state.NavigationGuards[cycleKey] = guard;
        coverage.UnavailableAttempts.Add(new("condition", "visit", cycleKey, "cycle", target.Id));
        facts[cycleKey] = "cycle";
        Check(Ff7AutoPlayPolicy.ReadComponentAvailability(state, source, target, facts).Unavailable, "A different movement-cycle observation was discarded with local physical hints.");
        Ff7AutoPlayState cold = state.Clone();
        Check(cold.NavigationGuards[key].PhysicalFailure == receipt, "Cold observation cloning lost an exact local receipt.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
