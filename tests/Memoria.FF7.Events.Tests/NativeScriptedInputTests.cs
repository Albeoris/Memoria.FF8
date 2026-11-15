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
