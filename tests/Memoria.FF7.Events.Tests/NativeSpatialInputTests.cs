using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeSpatialInputTests
{
    private static readonly FieldSpatialSample[] Samples = [new(10, new(0, 0, 0, 12)), new(20, new(0, 0, 0, 26))];

    public static void Run()
    {
        GuardAndNativePartyRequest();
        DirectActorQueries();
        HostAndUnrelatedQueriesStop();
        QueryWithoutTransitionStops();
        BoundedWaitAndLoop();
        ByteAliasesRetainSourceDependencies();
        TrianglePredicateTaint();
        DirectSpatialTransitions();
        StochasticSpatialTransitions();
        ConditionalDecodeWidths();
        Console.WriteLine("PASS native read-only autonomous spatial demands");
    }

    private static void StochasticSpatialTransitions()
    {
        byte[] table = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        FieldSpatialRandomDomain domain = new(table, 1, 0);
        Check(domain.Fingerprint == new FieldSpatialRandomDomain(table, 1, 127).Fingerprint, "A full-cycle domain changed with the current native random phase.");
        FieldSpatialSample[] positions = [new(1, new(-3, 5, 0, 0))];
        for (byte operation = 0; operation <= 10; operation++)
        {
            byte right = operation >= 9 ? (byte)4 : (byte)36;
            (_, FieldMachine machine, RejectHost host) = Create(Main([0x99, 5, 30], [0x14, 0x50, 30, right, operation, 12]), [0]);
            string before = JsonSerializer.Serialize(machine.Capture());
            FieldSpatialDemand[] demands = machine.ReadCurrentSpatialDemands(1, positions, randomDomain: domain).Demands;
            int[] destinations = demands.SelectMany(demand => demand.DestinationFields).Order().ToArray();
            Check(destinations.SequenceEqual(operation == 8 ? new[] { 99 } : new[] { 99, 100 }), $"Native stochastic comparison {operation} lost a feasible branch or fabricated one.");
            Check(demands.All(demand => demand.Stochastic) && host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Stochastic projection changed source state or sampled the native host.");
        }
        (_, FieldMachine restricted, _) = Create(Main([0x99, 5, 30], [0x14, 0x50, 30, 36, 0, 12]), [0]);
        Check(restricted.ReadCurrentSpatialDemands(1, positions, randomDomain: new(table, 16, 0)).Demands.Single().DestinationFields.Single() == 100, "An unreachable random-table phase fabricated a feasible outcome.");
        (_, FieldMachine correlated, _) = Create(Main([0x99, 5, 30, 0x99, 5, 31], [0x14, 0x55, 30, 31, 0, 12]), [0]);
        Check(correlated.ReadCurrentSpatialDemands(1, positions, randomDomain: domain).Demands.Single().DestinationFields.Single() == 100, "Consecutive native table draws were incorrectly treated as independent.");

        static byte[] Main(byte[] random, byte[] condition)
        {
            byte[] body = [.. random, .. condition, .. Transition(99), .. Transition(100)];
            return [0xB8, 0x66, 1, 17, 19, 0x16, 0x60, 19, 0, 5, 0, 0, checked((byte)(body.Length + 1)), .. body, 0];
        }
    }

    private static void ConditionalDecodeWidths()
    {
        for (byte opcode = 0x14; opcode <= 0x19; opcode++)
        {
            bool word = opcode >= 0x16;
            bool wide = (opcode & 1) != 0;
            List<byte> condition = [opcode, 0];
            condition.AddRange(word ? new byte[] { 255, 255, 0, 0 } : new byte[] { 255, 0 });
            condition.Add(2);
            condition.Add(wide ? (byte)13 : (byte)12);
            if (wide)
            {
                condition.Add(0);
            }
            FieldProgram program = new([0, .. condition, .. Transition(99), .. Transition(100)], [new ushort[32]]);
            RecordingHost host = new();
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.AdvanceTick();
            Check(host.Destination == (opcode is 0x16 or 0x17 ? 100 : 99), $"Conditional {opcode:X2} changed native operand signedness or branch displacement.");
        }
    }

    private sealed class RecordingHost : IFieldHost
    {
        public int? Destination { get; private set; }
        public FieldHostReply Execute(FieldHostRequest request)
        {
            Destination ??= ((FieldTransitionCommand)request.Command).Map;
            return new(true);
        }
    }

    private static void DirectSpatialTransitions()
    {
        byte[] query = [0xB8, 0x66, 1, 17, 19];
        (_, FieldMachine machine, RejectHost host) = Create([.. query, 0x16, 0x60, 19, 0, 5, 0, 0, 16, 0x33, 1, 0x4A, 1, .. Transition(99), 0], [0]);
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldSpatialSample[] positions = [new(1, new(-3, 5, 0, 0)), new(2, new(5, -3, 0, 0))];
        FieldSpatialDemand demand = machine.ReadCurrentSpatialDemands(1, positions).Demands.Single();
        Check(demand is { SampleId: 1, TargetScript: -1, TriangleOnly: false } && demand.DestinationFields.SequenceEqual([99]), "A direct coordinate-guarded native MAP lost its spatial demand.");
        Check(host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Direct MAP inspection dispatched a host or changed the source.");
        (_, FieldMachine unguarded, _) = Create([.. query, .. Transition(99), 0], [0]);
        Check(unguarded.ReadCurrentSpatialDemands(1, positions).Demands.Length == 0, "An unused position query fabricated a spatial predicate for an unconditional MAP.");
        (_, FieldMachine housekeeping, RejectHost randomHost) = Create([0x99, 5, 30, .. query, 0x16, 0x60, 19, 0, 5, 0, 0, 12, .. Transition(99), 0], [0]);
        string original = JsonSerializer.Serialize(housekeeping.Capture());
        Check(housekeeping.ReadCurrentSpatialDemands(1, positions).Demands.Single().SampleId == 1, "Unrelated native random bookkeeping hid a proved coordinate transition.");
        Check(randomHost.Calls == 0 && original == JsonSerializer.Serialize(housekeeping.Capture()), "Spatial inspection sampled or modified authoritative random bookkeeping.");
        (_, FieldMachine stochastic, _) = Create([.. query, 0x99, 5, 19, 0x16, 0x60, 19, 0, 5, 0, 0, 12, .. Transition(99), 0], [0]);
        bool rejected = false;
        try
        {
            stochastic.ReadCurrentSpatialDemands(1, positions);
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Check(rejected, "A random-dependent spatial predicate was silently omitted or fabricated as a deterministic demand.");
    }

    private static void DirectActorQueries()
    {
        foreach (byte[] query in new byte[][] { [0xB9, 6, 1, 7], [0xC1, 0x66, 0x66, 1, 17, 19, 21, 7] })
        {
            (_, FieldMachine machine, _) = Create([.. query, 0x16, 0x60, 7, 0, 24, 0, 4, 4, 0x01, 1, 0x44, 0], Transition(99));
            Check(machine.ReadCurrentSpatialDemands(1, Samples).Demands.Single() is { SampleId: 20, TriangleOnly: true }, "A direct actor triangle or position query lost its native selector or sample.");
        }
        (_, FieldMachine coordinates, _) = Create([0xB8, 0x66, 1, 17, 19, 0x16, 0x60, 19, 0, 5, 0, 0, 4, 0x01, 1, 0x44, 0], Transition(99));
        FieldSpatialSample[] positions = [new(1, new(-3, 5, 0, 0)), new(2, new(5, -3, 0, 0))];
        Check(coordinates.ReadCurrentSpatialDemands(1, positions).Demands.Single() is { SampleId: 1, TriangleOnly: false }, "Packed signed XY query results changed native coordinate comparison semantics.");
    }

    private static void GuardAndNativePartyRequest()
    {
        byte[] main = [0x14, 0x50, 6, 0, 0, 3, 0x12, 6, 0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0x16, 0x60, 7, 0, 24, 0, 4, 4, 0x06, 0, 0x44, 0];
        (FieldProgram program, FieldMachine machine, RejectHost host) = Create(main, Transition(99));
        FieldSpatialObservation blocked = machine.ReadCurrentSpatialDemands(1, Samples);
        Check(blocked.Demands.Length == 0 && blocked.Reads.Contains(new(FieldBank.MapByte, 6, false)), "A false source guard invented a spatial transition or lost its refresh dependency.");
        machine.Variables.WriteByte(FieldBank.MapByte, 6, 1);
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldSpatialObservation observed = machine.ReadCurrentSpatialDemands(1, Samples);
        FieldSpatialDemand demand = observed.Demands.Single();
        Check(demand is { Entity: 0, ProgramCounter: 25, TargetEntity: 1, TargetScript: 4, SampleId: 20 } && demand.DestinationFields.SequenceEqual([99]) && demand.TriangleOnly, "Native spatial predicates or party child resolution selected the wrong request or sample.");
        Check(host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Spatial projection mutated the authoritative VM, inputs, variables, host or RNG.");
        FieldMachine cold = FieldMachine.Restore(program, machine.Capture(), host);
        Check(JsonSerializer.Serialize(observed) == JsonSerializer.Serialize(cold.ReadCurrentSpatialDemands(1, Samples)), "Cold restoration changed spatial demands or their source fingerprint.");
        string fingerprint = demand.Fingerprint;
        machine.Variables.WriteWord(FieldBank.MapWord, 7, 55);
        FieldSpatialDemand moved = machine.ReadCurrentSpatialDemands(1, Samples).Demands.Single();
        Check(moved.Fingerprint == fingerprint && !moved.Reads.Any(read => read.Bank == FieldBank.MapByte && read.Operand is 7 or 8), "Substituted triangle output became a nonprogress dependency.");
        machine.Variables.WriteByte(FieldBank.MapByte, 6, 0);
        Check(machine.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "Spatial discovery ignored a changed source control guard.");
    }

    private static void HostAndUnrelatedQueriesStop()
    {
        foreach (byte[] prefix in new byte[][] { [0x99, 0x50, 0], [0x40, 0, 0], [0x70, 0, 1, 0], [0x49, 0, 0, 0], [0xB9, 6, 2, 7] })
        {
            byte[] main = [.. prefix, 0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0x06, 0, 0x44, 0];
            (_, FieldMachine machine, RejectHost host) = Create(main, Transition(99));
            string before = JsonSerializer.Serialize(machine.Capture());
            Check(machine.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0 && host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Spatial discovery fabricated an unknown host, RNG, battle, menu or other actor query result.");
        }
    }

    private static void QueryWithoutTransitionStops()
    {
        foreach (byte[] main in new byte[][] { [0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0], [0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0x06, 0, 0x44, 0] })
        {
            (_, FieldMachine machine, _) = Create(main, [0]);
            Check(machine.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A query without a proved child transition invented a navigation frontier.");
        }
        (_, FieldMachine active, _) = Create([0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0x06, 0, 0x44, 0], Transition(99));
        FieldVmSnapshot snapshot = active.Capture();
        snapshot.Entities[0] = snapshot.Entities[0] with { Priority = 1 };
        FieldMachine requested = FieldMachine.Restore(CreateProgram([0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0x06, 0, 0x44, 0], Transition(99)), snapshot);
        Check(requested.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A requested script became an autonomous spatial owner.");
    }

    private static void BoundedWaitAndLoop()
    {
        (_, FieldMachine loop, _) = Create([0x12, 0], Transition(99));
        Check(loop.ReadCurrentSpatialDemands(1, Samples, 4).Demands.Length == 0, "A bounded pure loop invented a spatial demand.");
        (_, FieldMachine waiting, _) = Create([0x24, 200, 0, 0x75, 0x66, 0x66, 0, 17, 19, 21, 7, 0x06, 0, 0x44, 0], Transition(99));
        string before = JsonSerializer.Serialize(waiting.Capture());
        Check(waiting.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 2 && before == JsonSerializer.Serialize(waiting.Capture()), "A bounded wait projection changed logical time or failed to reach its spatial query.");
        Check(waiting.ReadCurrentSpatialDemands(1, Samples, 1).Demands.Length == 0, "Spatial projection exceeded its instruction budget.");
    }

    private static void ByteAliasesRetainSourceDependencies()
    {
        byte[] main = [0x80, 0x50, 7, 1, 0x16, 0x60, 7, 0, 1, 0, 0, 4, 0x75, 0x66, 0x66, 0, 17, 19, 21, 9, 0x06, 0, 0x44, 0];
        (_, FieldMachine machine, _) = Create(main, Transition(99));
        FieldSpatialObservation result = machine.ReadCurrentSpatialDemands(1, Samples);
        Check(result.Demands.Length == 2 && result.Reads.Contains(new(FieldBank.MapByte, 8, false)) && !result.Reads.Contains(new(FieldBank.MapByte, 7, false)), "A byte own-store hid an unmodified source byte in an aliased WORD predicate.");
    }

    private static void TrianglePredicateTaint()
    {
        byte[] query = [0x75, 0x66, 0x66, 0, 17, 19, 21, 7];
        FieldSpatialSample[] positions = [new(1, new(30, 31, 32, 26))];
        foreach (byte coordinate in new byte[] { 17, 19, 21 })
        {
            (_, FieldMachine machine, _) = Create([.. query, 0x16, 0x60, coordinate, 0, 24, 0, 4, 4, 0x06, 0, 0x44, 0], Transition(99));
            Check(!machine.ReadCurrentSpatialDemands(1, positions).Demands.Single().TriangleOnly, "A coordinate predicate was classified as triangle-only.");
        }
        (_, FieldMachine copied, _) = Create([.. query, 0x81, 0x26, 30, 7, 0, 0x86, 0x20, 30, 1, 0, 0x16, 0x20, 30, 0, 24, 0, 4, 4, 0x06, 0, 0x44, 0], Transition(99));
        Check(copied.ReadCurrentSpatialDemands(1, Samples).Demands.Single().TriangleOnly, "A copied triangle predicate lost its provenance through a canonical bank alias or arithmetic.");
        (_, FieldMachine replacedCoordinate, _) = Create([.. query, 0x81, 0x66, 17, 7, 0, 0x16, 0x60, 17, 0, 24, 0, 4, 4, 0x06, 0, 0x44, 0], Transition(99));
        Check(replacedCoordinate.ReadCurrentSpatialDemands(1, Samples).Demands.Single().TriangleOnly, "An overwritten coordinate output retained stale coordinate taint.");
        (_, FieldMachine literal, _) = Create([.. query, 0x81, 0x60, 7, 26, 0, 0x16, 0x60, 7, 0, 24, 0, 4, 4, 0x06, 0, 0x44, 0], Transition(99));
        Check(literal.ReadCurrentSpatialDemands(1, Samples).Demands.All(demand => !demand.TriangleOnly), "A literal overwrite retained stale triangle taint.");
        (_, FieldMachine partial, _) = Create([.. query, 0x80, 0x50, 8, 0, 0x16, 0x60, 7, 0, 24, 0, 4, 4, 0x06, 0, 0x44, 0], Transition(99));
        Check(!partial.ReadCurrentSpatialDemands(1, Samples).Demands.Single().TriangleOnly, "A partially overwritten WORD alias was treated as a proved scalar predicate.");
        (_, FieldMachine byteAlias, _) = Create([.. query, 0x14, 0x50, 7, 24, 4, 4, 0x06, 0, 0x44, 0], Transition(99));
        Check(!byteAlias.ReadCurrentSpatialDemands(1, Samples).Demands.Single().TriangleOnly, "A partial WORD read was treated as a proved scalar predicate.");
        (_, FieldMachine unused, _) = Create([.. query, 0x06, 0, 0x44, 0], Transition(99));
        Check(unused.ReadCurrentSpatialDemands(1, Samples).Demands.All(demand => !demand.TriangleOnly), "Unused triangle output invented a triangle predicate.");
    }

    private static (FieldProgram Program, FieldMachine Machine, RejectHost Host) Create(byte[] main, byte[] child)
    {
        FieldProgram program = CreateProgram(main, child);
        RejectHost host = new();
        FieldMachine initialized = new(program, host: host);
        initialized.Initialize();
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 255);
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 11, 255);
        FieldVmSnapshot snapshot = initialized.Capture();
        snapshot.Entities[1] = snapshot.Entities[1] with { ProgramCounter = 1 + main.Length, ModelBound = true, PartyCharacter = 0 };
        snapshot.Entities[2] = snapshot.Entities[2] with { ProgramCounter = 1 + main.Length, ModelBound = true, PartyCharacter = 1 };
        return (program, FieldMachine.Restore(program, snapshot, host), host);
    }

    private static FieldProgram CreateProgram(byte[] main, byte[] child)
    {
        ushort[] childEntries = new ushort[32];
        childEntries[4] = checked((ushort)(main.Length + 2));
        return new([0, .. main, 0, .. child], [new ushort[32], childEntries, new ushort[32]]);
    }

    private static byte[] Transition(byte field)
    {
        return [0x60, field, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    }

    private sealed class RejectHost : IFieldHost
    {
        public int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Spatial inspection must not dispatch the authoritative host.");
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
