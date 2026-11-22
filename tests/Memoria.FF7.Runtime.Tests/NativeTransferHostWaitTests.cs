using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeTransferHostWaitTests
{
    private sealed class WaitingHost : IFieldHost
    {
        internal List<FieldHostRequest> Calls { get; } = [];
        internal List<FieldHostRequest> Cancelled { get; } = [];
        internal HashSet<long> Active { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls.Add(request);
            Active.Add(request.Id);
            return new(false);
        }

        public void Cancel(FieldHostRequest request)
        {
            Cancelled.Add(request);
            Active.Remove(request.Id);
        }
    }

    internal static void Run()
    {
        CheckTransfer(7, sameEntry: false);
        CheckTransfer(3, sameEntry: false);
        CheckTransfer(7, sameEntry: true);
        Console.WriteLine("Native priority transfer retires only orphan VM host waits while preserving real suspended work and host activity.");
    }

    private static void CheckTransfer(int targetPriority, bool sameEntry)
    {
        byte[] code = [0, 0xB4, 0, 64, 2, 10, 1, 7, (byte)(targetPriority << 5 | 3), 0x12, 0];
        byte[] original = code.ToArray();
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[0][1] = 7;
        entries[0][3] = sameEntry ? (ushort)1 : (ushort)9;
        FieldProgram program = new(code, entries);
        FieldMachine initialized = new(program);
        initialized.Initialize();
        FieldVmSnapshot initial = initialized.Capture();
        initial.Entities[0] = initial.Entities[0] with { ModelBound = true };
        initial.Entities[1] = initial.Entities[1] with { ModelBound = true };
        WaitingHost host = new();
        FieldMachine source = FieldMachine.Restore(program, initial, host);
        source.AdvanceTick();
        FieldHostRequest originalWait = source.Capture().PendingRequests!.Single(request => request.Entity == 0);
        FieldHostRequest otherWait = source.Capture().PendingRequests!.Single(request => request.Entity == 1);
        Check(originalWait is { Priority: 7, ProgramCounter: 1, Command: FieldActorTurn }, "The fixture did not execute a genuine native ambient TURN request.");
        Check(source.RequestExternalScript(0, 1, 5) is { Accepted: true, Started: true }, "The real P5 preemption did not start.");
        FieldVmSnapshot before = source.Capture();
        string beforeJson = JsonSerializer.Serialize(before);
        FieldMachine machine = FieldMachine.Restore(program, before, host);
        int returned = 0;
        int dispatched = 0;
        machine.ScriptReturned = _ => returned++;
        machine.ScriptDispatched = _ => dispatched++;
        int priorCalls = host.Calls.Count;
        machine.AdvanceTick();
        FieldVmSnapshot after = machine.Capture();
        FieldHostRequest[] pending = after.PendingRequests ?? [];
        bool retained = targetPriority < 7 || sameEntry;
        Check(pending.Contains(originalWait) == retained && pending.Contains(otherWait), "Transfer retired actual suspended/current work or retained an orphan equal-priority wait.");
        Check(after.Entities[0].Priority == targetPriority && after.Entities[0].ProgramCounter == (sameEntry ? 1 : 9) && after.Entities[0].SavedProgramCounters[7] == 1, "VM wait retirement changed native target priority, PC or preserved saved-slot residue.");
        Check(returned == 0 && dispatched == 0 && host.Cancelled.Count == 0 && host.Active.Contains(originalWait.Id), "VM wait retirement cancelled or completed native actor work or fabricated return/dispatch events.");
        Check(host.Calls.Skip(priorCalls).All(request => request.Entity == 1 || sameEntry && request == originalWait), "Transfer polled or restarted an abandoned native host request.");
        FieldVmSnapshot cold = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(after))!;
        Check(JsonSerializer.Serialize(FieldMachine.Restore(program, cold, new WaitingHost()).Capture()) == JsonSerializer.Serialize(after), "Cold restoration changed valid post-transfer pending ownership.");
        Check(beforeJson == JsonSerializer.Serialize(before) && beforeJson == JsonSerializer.Serialize(source.Capture()) && code.SequenceEqual(original), "Transfer changed a retained source VM, snapshot or instruction bytes.");
        if (!retained)
        {
            ExpectCorrupt(program, after with { PendingRequests = [originalWait, otherWait] });
            FieldEntitySnapshot[] invalidSlots = after.Entities.Select(entity => entity with { SavedProgramCounters = entity.SavedProgramCounters.ToArray() }).ToArray();
            invalidSlots[0].SavedProgramCounters[4] = 1;
            ExpectCorrupt(program, after with { Entities = invalidSlots, PendingRequests = [originalWait with { Priority = 4 }, otherWait] });
        }
    }

    private static void ExpectCorrupt(FieldProgram program, FieldVmSnapshot snapshot)
    {
        bool rejected = false;
        try
        {
            FieldMachine.Restore(program, snapshot, new WaitingHost());
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        if (!rejected)
        {
            throw new InvalidOperationException("Restore silently pruned or resurrected an orphan native host request.");
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
