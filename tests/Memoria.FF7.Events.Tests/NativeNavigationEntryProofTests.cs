using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeNavigationEntryProofTests
{
    public static void Run()
    {
        IdempotentControlPublications();
        PartyMembershipBranches();
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
        var stableGuard = Create([0x14, 0x10, 10, 0, 0, 5, 0x80, 0x10, 0, 1, 0]);
        stableGuard.Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 10, 1);
        FieldNavigationEntryProof guarded = Prove(stableGuard.Machine);
        Check(guarded.HasNoEffect && guarded.Reads.Contains(new(FieldBank.Persistent0Byte, 10, false)), "A stable auxiliary guard could not exclude its unreachable predicate writer.");
        var mutableGuard = Create([0x80, 0x10, 10, 0, 0x14, 0x10, 10, 0, 0, 5, 0x80, 0x10, 0, 1, 0]);
        mutableGuard.Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 10, 1);
        Check(!Prove(mutableGuard.Machine).HasNoEffect, "Removing a writable guard did not expose its predicate write arm.");
        var guardAlias = Create([0x80, 0x10, 11, 0, 0x16, 0x20, 10, 0, 0, 0, 0, 5, 0x80, 0x10, 0, 1, 0]);
        guardAlias.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 10, 256);
        Check(!Prove(guardAlias.Machine).HasNoEffect, "A writable high-byte alias retained an assumed word guard.");
        var circular = Create([0x14, 0x10, 10, 0, 0, 5, 0x80, 0x10, 11, 0, 0x14, 0x10, 11, 0, 0, 5, 0x80, 0x10, 10, 0, 0x14, 0x10, 10, 0, 0, 5, 0x80, 0x10, 0, 1, 0]);
        circular.Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 10, 1);
        circular.Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 11, 1);
        Check(Prove(circular.Machine).HasNoEffect, "A closed mutually inductive guard set was rejected.");
        Check(!Prove(circular.Machine, [new(FieldBank.Persistent0Byte, 11, false)]).HasNoEffect, "An external clobber escaped circular guard refinement.");
        var boundedGuard = Create([0x81, 0x20, 10, 0x1A, 2, 0x16, 0x20, 10, 0, 0x13, 5, 4, 5, 0x80, 0x10, 0, 1, 0]);
        boundedGuard.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 10, 535);
        string boundedBefore = JsonSerializer.Serialize(boundedGuard.Machine.Capture());
        Check(Prove(boundedGuard.Machine).HasNoEffect, "Known writes preserving an auxiliary threshold invalidated the predicate.");
        Check(JsonSerializer.Serialize(boundedGuard.Machine.Capture()) == boundedBefore && boundedGuard.Host.Calls == 0, "Bounded-value proof changed native state or dispatched a host.");
        var crossingGuard = Create([0x81, 0x20, 10, 0x14, 5, 0x16, 0x20, 10, 0, 0x13, 5, 4, 5, 0x80, 0x10, 0, 1, 0]);
        crossingGuard.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 10, 535);
        Check(!Prove(crossingGuard.Machine).HasNoEffect, "A known write crossing the threshold hid a reachable predicate writer.");
        foreach (byte[] write in new byte[][] { [0x80, 0x10, 11, 5], [0x99, 0x01, 11] })
        {
            var uncertainGuard = Create(write.Concat(new byte[] { 0x16, 0x20, 10, 0, 0x13, 5, 4, 5, 0x80, 0x10, 0, 1, 0 }).ToArray());
            uncertainGuard.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 10, 535);
            Check(!Prove(uncertainGuard.Machine).HasNoEffect, "A high-byte alias or random result retained an unsafe word threshold.");
        }
        var inputGuard = Create([0x30, 0x20, 0, 6, 0x81, 0x20, 10, 0x14, 5, 0x16, 0x20, 10, 0, 0x13, 5, 4, 5, 0x80, 0x10, 0, 1, 0]);
        inputGuard.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 10, 535);
        Check(!Prove(inputGuard.Machine).HasNoEffect, "A sampled input branch hid a threshold-changing write.");
        foreach (byte opcode in new byte[] { 0x16, 0x18 })
        {
            var signedGuard = Create([0x81, 0x20, 10, 0, 0x80, opcode, 0x20, 10, 0, 0, 0, 3, 5, 0x80, 0x10, 0, 1, 0]);
            signedGuard.Machine.Variables.WriteWord(FieldBank.Persistent0Word, 10, short.MaxValue);
            Check(Prove(signedGuard.Machine).HasNoEffect == (opcode == 0x18), "Bounded word comparisons lost native signed/unsigned wrap semantics.");
        }
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
        var safeTalk = Create([0x80, 0x10, 10, 1, 0], activeProducer: false, childOffset: 0);
        Check(Prove(safeTalk.Machine, entries: [new(2, 1)]).HasNoEffect, "A covered talk producer writing disjoint bytes invalidated the line predicate.");
        var clobberingTalk = Create([0x80, 0x10, 1, 1, 0], activeProducer: false, childOffset: 0);
        Check(!Prove(clobberingTalk.Machine, entries: [new(2, 1)]).HasNoEffect, "A newly offered talk entry overwrote the line predicate through its word alias.");
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

    private static void IdempotentControlPublications()
    {
        FieldMachine CreateControls(byte[]? producer = null, bool active = false, bool busy = false, bool pending = false, bool saved = false)
        {
            byte[] code = [0, 0, 0x33, 0, 0x4A, 0, 0];
            ushort[][] entries = [new ushort[32], new ushort[32], new ushort[32]];
            entries[1][5] = 2;
            entries[2][2] = 7;
            FieldProgram program = new(code.Concat(producer ?? new byte[] { 0 }).ToArray(), entries);
            FieldMachine machine = new(program, host: new RejectHost());
            machine.Initialize();
            if (active || busy || pending || saved)
            {
                FieldVmSnapshot snapshot = machine.Capture();
                if (active)
                {
                    snapshot.Entities[2] = snapshot.Entities[2] with { ProgramCounter = 7 };
                }
                if (busy)
                {
                    snapshot.Entities[1] = snapshot.Entities[1] with { Priority = 1 };
                }
                if (pending)
                {
                    snapshot.Entities[2] = snapshot.Entities[2] with { ProgramCounter = 7 };
                    snapshot = snapshot with { NextRequestId = 2, PendingRequests = [new(1, 2, 7, 7, new FieldControlCommand(FieldControlKind.MenuDisabled, 1))] };
                }
                if (saved)
                {
                    snapshot.Entities[2] = snapshot.Entities[2] with { Priority = 0 };
                    snapshot.Entities[2].SavedProgramCounters[7] = 7;
                }
                machine = FieldMachine.Restore(program, snapshot, new RejectHost());
            }
            return machine;
        }
        FieldNavigationEntryProof ProveControls(FieldMachine machine, bool near = false, bool menuDisabled = false, int animation = 0)
        {
            FieldNavigationEntryPoint[] entries = [new(2, 2)];
            return machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [], entries, controls: new(false, menuDisabled, 0, animation, near ? entries : []));
        }
        FieldMachine stable = CreateControls();
        string before = JsonSerializer.Serialize(stable.Capture());
        Check(ProveControls(stable).HasNoEffect, "Equal native unlock/menu publications were rejected at a closed ordinary boundary.");
        Check(!stable.ProveNavigationEntryHasNoEffect(1, 5, new(), [], []).HasNoEffect, "Control publications were accepted without captured host facts.");
        Check(!ProveControls(stable, menuDisabled: true).HasNoEffect, "A menu-state change was mistaken for an idempotent publication.");
        Check(!ProveControls(stable, animation: 1).HasNoEffect, "Unlock's native player-animation reset was ignored.");
        Check(before == JsonSerializer.Serialize(stable.Capture()), "Control admission modified native VM state.");
        FieldMachine far = CreateControls([0x4A, 1, 0]);
        Check(ProveControls(far).HasNoEffect, "An untriggerable external menu writer invalidated the scoped control proof.");
        Check(!ProveControls(far, near: true).HasNoEffect, "A physically possible external menu writer escaped the scoped producer closure.");
        Check(!ProveControls(CreateControls([0x4A, 1, 0], active: true)).HasNoEffect, "An already active distant writer was incorrectly excluded by geometry.");
        Check(!ProveControls(CreateControls([0x4A, 1, 0], pending: true)).HasNoEffect, "A pending native host-request owner escaped the control producer roots.");
        Check(!ProveControls(CreateControls([0x4A, 1, 0], saved: true)).HasNoEffect, "A saved native continuation escaped the control producer roots.");
        Check(!ProveControls(CreateControls([0xD1, 1, 0], active: true)).HasNoEffect, "A LINE enable producer could invalidate the external-entry set.");
        foreach (var mode in new (byte[] Code, string Command)[] { (new byte[] { 0x49, 0, 7, 0, 0 }, nameof(FieldMenuCommand)), (new byte[] { 0x70, 0, 0, 0, 0 }, nameof(FieldBattleCommand)), (new byte[] { 0x20, 65, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 }, nameof(FieldMinigameCommand)) })
        {
            FieldMachine externalMode = CreateControls(mode.Code);
            Check(ProveControls(externalMode).HasNoEffect, "An unreachable external host mode invalidated a closed control entry.");
            FieldNavigationEntryProof reachable = ProveControls(externalMode, near: true);
            Check(!reachable.HasNoEffect && reachable.Reason!.Contains(mode.Command, StringComparison.Ordinal), "A reachable opaque host mode was treated as a closed control producer.");
            FieldNavigationEntryProof activeMode = ProveControls(CreateControls(mode.Code, active: true));
            Check(!activeMode.HasNoEffect && activeMode.Reason!.Contains(mode.Command, StringComparison.Ordinal), "An active opaque host mode escaped the captured-owner closure.");
        }
        Check(ProveControls(CreateControls([0xB4, 0, 64, 2, 1, 2, 0], active: true)).HasNoEffect, "An NPC facing-only producer was unnecessarily treated as a geometry or control clobber.");
        FieldNavigationEntryProof relocation = ProveControls(CreateControls([0xA5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], active: true));
        Check(!relocation.HasNoEffect && relocation.Reason!.Contains("FieldActorPlacement", StringComparison.Ordinal), "A scheduled actor relocation could invalidate the scoped geometry.");
        Check(!ProveControls(CreateControls(busy: true)).HasNoEffect, "An unpreemptible entry owner was certified as a new native entry.");
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

    private static void PartyMembershipBranches()
    {
        byte[] conditional = [0xCB, 1, 14, 0x16, 0x20, 0, 0, 0x7E, 2, 3, 6, 0x80, 0x10, 20, 1, 0, 0];
        foreach (bool present in new[] { false, true })
        {
            var sample = PartyEntry(conditional);
            sample.Machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, present ? (byte)1 : (byte)2);
            string before = JsonSerializer.Serialize(sample.Machine.Capture());
            FieldNavigationEntryProof proof = sample.Machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [], []);
            Check(proof.Complete && proof.HasNoEffect && proof.Reads.Contains(new(FieldBank.Persistent0Word, 0, true)), "Both party-membership successors did not retain their current stable native predicate proof.");
            Check(before == JsonSerializer.Serialize(sample.Machine.Capture()) && sample.Host.Calls == 0, "Party branch proof changed native party, variables, scheduler or host state.");
            Check(!sample.Machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [new(FieldBank.Persistent0Byte, 1, false)], []).HasNoEffect, "Party branch proof lost external byte-alias invalidation of its story guard.");
            var effect = PartyEntry([0xCB, 1, 5, 0x80, 0x10, 20, 1, 0]);
            effect.Machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, present ? (byte)1 : (byte)2);
            Check(!effect.Machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [], []).HasNoEffect, "A party outcome with a side effect was skipped using the other outcome's current membership.");
        }
        var writer = PartyEntry(conditional, [0x81, 0x20, 0, 0x70, 2, 0]);
        Check(!writer.Machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [], []).HasNoEffect, "A real native writer invalidating a party-guard successor escaped producer closure.");
        foreach (byte[] code in new byte[][] { [0xCB, 1], [0xCB, 1, 1, 0x11, 0xFC, 0xFF], [0xCB, 1, 2, 0xFF, 0, 0] })
        {
            var invalid = PartyEntry(code);
            Check(!invalid.Machine.ProveNavigationEntryHasNoEffect(1, 5, new(), [], []).HasNoEffect, "A truncated, cyclic or unsupported party successor was certified read-only.");
        }
        Console.WriteLine("PASS read-only party branches require both native successors and retain variable writer invalidation");
    }

    private static (FieldMachine Machine, RejectHost Host) PartyEntry(byte[] entry, byte[]? producer = null)
    {
        byte[] code = [0, 0, .. entry, .. (producer ?? [])];
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][5] = 2;
        FieldProgram program = new(code, entries);
        RejectHost host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 641);
        if (producer is not null)
        {
            FieldVmSnapshot snapshot = machine.Capture();
            snapshot.Entities[0] = snapshot.Entities[0] with { ProgramCounter = 2 + entry.Length };
            machine = FieldMachine.Restore(program, snapshot, host);
        }
        return (machine, host);
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
