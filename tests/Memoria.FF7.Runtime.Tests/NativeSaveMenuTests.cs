using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSaveMenuTests
{
    internal static void Run()
    {
        Ff7FieldResources field = new("save-menu-fixture", null!, null!, null!, [], [], null!, null!, null!);
        FieldProgram program = new([0, 0x49, 0, 14, 0, 0x80, 0x50, 4, 77, 0x5F, 0x12, 0], [new ushort[32]]);
        Ff7FieldHost host = new(field, (_, _) => 1);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        FieldVmSnapshot pending = machine.Capture();
        FieldHostRequest owner = pending.PendingRequests!.Single();
        Check(owner.Entity == 0 && owner.Priority == 7 && owner.ProgramCounter == 1 && owner.Command == new FieldMenuCommand(14, 0) && host.Process.Menu is { Completed: false, SaveOutcome: Ff7SaveMenuOutcome.Open } && host.Process.GlobalRequestId == owner.Id && pending.Entities[0].ProgramCounter == 1, "Save MENU14 did not retain its real native owner/PC and interactive pending process.");
        string before = JsonSerializer.Serialize(host.Capture());
        Reject(() => host.CancelSaveMenu(owner.Id + 1));
        Reject(() => host.CompleteMenu(owner.Id));
        Check(before == JsonSerializer.Serialize(host.Capture()), "A foreign request or generic completion mutated the pending save menu.");
        Ff7FieldProcess saved = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        Ff7FieldHost coldHost = new(field, (_, _) => 1, saved);
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(pending))!, coldHost);
        host.CancelSaveMenu(owner.Id);
        coldHost.CancelSaveMenu(owner.Id);
        Check(host.Process.Menu is { Completed: true, SaveOutcome: Ff7SaveMenuOutcome.Cancelled } && machine.Capture().Entities[0].ProgramCounter == 1, "Cancel acknowledgement skipped the suspended native MENU opcode.");
        machine.AdvanceTick();
        cold.AdvanceTick();
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 4) == 77 && machine.Capture().Entities[0].ProgramCounter == 10 && (machine.Capture().PendingRequests?.Length ?? 0) == 0 && host.Process.GlobalCommand == Ff7GlobalCommand.None, "Native MENU49 did not acknowledge the same cancelled owner and advance exactly four bytes before its same-pass continuation.");
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()) && JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(coldHost.Capture()), "Cold save-menu cancellation changed native process, PC, ownership or timing.");
        Ff7FieldHost legacy = new(field, (_, _) => 1, saved with { Menu = saved.Menu! with { SaveOutcome = null } });
        legacy.CancelSaveMenu(owner.Id);
        Ff7FieldHost completed = new(field, (_, _) => 1, saved with { Menu = saved.Menu! with { SaveOutcome = null, Completed = true } });
        Check(completed.Execute(owner).Completed && completed.Process.GlobalCommand == Ff7GlobalCommand.None, "A legacy completed menu was rejected solely for lacking a new optional cancellation receipt.");
        Console.WriteLine("PASS native save menu pending owner, explicit cancellation, exact PC continuation, cold identity and legacy defaults");
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("A foreign or unacknowledged save-menu completion was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
