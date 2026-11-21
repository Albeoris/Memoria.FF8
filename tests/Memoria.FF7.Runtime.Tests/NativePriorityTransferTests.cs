using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePriorityTransferTests
{
    private sealed class RejectingHost : IFieldHost
    {
        public FieldHostReply Execute(FieldHostRequest request)
        {
            throw new InvalidOperationException("Same-entity priority transfer must not issue host work.");
        }
    }

    internal static void Run()
    {
        foreach ((int sourcePriority, int targetPriority, int script) in new[] { (0, 7, 31), (3, 3, 1), (6, 1, 1), (7, 7, 1), (1, 2, 1) })
        {
            byte packed = (byte)(targetPriority << 5 | script);
            byte[] code = [0, 7, packed, 0xFF, 0, 0, 0, 0, 0, 0, 0x80, 0x50, 126, 77, 0x5F, 0];
            byte[] original = code.ToArray();
            ushort[][] entries = [new ushort[32]];
            entries[0][script] = 10;
            FieldProgram program = new(code, entries);
            FieldMachine source = new(program, host: new RejectingHost());
            source.Initialize();
            FieldVmSnapshot initial = source.Capture();
            int[] saved = Enumerable.Range(1, 8).ToArray();
            FieldSyncState[] sync = Enumerable.Repeat(FieldSyncState.Waiting, 8).ToArray();
            sync[2] = FieldSyncState.None;
            int[] owners = new int[8];
            owners[2] = -1;
            FieldVmSnapshot snapshot = initial with { Entities = [initial.Entities[0] with { ProgramCounter = 1, Priority = sourcePriority, SavedProgramCounters = saved, SyncStates = sync, SyncOwners = owners, ModelBound = true }] };
            string before = JsonSerializer.Serialize(snapshot);
            FieldMachine machine = FieldMachine.Restore(program, snapshot, new RejectingHost());
            int returns = 0;
            int dispatches = 0;
            List<int> executed = [];
            machine.ScriptReturned = _ => returns++;
            machine.ScriptDispatched = _ => dispatches++;
            machine.InstructionExecuted = (_, _, pc, _) => executed.Add(pc);
            machine.AdvanceTick();
            FieldEntitySnapshot actual = machine.Capture().Entities[0];
            Check(actual.Priority == targetPriority && actual.ProgramCounter == 15 && machine.Variables.ReadByte(FieldBank.MapByte, 126) == 77, "Priority transfer did not immediately execute its exact same-entity target script.");
            Check(executed.SequenceEqual(new[] { 1, 10, 14 }) && returns == 0 && dispatches == 0 && (machine.Capture().PendingRequests ?? []).Length == 0, "Two-byte transfer executed its abandoned footer, yielded, or fabricated native dispatch/return/host events.");
            int[] expectedSaved = saved.ToArray();
            FieldSyncState[] expectedSync = sync.ToArray();
            for (int priority = sourcePriority; priority < targetPriority - 1 && priority < 7; priority++)
            {
                if (expectedSync[priority] == FieldSyncState.Waiting)
                {
                    expectedSync[priority] = FieldSyncState.Done;
                }
                expectedSaved[priority + 1] = 0;
            }
            Check(actual.SavedProgramCounters.SequenceEqual(expectedSaved) && actual.SyncStates.SequenceEqual(expectedSync) && actual.SyncOwners.SequenceEqual(owners), "Transfer changed target saved PCs, skipped sync levels or ownership outside its native unwind interval.");
            Check(actual.WaitCounter == 0 && actual.ModelBound, "Transfer changed unrelated wait or model-binding state.");
            FieldVmSnapshot coldSnapshot = JsonSerializer.Deserialize<FieldVmSnapshot>(before)!;
            FieldMachine cold = FieldMachine.Restore(program, coldSnapshot, new RejectingHost());
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(machine.Capture()), "Cold restoration changed same-entity transfer semantics.");
            Check(before == JsonSerializer.Serialize(snapshot) && code.SequenceEqual(original), "Priority transfer mutated its retained source snapshot or script bytes.");
        }
        FieldMachine truncated = new(new FieldProgram([0, 7], [new ushort[32]]));
        truncated.Initialize();
        bool rejected = false;
        try
        {
            truncated.AdvanceTick();
        }
        catch (FieldExecutionException exception)
        {
            rejected = exception.Context.ProgramCounter == 1 && exception.Context.Opcode == 7;
        }
        Check(rejected, "A missing packed transfer operand was admitted or lost its exact native fault location.");
        Console.WriteLine("Native priority transfer preserves same-entity execution, selective sync unwinding and cold-state purity.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
