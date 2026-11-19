using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGatewayRouteTests
{
    internal static void Run()
    {
        var field = NativeDialogueTests.Field();
        Ff7FieldHost host = new(field, (_, _) => 1);
        PcFieldGateway gateway = new(new(0, -10, 0), new(0, 10, 0), 20, 30, 4, 100, 64, 0);
        Ff7Actor sourceActor = new() { Slot = 0, Entity = 0, Model = 0, X4096 = -4096, CollisionRadius = 8 };
        Ff7Actor to = sourceActor with { X4096 = 4096 };
        Ff7NativeGatewayDeparture accepted = Ff7Session.RequestGatewayDeparture(field.Name, 1, host, 2, gateway, sourceActor, to) ?? throw new InvalidOperationException("Native crossing was not accepted.");
        Check(accepted.GatewayIndex == 2 && accepted.Generation == 1 && accepted.Transition == new FieldTransitionCommand(100, 20, 30, 4, 64), "Accepted source receipt lost exact index/full entry.");
        Check(Ff7Session.RequestGatewayDeparture(field.Name, 1, host, 3, gateway with { DestinationX = 21 }, sourceActor, to) is null && host.Process.Transition == accepted.Transition, "Rejected overlapping gateway replaced the first accepted entry.");
        Ff7FieldHost script = new(field, (_, _) => 1);
        script.Execute(new(1, 0, 1, 10, accepted.Transition));
        Check(Ff7Session.RequestGatewayDeparture(field.Name, 1, script, 2, gateway, sourceActor, to) is null, "A scripted transition to the same map acquired physical gateway ownership.");
        Ff7FieldHost locked = new(field, (_, _) => 1, new Ff7FieldProcess { MapJumpDisabled = true });
        Check(Ff7Session.RequestGatewayDeparture(field.Name, 1, locked, 2, gateway, sourceActor, to) is null, "A disabled native gateway acquired an accepted receipt.");
        FieldMachine machine = new(FieldProgram.FromPcScripts(field.Scripts));
        machine.Initialize();
        Ff7SessionSave snapshot = new() { ResourceProfile = "authored-gateway", Language = "en", FieldName = field.Name, FieldGeneration = 1, Progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [] }, Vm = machine.Capture(), Process = host.Capture() };
        string legacy = JsonSerializer.Serialize(snapshot);
        Check(!legacy.Contains("GatewayDeparture", StringComparison.Ordinal) && JsonSerializer.Deserialize<Ff7SessionSave>(legacy)!.GatewayDeparture is null, "Absent gateway metadata changed old native snapshot/hash JSON.");
        string explicitNull = legacy[..^1] + ",\"GatewayDeparture\":null}";
        Check(JsonSerializer.Deserialize<Ff7SessionSave>(explicitNull)!.GatewayDeparture is null, "Older null gateway metadata was not optional.");
        Ff7SessionSave cold = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(snapshot with { GatewayDeparture = accepted }))!;
        Check(cold.GatewayDeparture == accepted && cold.Process.Transition == accepted.Transition, "Cold source checkpoint lost its accepted native entry.");
        CheckSourceBoundary(snapshot with { GatewayDeparture = accepted });
        Ff7AutoPlayTarget bridge = new("bridge", 1, 2, Ff7AutoPlayTargetKind.LinePush, "bridge-condition", false, default);
        Ff7AutoPlayTarget gatewayTarget = new("gateway", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gateway-condition", false, default, GatewayIndex: 0, DestinationField: 100);
        Ff7AutoPlayObservation observation = new("visit", "program", 1, [gatewayTarget, bridge], true, true) { NavigationKey = "component" };
        Ff7AutoPlayState scope = new() { NativeRevalidation = new("component", gatewayTarget.Id, gatewayTarget.ConditionFingerprint, new('A', 64), true) };
        scope.NavigationGuards["gateway"] = new("visit", -1, EventId: gatewayTarget.Id, NavigationKey: "component", MovementFailure: new(0, 10, 0, 10));
        scope.NavigationGuards["bridge"] = new("visit", 1, EventId: bridge.Id, NavigationKey: "component", MovementFailure: new(0, 10, 0, 10));
        Check(Ff7AutoPlayPolicy.ReadNativeProbeCandidates(scope, observation).Length == 0, "An arrived maintenance task excluded a separate certifying local proof job.");
        scope.NativeRevalidation = null;
        Check(Ff7AutoPlayPolicy.ReadNativeProbeCandidates(scope, observation, gatewayTarget.Id).Length == 0, "A completed failed gateway task exhausted the needed bridge or was immediately reprobed.");
        Check(Ff7AutoPlayPolicy.ReadNativeProbeCandidates(scope, observation).Length == 0 && scope.SourceRevalidationAttempts.Count == 0, "Full/cancelled job scope inherited a false exhausted/evaluated receipt.");
        Console.WriteLine("PASS accepted gateway index/full-entry ownership, rejected overlap/script/disabled requests, optional native hash compatibility and cold pending receipt");
    }

    private static void CheckSourceBoundary(Ff7SessionSave source)
    {
        Ff7NativeGatewayDeparture receipt = source.GatewayDeparture!;
        string fingerprint = Ff7AutoplayDriver.ReadNativeRouteFingerprint(source);
        string nativeBefore = JsonSerializer.Serialize(source);
        Ff7AutoPlayNativeRoute Proof(Ff7NativeGatewayDeparture? terminal = null)
        {
            Ff7NativeGatewayDeparture expected = terminal ?? receipt;
            return new("gateway", "condition", expected.Generation, [new(new('A', 64), default)], fingerprint, Cursor: 1, GatewayDeparture: expected);
        }
        byte[] clockCode = [0, 0x80, 0x11, 24, 16, 0];
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram clockProgram = new(clockCode, [entries]);
        FieldMachine sourceMachine = new(new FieldProgram([0, 0], [new ushort[32]]));
        sourceMachine.Initialize();
        using Ff7NativeRouteClockWitness sourceClock = new(sourceMachine);
        int loads = 0;
        bool destinationClockRead = false;
        FieldMachine? destination = null;
        void Load(FieldTransitionCommand command)
        {
            Check(command == receipt.Transition, "The loader received an altered native entry.");
            loads++;
            destination = new(clockProgram);
            destination.InstructionVariableAccessed += (access, _) => destinationClockRead |= Ff7AutoplayDriver.IsNativeClockAccess(access);
            destination.Initialize();
            destination.TryRequestScript(0, 1);
            destination.AdvanceTick();
        }
        string vmBefore = JsonSerializer.Serialize(sourceMachine.Capture());
        Ff7Session.CompleteNativeFieldDeparture(receipt.Transition, receipt, true, actual => Check(actual == receipt, "Detached source observation changed its accepted native entry."), Load);
        Check(loads == 0 && destination is null && !destinationClockRead && vmBefore == JsonSerializer.Serialize(sourceMachine.Capture()), "Detached source boundary loaded/executed a new VM or replayed source PC/RNG state.");
        foreach (bool cold in new[] { false, true })
        {
            Ff7AutoPlayState state = new() { NativeRoute = Proof() };
            if (cold)
            {
                state = state.Clone();
            }
            int published = 0;
            int initialLoads = loads;
            Ff7Session.CompleteNativeFieldDeparture(receipt.Transition, receipt, false, actual =>
            {
                Check(loads == initialLoads && !destinationClockRead, "Live proof authentication ran after the loader/new VM clock consumer.");
                Ff7AutoPlayPolicy.AuthenticateNativeGatewayDeparture(state, actual, true, fingerprint, () => published++);
                Check(state.NativeRoute is null && published == 1, "Accepted source proof was not cleared/published before ordinary load.");
            }, Load);
            Check(loads == initialLoads + 1 && destinationClockRead && sourceClock.Independent, "Normal/cold source acceptance suppressed native loading or contaminated source clock proof with a destination VM.");
            destinationClockRead = false;
        }
        foreach (Ff7NativeGatewayDeparture wrong in new[] { receipt with { GatewayIndex = receipt.GatewayIndex + 1 }, receipt with { Transition = receipt.Transition with { X = checked((short)(receipt.Transition.X + 1)) } }, receipt with { Generation = receipt.Generation + 1 }, receipt with { Field = "other-source" } })
        {
            Reject(Proof(wrong), receipt, true, fingerprint);
        }
        Reject(Proof(), null, false, fingerprint);
        Reject(Proof(), receipt, true, new('B', 64));
        Reject(Proof() with { Cursor = 0 }, receipt, true, fingerprint);
        void Reject(Ff7AutoPlayNativeRoute proof, Ff7NativeGatewayDeparture? actual, bool accepted, string hash)
        {
            Ff7AutoPlayState state = new() { NativeRoute = proof };
            int beforeLoads = loads;
            int published = 0;
            bool rejected = false;
            try
            {
                Ff7Session.CompleteNativeFieldDeparture(receipt.Transition, actual, false, observed => Ff7AutoPlayPolicy.AuthenticateNativeGatewayDeparture(state, observed, accepted, hash, () => published++), Load);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected && state.NativeRoute is null && published == 1 && loads == beforeLoads, "A wrong entry/index/source/script/hash/inflight owner was admitted or discarded its actual source pending command.");
            Ff7SessionSave recovery = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(source with { AutoPlay = state }))!;
            recovery.Validate();
            var restoredField = NativeDialogueTests.Field();
            Ff7FieldHost restoredHost = new(restoredField, (_, _) => 1, recovery.Process);
            FieldMachine restoredMachine = FieldMachine.Restore(FieldProgram.FromPcScripts(restoredField.Scripts), recovery.Vm, restoredHost);
            Check(JsonSerializer.Serialize(restoredMachine.Capture()) == JsonSerializer.Serialize(recovery.Vm), "Failure recovery replayed or changed source VM execution/RNG.");
            Check(recovery.Process.Transition == receipt.Transition && recovery.GatewayDeparture == receipt && JsonSerializer.Serialize(recovery.Vm) == JsonSerializer.Serialize(source.Vm), "Boundary failure did not retain a capturable/restorable source command and script PCs.");
        }
        int oldLoads = loads;
        Ff7Session.CompleteNativeFieldDeparture(receipt.Transition, null, false, actual => Check(actual is null, "An old pending transition invented a physical receipt."), Load);
        Check(loads == oldLoads + 1, "Old null metadata refused an existing native pending command.");
        Check(nativeBefore == JsonSerializer.Serialize(source), "Source-boundary tests mutated native source progression/RNG/PC state.");
        FieldMachine reading = new(clockProgram);
        reading.Initialize();
        using Ff7NativeRouteClockWitness clock = new(reading);
        reading.TryRequestScript(0, 1);
        reading.AdvanceTick();
        Check(!clock.Independent, "An executed source VM clock read escaped the same witness used by native proof admission.");
        FieldMachine cancelled = new(clockProgram);
        cancelled.Initialize();
        Ff7NativeRouteClockWitness cancelledWitness = new(cancelled);
        cancelledWitness.Dispose();
        cancelled.TryRequestScript(0, 1);
        cancelled.AdvanceTick();
        Check(cancelledWitness.Independent, "A cancelled native proof retained its source VM operand observer.");
        var legacy = source with { GatewayDeparture = null, Progress = source.Progress with { Clock = new() } };
        var json = JsonSerializer.SerializeToNode(legacy)!.AsObject();
        json.Remove(nameof(Ff7SessionSave.GatewayDeparture));
        string oldHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.ToJsonString())));
        Check(oldHash == Ff7AutoplayDriver.ReadNativeRouteFingerprint(legacy), "Optional absent gateway metadata changed the old LINE native SHA contract.");
        Console.WriteLine("PASS tiny-kernel source-only pause, live/cold pre-loader auth, wrong owner/entry/script rejection with pending recovery, old-null loader, source/destination clock scope and old LINE SHA");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
