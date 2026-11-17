using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeNavigationEntryProofTests
{
    public static void Run()
    {
        var stable = Create();
        string before = JsonSerializer.Serialize(stable.Machine.Capture());
        FieldNavigationEntryProof proof = Prove(stable.Machine);
        Check(proof.Complete && proof.HasNoEffect && proof.Reads.SequenceEqual(new[] { new FieldVariableRead(FieldBank.Persistent0Word, 0, true) }), "Stable native conditional returns did not expose their exact predicate dependency.");
        Check(!stable.Machine.CanSkipScriptEntryForInput(1, 5, new()), "The broader proof changed the original input-only admission contract.");
        Check(JsonSerializer.Serialize(stable.Machine.Capture()) == before && stable.Host.Calls == 0, "Entry proof changed native PCs, variables, owners, input or hosts.");
        FieldMachine cold = FieldMachine.Restore(stable.Program, JsonSerializer.Deserialize<FieldVmSnapshot>(before)!, stable.Host);
        Check(Prove(cold).HasNoEffect && JsonSerializer.Serialize(cold.Capture()) == before, "Cold immutable-read proof changed native state or its result.");
        FieldVmSnapshot partialState = stable.Machine.Capture();
        FieldMachine partial = FieldMachine.Restore(stable.Program, partialState with { CycleCursor = new(1, 7, partialState.Entities[1].ProgramCounter) }, stable.Host);
        Check(!Prove(partial).HasNoEffect, "A partially consumed handler quantum cannot certify an entry's projected input path.");
        Check(!Prove(stable.Machine, [new(FieldBank.Persistent0Byte, 1, false)]).HasNoEffect, "An external publication overlapping the high byte of a word predicate was admitted.");
        Check(Prove(stable.Machine, [new(FieldBank.Persistent0Byte, 2, false)]).HasNoEffect, "An unrelated external publication invalidated a closed predicate.");
        foreach (int owner in new[] { 0, 2 })
        {
            var writer = Create([0x80, 0x10, 1, 1, 0], owner);
            Check(!Prove(writer.Machine).HasNoEffect, "An earlier or later native producer could overwrite a word predicate through its byte alias.");
        }
        var unrelated = Create([0x80, 0x10, 10, 1, 0]);
        Check(Prove(unrelated.Machine).HasNoEffect, "A producer writing disjoint canonical bytes invalidated the predicate.");
        var randomUnrelated = Create([0x99, 0x01, 10, 0]);
        string randomBefore = JsonSerializer.Serialize(randomUnrelated.Machine.Capture());
        Check(Prove(randomUnrelated.Machine).HasNoEffect, "An unrelated random output invalidated a producer write-set proof.");
        Check(!randomUnrelated.Machine.AnalyzeScriptAtProgramCounter(0, randomUnrelated.ProducerPc).Complete, "Write-set inspection relaxed ordinary stochastic event coverage.");
        Check(JsonSerializer.Serialize(randomUnrelated.Machine.Capture()) == randomBefore && randomUnrelated.Host.Calls == 0, "Random producer inspection advanced the native VM or host RNG.");
        var randomAlias = Create([0x99, 0x01, 1, 0]);
        Check(!Prove(randomAlias.Machine).HasNoEffect, "A random write through the predicate's high-byte alias escaped the producer closure.");
        var randomBranch = Create([0x99, 0x05, 10, 0x14, 0x50, 10, 0, 0, 5, 0x80, 0x10, 0, 1, 0]);
        randomBranch.Machine.Variables.WriteByte(FieldBank.MapByte, 10, 1);
        Check(!Prove(randomBranch.Machine).HasNoEffect, "A random-dependent conditional hid its reachable predicate write arm.");
        var conditional = Create([0x14, 0x10, 10, 0, 0, 5, 0x80, 0x10, 0, 1, 0]);
        Check(!Prove(conditional.Machine).HasNoEffect, "A producer conditional outside the immutable domain hid its reachable write arm.");
        var request = Create([0x01, 2, 0x21, 0, 0x80, 0x10, 0, 1, 0], childOffset: 4);
        Check(!Prove(request.Machine).HasNoEffect, "A requested child writer escaped the native producer closure.");
        var saved = Create([0x80, 0x10, 0, 1, 0], activeProducer: false);
        FieldVmSnapshot savedState = saved.Machine.Capture();
        int[] returns = (int[])savedState.Entities[0].SavedProgramCounters.Clone();
        returns[7] = saved.ProducerPc;
        savedState.Entities[0] = savedState.Entities[0] with { SavedProgramCounters = returns };
        FieldMachine resumed = FieldMachine.Restore(saved.Program, savedState, saved.Host);
        Check(!Prove(resumed).HasNoEffect, "A saved native return continuation escaped the producer closure.");
        var external = Create([0x80, 0x10, 0, 1, 0], activeProducer: false, childOffset: 0);
        Check(Prove(external.Machine).HasNoEffect, "An unrequested external body became an invented native producer.");
        Check(!Prove(external.Machine, entries: [new(2, 1)]).HasNoEffect, "A competing external entry, including a possible talk handler, escaped the closure.");
        foreach (byte[] producer in new byte[][] { [0x99, 0x10, 0, 0], [0xC1, 0x22, 0x22, 0, 0, 2, 4, 6, 0], [0xA1, 0, 0], [0xD0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0], [0xFF, 0] })
        {
            var unsupported = Create(producer);
            string snapshot = JsonSerializer.Serialize(unsupported.Machine.Capture());
            Check(!Prove(unsupported.Machine).HasNoEffect, "An unknown, query-dependent or allocation-changing producer was accepted.");
            Check(JsonSerializer.Serialize(unsupported.Machine.Capture()) == snapshot && unsupported.Host.Calls == 0, "Rejecting an incomplete closure mutated live execution or dispatched a host.");
        }
        var bound = Create([0xA1, 0, 0]);
        Check(Prove(bound.Machine, entries: [new(0, 2)]).HasNoEffect, "A model registration with its future contact entry covered was rejected.");
        var line = Create([0xD0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0]);
        Check(!Prove(line.Machine, entries: [new(0, 2)]).HasNoEffect, "Partial native LINE entry coverage admitted a new trigger owner.");
        Check(Prove(line.Machine, entries: Enumerable.Range(2, 5).Select(script => new FieldNavigationEntryPoint(0, script)).ToArray()).HasNoEffect, "A LINE registration with every future movement-trigger entry covered was rejected.");
        var controlled = Create([0xBF, 2, 0]);
        Check(!Prove(controlled.Machine, entries: [new(0, 2)]).HasNoEffect, "Player-control coverage was checked against the caller instead of the native target.");
        Check(Prove(controlled.Machine, entries: [new(2, 2)]).HasNoEffect, "An explicitly covered native player-control target was rejected.");
        Check(!stable.Machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [], [], 1).HasNoEffect, "Producer exhaustion was accepted as an invariance proof.");
        ushort[] longEntries = new ushort[32];
        longEntries[5] = 2;
        FieldMachine longEntry = new(new(new byte[] { 0, 0 }.Concat(Enumerable.Repeat(new byte[] { 0x10, 1 }, 8).SelectMany(bytes => bytes)).Append((byte)0).ToArray(), [longEntries]));
        longEntry.Initialize();
        Check(!longEntry.ProveNavigationEntryHasNoEffect(0, 5, new(), [], []).HasNoEffect, "A return beyond the native eight-handler quantum was accepted.");
    }

    private static FieldNavigationEntryProof Prove(FieldMachine machine, FieldVariableRead[]? writes = null, FieldNavigationEntryPoint[]? entries = null)
    {
        return machine.ProveNavigationEntryHasNoEffect(1, 5, new(), writes ?? [], entries ?? []);
    }

    private static (FieldMachine Machine, FieldProgram Program, RejectHost Host, int ProducerPc) Create(byte[]? producer = null, int producerOwner = 0, bool activeProducer = true, int? childOffset = null)
    {
        byte[] code = [0, 0, 0x16, 0x20, 0, 0, 0x4A, 6, 4, 5, 0x80, 0x10, 0, 1, 0x16, 0x20, 0, 0, 0xDB, 1, 3, 5, 0x80, 0x10, 0, 1, 0x16, 0x20, 0, 0, 2, 2, 0, 5, 0x80, 0x10, 0, 1, 0];
        ushort[][] entries = [new ushort[32], new ushort[32], new ushort[32]];
        entries[1][5] = 2;
        if (childOffset is int offset)
        {
            entries[2][1] = checked((ushort)(code.Length + offset));
        }
        FieldProgram program = new(code.Concat(producer ?? []).ToArray(), entries);
        RejectHost host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 523);
        if (producer is not null && activeProducer)
        {
            FieldVmSnapshot snapshot = machine.Capture();
            snapshot.Entities[producerOwner] = snapshot.Entities[producerOwner] with { ProgramCounter = code.Length };
            machine = FieldMachine.Restore(program, snapshot, host);
        }
        return (machine, program, host, code.Length);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RejectHost : IFieldHost
    {
        public int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("A proof must not dispatch a live host.");
        }
    }
}
