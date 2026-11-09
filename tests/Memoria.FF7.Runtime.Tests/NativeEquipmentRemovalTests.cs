using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEquipmentRemovalTests
{
    public static void Run()
    {
        byte[] record = Record();
        record[30] = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), 0x12345601);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(124), 0xABCDEF2C);
        Ff7Progress original = Progress(record);
        byte[] types = new byte[255];
        byte[] classes = new byte[16];
        types[1] = 3;
        classes[3] = 10;
        Ff7EquipmentRemovalResult result = Ff7NativeEquipmentRemoval.Apply(original, 1, types, classes);
        Check(result.MateriaFlags == 3, "Returned materia must publish the native acquisition flags.");
        Check(result.Progress.Materia.SequenceEqual(new[] { new Ff7MateriaState(1, 0x123456), new Ff7MateriaState(44, 0xABCDEF) }), "Weapon/armor materia order or AP changed.");
        Check(result.Progress.Inventory.Single() == new Ff7InventorySlot(292, 1), "Accessory must return as one native inventory item.");
        byte[] expectedRecord = record.ToArray();
        expectedRecord.AsSpan(64, 64).Fill(255);
        expectedRecord[30] = 255;
        Check(result.Progress.Characters[0].NativeRecord.SequenceEqual(expectedRecord), "Removal changed other character bytes.");
        Check(ReferenceEquals(original.Characters[1], result.Progress.Characters[1]), "Another character was changed.");
        Check(original.Characters[0].NativeRecord.SequenceEqual(record), "The prior progress snapshot was mutated.");
        Ff7Progress full = original with { Materia = Enumerable.Repeat(new Ff7MateriaState(2, 0), 200).ToArray(), Inventory = Enumerable.Range(0, 320).Select(id => new Ff7InventorySlot(id, 99)).ToArray() };
        Ff7EquipmentRemovalResult fullResult = Ff7NativeEquipmentRemoval.Apply(full, 1, types, classes);
        Check(fullResult.Progress.Materia.Length == 200 && fullResult.MateriaFlags == 0, "A full materia inventory must discard the offered entries without setting acquisition flags.");
        Check(fullResult.Progress.Characters[0].NativeRecord.AsSpan(64, 64).ToArray().All(value => value == 255) && fullResult.Progress.Characters[0].NativeRecord[30] == 255, "Full inventories do not prevent equipment clearing.");
        Check(fullResult.Progress.Inventory.Single(item => item.ItemId == 292).Quantity == 99, "Returned accessory stack must use native saturation.");
        byte[] unmatchedRecord = record.ToArray();
        unmatchedRecord[30] = 50;
        Ff7Progress unmatched = full with { Characters = [full.Characters[0] with { NativeRecord = unmatchedRecord }, full.Characters[1]] };
        Ff7Progress discarded = Ff7NativeEquipmentRemoval.Apply(unmatched, 1, types, classes).Progress;
        Check(discarded.Inventory.SequenceEqual(full.Inventory) && discarded.Characters[0].NativeRecord[30] == 255, "An unmatched accessory must be lost when native item slots are full.");
        Check(JsonSerializer.Serialize(result.Progress) == JsonSerializer.Serialize(Ff7NativeEquipmentRemoval.Apply(result.Progress, 1, types, classes).Progress), "Empty equipment removal must be idempotent.");
        MenuLifecycle(original, types, classes);
        InstalledContinuation();
    }

    private static void MenuLifecycle(Ff7Progress original, byte[] types, byte[] classes)
    {
        Ff7FieldHost host = Build();
        Ff7Progress progress = original;
        int applications = 0;
        FieldHostReply Apply(FieldCommand command)
        {
            applications++;
            progress = Ff7NativeEquipmentRemoval.Apply(progress, ((FieldMenuCommand)command).Parameter, types, classes).Progress;
            return new(true);
        }
        host.ExternalCommand = Apply;
        var request = new FieldHostRequest(123, 0, 6, 4, new FieldMenuCommand(17, 1));
        Check(!host.Execute(request).Completed && applications == 0, "MENU must first queue and yield before executing the native global process.");
        Check(host.Process.GlobalCommand == Ff7GlobalCommand.Menu && host.Process.Menu is { Completed: false }, "Queued MENU owner was not saved.");
        Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.ExternalCommand = Apply;
        Check(!cold.Execute(request).Completed && applications == 0, "Polling a cold pending MENU must not execute its global process.");
        cold.AdvanceNativeMenu();
        cold.AdvanceNativeMenu();
        Check(applications == 1 && cold.Execute(request).Completed, "Completed tail service must execute the queued operation exactly once.");
        Check(cold.Process.GlobalCommand == Ff7GlobalCommand.None && cold.Process.Menu is { Completed: true }, "Completed MENU failed to release the global owner.");
        Ff7FieldHost completed = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(cold.Capture()))!);
        completed.ExternalCommand = _ => throw new Exception("A completed MENU receipt repeated its side effects.");
        Check(completed.Execute(request).Completed && applications == 1, "Completed cold MENU receipt changed.");

        byte[] code = new byte[64];
        new byte[] { 0, 0x49, 0xA5, 17, 9, 0x80, 0x50, 10, 7, 0x5F, 0x12, 1 }.CopyTo(code, 16);
        FieldProgram program = new(code, [Enumerable.Repeat((ushort)16, 32).ToArray()]);
        Ff7FieldHost operandHost = Build();
        operandHost.ExternalCommand = command =>
        {
            Check(command == new FieldMenuCommand(17, 255), "MENU parameter must read selector2 as a BYTE; selector1 is not an operand.");
            return new(true);
        };
        FieldMachine machine = new(program, host: operandHost);
        machine.Initialize();
        machine.Variables.WriteByte(FieldBank.MapByte, 9, 255);
        machine.AdvanceTick();
        Check(machine.Capture().Entities[0].ProgramCounter == 17 && machine.Variables.ReadByte(FieldBank.MapByte, 10) == 0, "First MENU call must retain PC and yield.");
        machine = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, operandHost);
        operandHost.AdvanceNativeMenu();
        machine.AdvanceTick();
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 10) == 7, "Completed MENU must advance four bytes without another yield.");
    }

    private static void InstalledContinuation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_EQUIPMENT_REMOVAL_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed equipment removal: original resources or external standalone fixture are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7SessionSave before = session.Capture();
        FieldHostRequest request = (before.Vm.PendingRequests ?? []).Single(item => item.Command is FieldMenuCommand { Mode: 17 });
        Check(before.Vm.CycleCursor is not null, "The external fixture must retain the actual failing MENU cycle cursor.");
        Check(!session.Host.Execute(request).Completed, "Actual partial MENU retry must queue its previously uncreated global owner.");
        Check(JsonSerializer.Serialize(before.Progress) == JsonSerializer.Serialize(session.Progress), "Queueing MENU changed progression before service.");
        using Ff7Session queued = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Check(!queued.Host.Execute(request).Completed, "Actual cold queued MENU poll must not perform service.");
        queued.Host.AdvanceNativeMenu();
        Check(queued.Host.Execute(request).Completed, "Actual cold queued MENU did not complete after its tail service.");
        string progress = JsonSerializer.Serialize(queued.Progress);
        Check(queued.Host.Execute(request).Completed && JsonSerializer.Serialize(queued.Progress) == progress, "Actual completed MENU receipt applied twice.");
        Check(JsonSerializer.Serialize(before.Vm) == JsonSerializer.Serialize(queued.Capture().Vm), "Host-only MENU verification changed native VM facts.");
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(queued.Capture()))!);
        Check(cold.Host.Execute(request).Completed && JsonSerializer.Serialize(cold.Progress) == progress, "Actual cold completed MENU repeated removal.");
        cold.AdvanceTick(false);
        Check(cold.Capture().Vm.CycleCursor is null, "Actual partial native cycle failed to finish after MENU completion.");
        Check(cold.Host.Process.Menu is { Mode: 17, Parameter: 2, Completed: true }, "The next authored MENU17(2) must queue and complete at the same field tail.");
        cold.AdvanceTick(false);
        Check(cold.Host.Process.GlobalCommand != Ff7GlobalCommand.Menu, "The subsequent MENU17(2) completion did not release its global owner.");
    }

    private static byte[] Record()
    {
        byte[] record = new byte[132];
        record.AsSpan(64, 64).Fill(255);
        record[30] = 255;
        return record;
    }

    private static Ff7Progress Progress(byte[] record)
    {
        return new() { InitialKernelData = [1], Characters = [new(1, "A", 1, 0, 1, 1, record), new(2, "B", 1, 0, 1, 1, Record())], Party = [1, 2, 255], Inventory = [], Materia = [] };
    }

    private static Ff7FieldHost Build(Ff7FieldProcess? process = null)
    {
        var field = new Ff7FieldResources("equipment-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process ?? new() { BlinkStateInitialized = true });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
