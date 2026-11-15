using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeScriptedInputTests
{
    public static void Run()
    {
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
