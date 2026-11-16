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
        SpatialInputPredicates();
        SpatialInputWitnesses();
        SpatialInputBounds();
        SharedPhysicalAliasesAndDirectEffects();
        AutonomousBodyExitProofs();
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

    private static void SpatialInputPredicates()
    {
        foreach (byte opcode in new byte[] { 0x30, 0x31, 0x32 })
        {
            foreach (ushort mask in new ushort[] { 3, 0x220 })
            {
                byte[] gates = [opcode, (byte)mask, (byte)(mask >> 8), 4];
                (FieldProgram program, FieldMachine machine, RejectHost host) = Create(InputMain(gates), Transition(99));
                string before = JsonSerializer.Serialize(machine.Capture());
                FieldSpatialDemand demand = machine.ReadCurrentSpatialDemands(1, Samples).Demands.Single();
                FieldSpatialInputGuard guard = demand.InputGuards.Single();
                Check(demand.SampleId == 20 && demand.TriangleOnly && guard.Matched && guard.Demand.Kind == (FieldScriptInputKind)(opcode - 0x30) && guard.Demand.Mask == mask && guard.Demand.Mapped == ((mask & 0x200) != 0) && !guard.Demand.AwaitingInput, "A native spatial input gate lost its selector, full OR mask, mapping flag, branch result or physical predicate.");
                Check(FieldMachine.TryFindSpatialInputWitness(demand.InputGuards, out FieldInputState input, sharedPhysicalButtons: true) && FieldMachine.AreSpatialInputGuardsSatisfied(demand.InputGuards, input), "A feasible native held/pressed/released guard has no coherent physical witness.");
                SpatialInputOracle oracle = new();
                FieldMachine native = FieldMachine.Restore(program, machine.Capture(), oracle);
                native.SetInput(input.PreviousHeld!.Value, 0, input.PreviousRawHeld!.Value, 0);
                native.SetInput(input.Held, input.Pressed, input.RawHeld, input.RawPressed);
                for (int tick = 0; tick < 4 && oracle.Destination is null; tick++)
                {
                    native.AdvanceTick();
                }
                Check(oracle.Destination == 99, "A projected spatial input witness disagreed with native input-branch and party-request execution.");
                Check(host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Spatial input inspection changed authoritative input, PC, persistent state or host execution.");
                Check(JsonSerializer.Serialize(demand) == JsonSerializer.Serialize(FieldMachine.Restore(program, machine.Capture(), host).ReadCurrentSpatialDemands(1, Samples).Demands.Single()), "Cold restoration changed the retained spatial input contract.");
            }
        }
        (_, FieldMachine zero, _) = Create(InputMain([0x30, 0, 0, 4]), Transition(99));
        Check(zero.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A matched zero-mask native input guard fabricated a spatial frontier.");
        (_, FieldMachine alternatives, _) = Create(InputMain([0x30, 3, 0, 5, 0x10, 3, 0, 0]), Transition(99));
        FieldSpatialDemand[] branches = alternatives.ReadCurrentSpatialDemands(1, Samples).Demands;
        Check(branches.Length == 2 && branches.Select(demand => demand.ProgramCounter).Distinct().Count() == 1 && branches.Select(demand => demand.Fingerprint).Distinct().Count() == 2 && branches.Select(demand => demand.InputGuards.Single().Matched).ToHashSet().SetEquals([true, false]), "Distinct matched/unmatched native paths to the same spatial request were merged or silently omitted.");
        (_, FieldMachine beforeQuery, _) = Create([0x30, 0x20, 2, 16, 0xB9, 6, 1, 7, 0x16, 0x60, 7, 0, 26, 0, 0, 4, 0x01, 1, 0x44, 0], Transition(99));
        Check(beforeQuery.ReadCurrentSpatialDemands(1, Samples).Demands.Single().InputGuards.Single().Matched, "An input predicate preceding the physical query hid a feasible spatial frontier.");
    }

    private static byte[] InputMain(byte[] gates)
    {
        return [0xB9, 6, 1, 7, 0x16, 0x60, 7, 0, 26, 0, 0, checked((byte)(gates.Length + 4)), .. gates, 0x01, 1, 0x44, 0];
    }

    private static void SpatialInputWitnesses()
    {
        FieldSpatialInputGuard Guard(FieldScriptInputKind kind, ushort mask, bool matched = true, bool mapped = true) => new(new($"guard:{kind}:{mask}:{mapped}", "authored", 0, 7, 1, kind, mask, mapped, false), matched);
        FieldSpatialInputGuard[] orAnd = [Guard(FieldScriptInputKind.Held, 3), Guard(FieldScriptInputKind.Held, 1, false), Guard(FieldScriptInputKind.Pressed, 2)];
        Check(FieldMachine.TryFindSpatialInputWitness(orAnd, out FieldInputState input, sharedPhysicalButtons: true) && input.Held == 2 && input.Pressed == 2 && FieldMachine.AreSpatialInputGuardsSatisfied(orAnd, input), "AND across native predicates changed OR within a mask or ignored an unmatched branch.");
        FieldSpatialInputGuard[] contradiction = [Guard(FieldScriptInputKind.Pressed, 1), Guard(FieldScriptInputKind.Released, 1)];
        Check(!FieldMachine.TryFindSpatialInputWitness(contradiction, out _), "A simultaneous press and release of the same native button acquired a fabricated witness.");
        FieldSpatialInputGuard[] negative = [Guard(FieldScriptInputKind.Held, 1), Guard(FieldScriptInputKind.Pressed, 1, false)];
        Check(!FieldMachine.TryFindSpatialInputWitness(negative, out _, sharedPhysicalButtons: true, previousInput: new()), "A held-but-not-pressed predicate ignored the actual previous native input.");
        Check(FieldMachine.TryFindSpatialInputWitness(negative, out input, sharedPhysicalButtons: true) && input.Held == 1 && input.PreviousHeld == 1 && input.Pressed == 0, "A feasible held predicate failed to retain its required preparatory input.");
        FieldSpatialInputGuard[] streams = [Guard(FieldScriptInputKind.Held, 1), Guard(FieldScriptInputKind.Held, 1, false, mapped: false)];
        Check(FieldMachine.TryFindSpatialInputWitness(streams, out input) && input.Held == 1 && input.RawHeld == 0 && FieldMachine.AreSpatialInputGuardsSatisfied(streams, input), "Mapped and raw native input streams were incorrectly conflated.");
        Check(!FieldMachine.TryFindSpatialInputWitness(streams, out _, sharedPhysicalButtons: true), "A common physical button was claimed to satisfy conflicting mapped/raw predicates.");
        Check(FieldMachine.TryFindSpatialInputWitness([Guard(FieldScriptInputKind.Held, 0, false)], out input) && input.Held == 0 && !FieldMachine.TryFindSpatialInputWitness([Guard(FieldScriptInputKind.Held, 0)], out _), "Native zero-mask matched/unmatched semantics changed.");
    }

    private static void SpatialInputBounds()
    {
        List<FieldSpatialInputGuard> constraints = [];
        for (int bit = 0; bit < 13; bit++)
        {
            constraints.Add(new(new($"held:{bit}", "authored", 0, 7, bit + 1, FieldScriptInputKind.Held, (ushort)(1 << bit), true, false), true));
        }
        constraints.Add(new(new("release", "authored", 0, 7, 14, FieldScriptInputKind.Released, 0x1FFF, true, false), true));
        bool rejected = false;
        try
        {
            FieldMachine.TryFindSpatialInputWitness(constraints, out _);
        }
        catch (NotSupportedException error) when (error.Message.Contains("constraint budget", StringComparison.Ordinal))
        {
            rejected = true;
        }
        Check(rejected, "Input witness exhaustion silently fabricated satisfaction or absence.");
        List<byte> gates = [];
        for (int bit = 0; bit < 13; bit++)
        {
            gates.AddRange([0x30, (byte)(1 << bit), (byte)((1 << bit) >> 8), 1]);
        }
        (_, FieldMachine machine, RejectHost host) = Create(InputMain(gates.ToArray()), Transition(99));
        string before = JsonSerializer.Serialize(machine.Capture());
        rejected = false;
        try
        {
            machine.ReadCurrentSpatialDemands(1, Samples);
        }
        catch (NotSupportedException error) when (error.Message.Contains("alternative budget", StringComparison.Ordinal))
        {
            rejected = true;
        }
        Check(rejected && host.Calls == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Spatial input-path exhaustion silently hid an observable frontier or changed native source state.");
    }

    private sealed class SpatialInputOracle : IFieldHost
    {
        public ushort? Destination { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is FieldActorQuery query)
            {
                return new(true, query.Query == FieldActorQueryKind.Triangle ? 26 : 0, Position: new(0, 0, 0, 26));
            }
            if (request.Command is FieldTransitionCommand transition)
            {
                Destination = transition.Map;
                return new(true);
            }
            throw new InvalidOperationException("The authored spatial input oracle reached an unrelated native host operation.");
        }
    }

    private static byte[] DirectBody(bool releaseDeclineMenu = true, bool unsafeIncomingPath = false)
    {
        byte[] body = [0x16, 0x60, 10, 0, 26, 0, 0, 44, 0x33, 1, 0x4A, 1, 0x48, 5, 0, 1, 0, 1, 12, 0x14, 0x50, 12, 0, 0, 11, 0x6D, 16, 0, 0, 0x33, 0, 0x4A, 0, 0x10, 17, 0x33, 0, 0x4A, 0, 0x16, 0x60, 10, 0, 26, 0, 0, 3, 0x12, 8, 0x12, 49, 0];
        if (!releaseDeclineMenu)
        {
            body[38] = 1;
        }
        if (unsafeIncomingPath)
        {
            body[32] = 1;
            body[34] = 5;
        }
        return body;
    }

    private static (FieldProgram Program, FieldMachine Machine, RejectHost Host, int Entry) SharedSource(byte[] producer, byte[]? clobber = null, bool producerLater = false, byte[]? body = null)
    {
        clobber ??= [0];
        body ??= DirectBody();
        int clobberEntry = producer.Length + 2;
        int entry = clobberEntry + clobber.Length + 1;
        byte[] code = [0, .. producer, 0, .. clobber, 0, .. body];
        FieldProgram program = new(code, Enumerable.Range(0, 4).Select(_ => new ushort[32]).ToArray());
        RejectHost host = new();
        FieldMachine initialized = new(program, host: host);
        initialized.Initialize();
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 255);
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 11, 255);
        initialized.Variables.WriteWord(FieldBank.MapWord, 10, 26);
        FieldVmSnapshot source = initialized.Capture();
        source.Entities[0] = source.Entities[0] with { ProgramCounter = producerLater ? entry : 1 };
        source.Entities[1] = source.Entities[1] with { ProgramCounter = 0, ModelBound = true, PartyCharacter = 0 };
        source.Entities[2] = source.Entities[2] with { ProgramCounter = clobberEntry };
        source.Entities[3] = source.Entities[3] with { ProgramCounter = producerLater ? 1 : entry };
        return (program, FieldMachine.Restore(program, source, host), host, entry);
    }

    private static void SharedPhysicalAliasesAndDirectEffects()
    {
        byte[] producer = [0xB9, 6, 1, 10, 0x12, 4];
        (FieldProgram program, FieldMachine machine, RejectHost host, int entry) = SharedSource(producer);
        string before = JsonSerializer.Serialize(machine.Capture());
        int instructions = 0;
        machine.InstructionExecuted = (_, _, _, _) => instructions++;
        FieldSpatialDemand demand = machine.ReadCurrentSpatialDemands(1, Samples).Demands.Single();
        FieldSpatialEffectOwner effect = demand.DirectEffectOwner ?? throw new InvalidOperationException("The direct autonomous effect owner is absent.");
        Check(demand.Entity == 3 && demand.SampleId == 20 && demand.ProgramCounter == entry + 12 && demand.TriangleOnly && demand.MayChangeNavigation && demand.DirectEffectOwner is { Entity: 3, Priority: 7, Command: FieldDialogueCommand }, "A current earlier-owner triangle producer failed to expose its direct autonomous choice/topology consumer.");
        Check(effect.ExitProgramCounters.Contains(entry + 39) && effect.ExitProgramCounters.Contains(entry + 51), "The direct effect lost its post-handback physical poll or native RET boundary.");
        Check(host.Calls == 0 && instructions == 0 && before == JsonSerializer.Serialize(machine.Capture()), "Shared alias projection dispatched native effects, emitted live instruction receipts or changed its source.");
        Check(JsonSerializer.Serialize(demand) == JsonSerializer.Serialize(FieldMachine.Restore(program, machine.Capture(), host).ReadCurrentSpatialDemands(1, Samples).Demands.Single()), "Cold shared-producer discovery changed its native effect provenance.");
        (_, FieldMachine later, _, _) = SharedSource(producer, producerLater: true);
        Check(later.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A later-cycle owner authorized a consumer using stale shared map coordinates.");
        (_, FieldMachine partial, _, _) = SharedSource(producer, [0x80, 0x50, 11, 0, 0]);
        Check(partial.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A byte overwrite retained physical ownership of an overlapping shared triangle WORD.");
        (_, FieldMachine disjoint, _, _) = SharedSource(producer, [0x80, 0x50, 12, 0, 0]);
        Check(disjoint.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 1, "A disjoint byte write destroyed an unchanged physical query alias.");
        (_, FieldMachine unknown, _, _) = SharedSource(producer, [0x99, 5, 12, 0]);
        Check(unknown.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "An unknown intervening host/result tail authorized stale physical provenance.");
        byte[] lateProducer = [0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, 0x80, 0x50, 20, 0, .. producer];
        (_, FieldMachine budget, _, _) = SharedSource(lateProducer);
        Check(budget.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A physical producer beyond the native eight-handler slice was projected into the current cycle.");
        (_, FieldMachine waiting, _, _) = SharedSource([0x24, 3, 0, .. producer]);
        Check(waiting.ReadCurrentSpatialDemands(1, Samples).Demands.Length == 0, "A yielding native wait was bypassed to fabricate a same-cycle shared physical producer.");
    }

    private static void AutonomousBodyExitProofs()
    {
        byte[] producer = [0xB9, 6, 1, 10, 0x12, 4];
        (_, FieldMachine machine, _, int entry) = SharedSource(producer);
        HashSet<FieldControlKind> acquired = [FieldControlKind.PlayerLocked, FieldControlKind.MenuDisabled];
        FieldScriptAnalysis complete = machine.AnalyzeScriptAtProgramCounter(3, entry + 12, exitBoundaries: new HashSet<int> { entry }, acquiredControls: acquired);
        Check(complete.Complete && complete.MayChangeNavigation && complete.ExitProgramCounters.Contains(entry + 39), "A post-effect physical predicate equivalent to its source was not proved after both control handbacks.");
        (_, FieldMachine missing, _, int missingEntry) = SharedSource(producer, body: DirectBody(releaseDeclineMenu: false));
        Check(!missing.AnalyzeScriptAtProgramCounter(3, missingEntry + 12, exitBoundaries: new HashSet<int> { missingEntry }, acquiredControls: acquired).ExitProgramCounters.Contains(missingEntry + 39), "A physical poll completed an effect while its menu control domain remained owned.");
        (_, FieldMachine incoming, _, int incomingEntry) = SharedSource(producer, body: DirectBody(unsafeIncomingPath: true));
        Check(!incoming.AnalyzeScriptAtProgramCounter(3, incomingEntry + 12, exitBoundaries: new HashSet<int> { incomingEntry }, acquiredControls: acquired).ExitProgramCounters.Contains(incomingEntry + 39), "One returned-control path hid another incoming path retaining control at the same physical poll.");
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
