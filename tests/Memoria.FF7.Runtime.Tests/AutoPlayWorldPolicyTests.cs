using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldPolicyTests
{
    public static void Run()
    {
        NativeHandoffsRequireTheirOwners();
        ControlledOwnerProjectionIsGuardedAndReadOnly();
    }

    private static void NativeHandoffsRequireTheirOwners()
    {
        WorldEntitySnapshot idle = new(-1, 0, 0, 0, false, 0, []);
        WorldSnapshot vm = new(1, "authored-world", 0, 1, false, new byte[64], [], idle, []);
        Ff7WorldProcess world = new(0, 0, 7, vm, [], 0, true, 0, 0, null, [], -1, new(), 0, 1);
        Ff7AutoPlayActiveEvent gateway = new("field", "gateway:0", "source", 4, -1, -1, Ff7AutoPlayTargetKind.Gateway, 7, 1, "before");
        Check(Ff7AutoPlayPolicy.IsAcceptedWorldArrival(gateway, world, new(7, 0, 0, 0, 0)), "An actual source-gateway world handoff must complete without an interaction-script Accepted marker or generation increment.");
        Check(!Ff7AutoPlayPolicy.IsAcceptedWorldArrival(gateway with { DestinationField = 8 }, world, new(7, 0, 0, 0, 0)), "A different native world entry selector must not complete a predicted gateway.");
        Ff7AutoPlayActiveEvent mesh = new("world", "mesh:0:0:script:3", "entry", 4, -1, 3, Ff7AutoPlayTargetKind.WorldMesh, null, 1, "before", Accepted: true);
        Ff7AutoPlayWorldDispatchReceipt pending = new("world", mesh.EventId, 100, 1, 12, [200]);
        Check(!Ff7AutoPlayPolicy.HasWorldControlReturned(pending, idle with { ProgramCounter = 120, Priority = 3, Calls = [new(80, 0, 0)] }), "An admitted callback at its original call depth has not returned control.");
        Check(Ff7AutoPlayPolicy.HasWorldControlReturned(pending, idle with { ProgramCounter = 80, Priority = 3 }), "Native call-depth return must be recognized even when the interrupted callback also had priority three.");
        Check(!Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, pending, 5, 91), "A predicted world boundary must not establish a native field route.");
        Ff7AutoPlayWorldDispatchReceipt departed = pending with { Departure = new(1, -1, 200, 0x100, WorldOperation.FieldTransition), DestinationField = 91 };
        Check(Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, departed, 5, 91), "An accepted native packet and actual destination allocation must establish the field handoff.");
        Check(!Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh with { Accepted = false }, departed, 5, 91) && !Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, departed, 4, 91) && !Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, departed, 5, 92), "Unaccepted sources, unchanged generations and different fields must not complete native world routes.");
        Ff7AutoPlayState observed = new() { Active = mesh, WorldDispatch = departed };
        observed.WorldVisits["world"] = new() { ProgramIdentity = vm.ProgramIdentity, ObservedChunks = [new(0, 0)] };
        Ff7AutoPlayState cold = observed.Clone();
        Check(cold.Active == mesh && cold.WorldDispatch?.Departure == departed.Departure && cold.WorldVisits["world"].ObservedChunks.Single() == new Ff7AutoPlayWorldChunk(0, 0), "Cold observation restoration lost native source ownership or optional lazy discovery facts.");
    }

    private static void ControlledOwnerProjectionIsGuardedAndReadOnly()
    {
        byte[] persistent = new byte[1280];
        WorldProgram program = new(new([new(0x8000, 1)], new ushort[] { 0, 0x100, 0x110, 1, 0x11B, 8, 0x318, 0x203 }));
        RejectHost host = new();
        WorldMachine machine = new(program, persistent, host);
        string before = JsonSerializer.Serialize(machine.Capture());
        WorldScriptAnalysis Analyze(int owner) => machine.AnalyzeMeshDispatch(0, 0, 3, readHost: dependency => Ff7WorldRuntime.ProjectAutoPlayHostDependency(dependency, owner));
        WorldScriptAnalysis first = Analyze(0);
        WorldScriptAnalysis changed = Analyze(1);
        Check(first.Complete && first.HostValues.Single().Value == 0 && first.Transitions.Single().Selector == 0x100, "Native special selector eight must resolve from the saved controlled owner without sampling a live host.");
        Check(changed.Complete && changed.HostValues.Single().Value == 1 && changed.Transitions.Single().Selector == 0x101, "A changed controlled owner must alter native field selector operands and its retained host guard.");
        PcWorldMesh mesh = new(0, 0, [Vector3.Zero, new(100, 0, 0), new(0, 0, 100)], [], [new(0, 1, 2, 3 << 5, Vector2.Zero, Vector2.Zero, Vector2.Zero, 0)]);
        PcWorldFieldTable fields = new([new(0, 0, 0, 91, 0), new(0, 0, 0, 92, 0)]);
        Ff7AutoPlayWorldScriptObservation Catalog(WorldScriptAnalysis analysis) => Ff7WorldRuntime.BuildAutoPlayFrontierCatalog(0, program.Identity, 0, 0, mesh, _ => analysis, machine.ReadPredicateFingerprint, fields.Select).Scripts.Single();
        Check(Catalog(first).PredicateFingerprint != Catalog(changed).PredicateFingerprint, "Resolved host values must invalidate catalog conditions even when the world VM snapshot and bank reads are unchanged.");
        Check(Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, -1, 8), -1) == 0 && Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, -1, 16), 0) is null, "The snapshot projection must retain native missing-owner normalization and leave stochastic selectors incomplete.");
        WorldVariableRead[] globals = [new(0x114, 64), new(0x11C, 10)];
        Check(machine.ReadPredicateFingerprint(globals) == WorldMachine.ReadGlobalPredicateFingerprint(program.Identity, persistent, globals), "Cross-scene global observation must reuse the native typed scalar reader and identical guard signature.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && persistent.All(value => value == 0) && host.Calls == 0, "Resolved world discovery changed native PC, storage, host or RNG state.");
    }

    private sealed class RejectHost : IWorldHost
    {
        public int Calls { get; private set; }
        public WorldHostReply Execute(WorldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("World policy observation must not dispatch a native host.");
        }
        public int ReadSpecial(int selector, int activeModel)
        {
            Calls++;
            throw new InvalidOperationException("World policy observation must not sample a native host.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
