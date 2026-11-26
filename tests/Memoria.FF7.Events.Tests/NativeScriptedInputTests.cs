using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeScriptedInputTests
{
    public static void Run()
    {
        NativeNavigationEntryProofTests.Run();
        CheckMatchedPollingContinuations();
        CheckSkippableEntryProof();
        CheckTimedPollingLoop();
        CheckEvenTimedEdgePolling();
        for (int opcode = 0x30; opcode <= 0x32; opcode++)
        {
            ushort[] entries = new ushort[32];
            entries[1] = 1;
            entries[2] = 1;
            FieldMachine machine = new(new([0, (byte)opcode, 0x20, 2, 2, 0, 0x12, 5], [entries]));
            machine.Initialize();
            string before = JsonSerializer.Serialize(machine.Capture());
            FieldScriptInputDemand demand = machine.ReadCurrentInputDemands().Single();
            Check(demand is { Entity: 0, Priority: 7, ProgramCounter: 1, Mask: 0x220, Mapped: true, AwaitingInput: true } && demand.Kind == (FieldScriptInputKind)(opcode - 0x30), "The current KEY family demand lost its mask, mapping, kind or native owner.");
            Check(before == JsonSerializer.Serialize(machine.Capture()), "Reading scripted input mutated native VM or input state.");
            FieldMachine cold = FieldMachine.Restore(new([0, (byte)opcode, 0x20, 2, 2, 0, 0x12, 5], [entries]), machine.Capture());
            Check(demand == cold.ReadCurrentInputDemands().Single(), "Cold scripted-input observation changed its stable owner identity.");
            Check(machine.TryRequestScript(0, 2) && machine.ReadCurrentInputDemands().Single() is { Priority: 1, ProgramCounter: 1 }, "Aliased script slots lost their actual active request owner.");
        }
        FieldMachine idle = new(new([0, 0], [new ushort[32]]));
        idle.Initialize();
        Check(idle.ReadCurrentInputDemands().Count == 0, "An idle main RET became an invented button demand.");
        FieldMachine branch = new(new([0, 0x30, 0x20, 0, 1, 0], [new ushort[32]]));
        branch.Initialize();
        Check(branch.ReadCurrentInputDemands().Single() is { Mapped: false, AwaitingInput: false }, "An ordinary KEY condition became a wait-loop demand.");
        ushort[] gatedEntries = new ushort[32];
        gatedEntries[4] = 1;
        FieldMachine gatedContact = new(new([0, 0x31, 0x20, 2, 2, 0x5F, 0], [gatedEntries]));
        gatedContact.Initialize();
        string gateBefore = JsonSerializer.Serialize(gatedContact.Capture());
        Check(gatedContact.ReadCurrentInputDemands().All(demand => !demand.AwaitingInput), "A false-RET KEYON contact was incorrectly treated as a current input wait.");
        FieldScriptInputDemand? entryGate = gatedContact.ReadEntryInputDemand(0, 4);
        Check(entryGate is { Entity: 0, Priority: 1, ProgramCounter: 1, Kind: FieldScriptInputKind.Pressed, Mask: 0x220, Mapped: true, AwaitingInput: false }, "Entry discovery lost a native false-RET KEYON gate or synthesized a wait loop.");
        Check(gateBefore == JsonSerializer.Serialize(gatedContact.Capture()), "Entry input discovery changed the live PC, priority, variables or buttons.");
        RejectHost host = new();
        FieldMachine query = new(new([0, 0x99, 0x50, 0, 0x30, 0x20, 0, 2, 0, 0x12, 5], [new ushort[32]]), host: host);
        query.Initialize();
        Check(query.ReadCurrentInputDemands().Count == 0 && host.Calls == 0, "Input lookahead crossed a host/RNG query.");
        ushort[] queryEntries = new ushort[32];
        queryEntries[4] = 1;
        FieldMachine queriedEntry = new(new([0, 0x99, 0x50, 0, 0x31, 0x20, 2, 2, 0x5F, 0], [queryEntries]), host: host);
        queriedEntry.Initialize();
        Check(queriedEntry.ReadEntryInputDemand(0, 4) is null && host.Calls == 0, "Entry input discovery fabricated a host query result before an input gate.");
        FieldProgram chainedProgram = new([0, 0x30, 0, 0x10, 5, 0x99, 0x50, 0, 0, 0x30, 0x20, 2, 5, 0x99, 0x50, 0, 0, 0x30, 0x40, 2, 5, 0x99, 0x50, 0, 0, 0x12, 24], [new ushort[32]]);
        FieldMachine chained = new(chainedProgram, host: host);
        chained.Initialize();
        string chainedBefore = JsonSerializer.Serialize(chained.Capture());
        FieldScriptInputDemand[] chainedDemands = chained.ReadCurrentInputDemands().ToArray();
        Check(chainedDemands.Length == 3 && chainedDemands.All(candidate => candidate.AwaitingInput), "A chained native input loop failed to expose all of its proved gates.");
        Check(chainedDemands.Select(candidate => candidate.ProgramCounter).SequenceEqual(new[] { 1, 9, 17 }) && chainedDemands.Select(candidate => candidate.Mask).SequenceEqual(new ushort[] { 0x1000, 0x220, 0x240 }), "Chained demand discovery changed source order, gate owners or masks.");
        Check(host.Calls == 0 && chainedBefore == JsonSerializer.Serialize(chained.Capture()), "Chained input observation executed a matched handler or changed the live VM.");
        FieldMachine chainedCold = FieldMachine.Restore(chainedProgram, chained.Capture(), host);
        Check(chainedDemands.SequenceEqual(chainedCold.ReadCurrentInputDemands()), "Cold chained input observation changed its gates or owner identities.");
        chained.SetInput(0x20, 0, 0x1000);
        string heldBefore = JsonSerializer.Serialize(chained.Capture());
        Check(chainedDemands.SequenceEqual(chained.ReadCurrentInputDemands()) && host.Calls == 0 && heldBefore == JsonSerializer.Serialize(chained.Capture()), "Held buttons caused chained inspection to execute an accepted handler.");
        FieldMachine unsafeHost = new(new([0, 0x30, 0x20, 2, 5, 0, 0, 0, 0, 0x99, 0x50, 0, 0x12, 11], [new ushort[32]]), host: host);
        unsafeHost.Initialize();
        Check(unsafeHost.ReadCurrentInputDemands().All(candidate => !candidate.AwaitingInput) && host.Calls == 0, "An unmatched host/RNG operation was accepted as an input-only loop.");
        FieldMachine mutatingLoop = new(new([0, 0x30, 0x20, 2, 5, 0, 0, 0, 0, 0x80, 0x50, 0, 1, 0x12, 12], [new ushort[32]]));
        mutatingLoop.Initialize();
        string mutationBefore = JsonSerializer.Serialize(mutatingLoop.Capture());
        Check(mutatingLoop.ReadCurrentInputDemands().All(candidate => !candidate.AwaitingInput) && mutationBefore == JsonSerializer.Serialize(mutatingLoop.Capture()), "An unmatched variable mutation was accepted as an input-only loop or leaked into the live VM.");
        FieldMachine loop = new(new([0, 0x12, 0], [new ushort[32]]));
        loop.Initialize();
        Check(loop.ReadCurrentInputDemands(4).Count == 0, "A pure main loop exceeded bounded lookahead or invented input.");
    }

    private static void CheckMatchedPollingContinuations()
    {
        byte[] code = [0, 0x31, 0x20, 0, 6, 0x95, 0x05, 4, 0x12, 7, 0x31, 0x80, 0, 3, 0, 0, 0x12, 15];
        FieldProgram program = new(code, [new ushort[32]]);
        FieldMachine machine = new(program);
        machine.Initialize();
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldScriptInputDemand[] demands = machine.ReadCurrentInputDemands().ToArray();
        Check(demands.Length == 2 && demands[0].MatchedContinuation == FieldScriptInputContinuation.ReturnsToPollingLoop && demands[1].MatchedContinuation == FieldScriptInputContinuation.LeavesPollingLoop, "A mutable adjustment branch and a finite polling-phase exit were confused.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && machine.Variables.ReadByte(FieldBank.MapByte, 4) == 0, "Matched-branch classification executed a live adjustment or changed native ownership.");
        Check(demands.SequenceEqual(FieldMachine.Restore(program, machine.Capture()).ReadCurrentInputDemands()), "Cold classification changed the polling branch outcome.");
        FieldMachine extraGate = new(new([0, 0x31, 0x20, 0, 7, 0x31, 0x80, 0, 3, 0, 0, 0x12, 10], [new ushort[32]]));
        extraGate.Initialize();
        Check(extraGate.ReadCurrentInputDemands().First() is { AwaitingInput: true, MatchedContinuation: FieldScriptInputContinuation.Unknown }, "An unfamiliar matched-path input gate with a return edge was falsely certified to leave the original polling SCC.");
        code[5] = 0x99;
        RejectHost host = new();
        FieldMachine unknown = new(new(code, [new ushort[32]]), host: host);
        unknown.Initialize();
        Check(unknown.ReadCurrentInputDemands().First().MatchedContinuation == FieldScriptInputContinuation.Unknown, "An unexecuted host/random handler was falsely certified to exit or rejoin a polling phase.");
    }

    private static void CheckSkippableEntryProof()
    {
        ushort[] entries = new ushort[32];
        entries[4] = 1;
        RejectHost host = new();
        foreach (byte opcode in new byte[] { 0x30, 0x31, 0x32 })
        {
            foreach (bool mapped in new[] { false, true })
            {
                ushort mask = mapped ? (ushort)0x240 : (ushort)0x40;
                FieldProgram program = new([0, opcode, (byte)mask, (byte)(mask >> 8), 4, 0x99, 0x50, 0, 0], [entries]);
                FieldMachine machine = new(program, host: host);
                machine.Initialize();
                int callbacks = 0;
                machine.InstructionExecuted = (_, _, _, _) => callbacks++;
                string before = JsonSerializer.Serialize(machine.Capture());
                Check(machine.CanSkipScriptEntryForInput(0, 4, new()), "An unmatched native KEY gate followed by RET was not proved inert.");
                FieldInputState active = new(Held: mask, Pressed: mask, Released: mask, RawHeld: mask, RawPressed: mask, RawReleased: mask);
                Check(!machine.CanSkipScriptEntryForInput(0, 4, active), "A matched native KEY gate crossed a host operation in the skip proof.");
                FieldInputState otherStream = mapped ? new(RawHeld: mask, RawPressed: mask, RawReleased: mask) : new(Held: mask, Pressed: mask, Released: mask);
                Check(machine.CanSkipScriptEntryForInput(0, 4, otherStream), "The skip proof confused mapped and raw native input streams.");
                Check(!machine.CanSkipScriptEntryForInput(0, 4, new(), 1), "The skip proof ignored its instruction budget before RET.");
                Check(before == JsonSerializer.Serialize(machine.Capture()) && callbacks == 0 && host.Calls == 0, "The skip proof changed live state or dispatched a live callback.");
                FieldMachine cold = FieldMachine.Restore(program, machine.Capture(), host);
                Check(cold.CanSkipScriptEntryForInput(0, 4, new()), "Cold restoration changed the native input-only skip proof.");
            }
        }
        foreach (byte[] body in new byte[][]
        {
            [0x99, 0x50, 0, 0],
            [0x80, 0x50, 0, 1, 0],
            [0x01, 0, 0, 0],
            [0x5F, 0],
            [0x24, 0, 0, 0],
            [0x12, 0, 0],
            [0x14, 0x50, 0, 0, 0, 2, 0, 0x5F, 0],
            [0x14, 0x50, 0, 0, 0, 2, 0x5F, 0]
        })
        {
            FieldMachine machine = new(new([0, 0x30, 0x40, 2, 1, .. body], [entries]), host: host);
            machine.Initialize();
            string before = JsonSerializer.Serialize(machine.Capture());
            Check(!machine.CanSkipScriptEntryForInput(0, 4, new()), $"An unmatched KEY path with unproved continuation {Convert.ToHexString(body)} was incorrectly proved inert.");
            Check(before == JsonSerializer.Serialize(machine.Capture()) && host.Calls == 0, "A rejected skip proof changed native state or called the live host.");
        }
        FieldMachine jumps = new(new([0, 0x10, 1, 0x11, 2, 0, 0], [entries]));
        jumps.Initialize();
        Check(jumps.CanSkipScriptEntryForInput(0, 4, new()), "Pure native forward jumps to RET were not proved inert.");
        FieldMachine conditionalReturns = new(new([0, 0x30, 0x40, 2, 1, 0x14, 0x50, 0, 0, 0, 2, 0, 0], [entries]));
        conditionalReturns.Initialize();
        Check(conditionalReturns.CanSkipScriptEntryForInput(0, 4, new()), "A variable predicate with both successors returning was not proved inert.");
        FieldMachine uninitialized = new(new([0, 0], [entries]));
        Check(!uninitialized.CanSkipScriptEntryForInput(0, 4, new()), "An uninitialized VM supplied an entry skip proof.");
    }

    private static void CheckTimedPollingLoop()
    {
        RejectHost host = new();
        FieldProgram program = TimedPollingProgram([]);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldScriptInputDemand demand = machine.ReadCurrentInputDemands().Single();
        Check(demand is { ProgramCounter: 5, Priority: 7, Kind: FieldScriptInputKind.Held, Mask: 0x40, AwaitingInput: true, DelayTicks: 16 }, "A native WAIT/YIELD polling loop did not expose its proved current input gate and exact leading delay.");
        Check(machine.ReadCurrentInputDemands(2).Count == 0, "Skipping native polling delays bypassed the instruction budget.");
        Check(machine.ReadEntryInputDemand(0, 4) is null, "Current polling support weakened entry admission across a native WAIT.");
        Check(JsonSerializer.Serialize(machine.Capture()) == before && host.Calls == 0, "Timed polling inspection changed live native PCs, timers, variables, input or hosts.");
        machine.AdvanceTick();
        FieldVmSnapshot waiting = machine.Capture();
        Check(waiting.Entities[0].WaitCounter == 15 && waiting.Entities[0].ProgramCounter == 1, "The authored native WAIT did not establish its real timer and retained continuation.");
        before = JsonSerializer.Serialize(waiting);
        FieldScriptInputDemand waitingDemand = machine.ReadCurrentInputDemands().Single();
        Check(waitingDemand == (demand with { DelayTicks = 15 }), "An active native timer hid its proved future polling demand or changed its remaining delay.");
        Check(JsonSerializer.Serialize(machine.Capture()) == before && host.Calls == 0, "Reading a timed native gate cleared or advanced its authoritative timer.");
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(before)!, host);
        Check(cold.ReadCurrentInputDemands().Single() == waitingDemand && JsonSerializer.Serialize(cold.Capture()) == before, "Cold timed polling observation changed native timer state or gate identity.");
        FieldMachine yieldEntry = new(TimedPollingProgram([], 4), host: host);
        yieldEntry.Initialize();
        Check(yieldEntry.ReadEntryInputDemand(0, 4) is null, "Current polling support weakened entry admission across a native YIELD.");
        foreach (byte[] body in new byte[][] { [0x80, 0x50, 0, 1], [0x99, 0x50, 0] })
        {
            FieldMachine unsafeLoop = new(TimedPollingProgram(body), host: host);
            unsafeLoop.Initialize();
            string unchanged = JsonSerializer.Serialize(unsafeLoop.Capture());
            Check(unsafeLoop.ReadCurrentInputDemands().All(candidate => !candidate.AwaitingInput), "A timed no-button path with variable mutation or host effects was accepted as an input-only loop.");
            Check(JsonSerializer.Serialize(unsafeLoop.Capture()) == unchanged && host.Calls == 0, "Rejecting an unsafe timed polling loop changed live native state or dispatched a host.");
        }
    }

    private static void CheckEvenTimedEdgePolling()
    {
        ushort[] entries = new ushort[32];
        entries[4] = 1;
        FieldMachine machine = new(new([0, 0x24, 16, 0, 0x31, 0x40, 0, 2, 0, 0x12, 8], [entries]));
        machine.Initialize();
        for (int elapsed = 0; elapsed <= 16; elapsed++)
        {
            string unchanged = JsonSerializer.Serialize(machine.Capture());
            FieldScriptInputDemand demand = machine.ReadCurrentInputDemands().Single();
            Check(demand is { ProgramCounter: 4, Kind: FieldScriptInputKind.Pressed, AwaitingInput: true } && demand.DelayTicks == 16 - elapsed, "An even native WAIT polling loop lost the exact tick for its input edge.");
            Check(JsonSerializer.Serialize(machine.Capture()) == unchanged, "Reading a delayed input edge advanced the authoritative polling timer.");
            if (elapsed < 16)
            {
                machine.AdvanceTick();
            }
        }
        Check(machine.Capture().Entities[0].WaitCounter == 1, "The zero-delay edge was not scheduled at the native WAIT completion callback.");
        machine.SetInput(0x40, 0x40);
        machine.AdvanceTick();
        Check(machine.Capture().Entities[0].ProgramCounter == 8 && machine.Capture().Entities[0].WaitCounter == 0, "A pressed edge at zero delay missed its native even-period polling gate.");
        Check(machine.ReadEntryInputDemand(0, 4) is null, "Delayed current-demand support changed entry WAIT admission.");
        FieldMachine chained = new(new([0, 0x24, 16, 0, 0x30, 0x40, 0, 2, 0, 0x31, 0x80, 0, 2, 0, 0x12, 13], [new ushort[32]]));
        chained.Initialize();
        FieldScriptInputDemand[] chainedDemands = chained.ReadCurrentInputDemands().ToArray();
        Check(chainedDemands.Length == 2 && chainedDemands.All(demand => demand.AwaitingInput && demand.DelayTicks == 16) && chainedDemands.Select(demand => demand.ProgramCounter).SequenceEqual(new[] { 4, 9 }), "Additional no-button gates accumulated another polling cycle instead of retaining their shared delay.");
        FieldMachine zeroWait = new(new([0, 0x24, 0, 0, 0x31, 0x40, 0, 2, 0, 0x12, 8], [new ushort[32]]));
        zeroWait.Initialize();
        string zeroBefore = JsonSerializer.Serialize(zeroWait.Capture());
        Check(zeroWait.ReadCurrentInputDemands().Single() is { Kind: FieldScriptInputKind.Pressed, AwaitingInput: true, DelayTicks: 1 }, "An initial WAIT0 lost its real yielded callback before polling an input edge.");
        Check(JsonSerializer.Serialize(zeroWait.Capture()) == zeroBefore, "Reading an initial WAIT0 advanced its authoritative yield.");
        zeroWait.AdvanceTick();
        Check(zeroWait.ReadCurrentInputDemands().Single() is { ProgramCounter: 4, DelayTicks: 0 }, "A yielded WAIT0 retained a synthetic delay at its actual input gate.");
        zeroWait.SetInput(0x40, 0x40);
        zeroWait.AdvanceTick();
        Check(zeroWait.Capture().Entities[0].ProgramCounter == 8, "A pressed edge after WAIT0's real yield missed its native input gate.");
    }

    private static FieldProgram TimedPollingProgram(byte[] unmatchedBody, ushort entry = 1)
    {
        List<byte> code = [0, 0x24, 15, 0, 0x5F, 0x30, 0x40, 0, 2, 0, 0x5F];
        code.AddRange(unmatchedBody);
        int backJump = code.Count;
        code.Add(0x12);
        code.Add(checked((byte)(backJump - 1)));
        ushort[] entries = new ushort[32];
        entries[4] = entry;
        return new(code.ToArray(), [entries]);
    }

    private sealed class RejectHost : IFieldHost
    {
        public int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Input observation must not dispatch the authoritative host.");
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
