using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePresentationPollingInputTests
{
    internal static void Run()
    {
        foreach (byte operation in new byte[] { 0xC8, 0xC9, 0xCA })
        {
            byte[] prefix = Audio(operation);
            var (program, machine) = Machine(Polling(prefix));
            string before = JsonSerializer.Serialize(machine.Capture());
            FieldScriptInputDemand demand = machine.ReadCurrentInputDemands().Single();
            Check(demand is { AwaitingInput: true, PresentationPolling: true, Kind: FieldScriptInputKind.Pressed, Mask: 0x20 }, "A native literal presentation no-op hid the actual KEY wait.");
            Check(before == JsonSerializer.Serialize(machine.Capture()), "Presentation polling inspection executed a live audio command or changed native state.");
            FieldVmSnapshot source = machine.Capture();
            source.Entities[0] = source.Entities[0] with { ProgramCounter = 2 + prefix.Length };
            FieldMachine atKey = FieldMachine.Restore(program, source);
            Check(atKey.ReadCurrentInputDemands().Single() is { AwaitingInput: true, PresentationPolling: true }, "A no-op on the unmatched loop path lost its presentation-polling classification.");
        }
        Check(Machine(Polling([])).Machine.ReadCurrentInputDemands().Single() is { AwaitingInput: true, PresentationPolling: false }, "Ordinary KEY polling incorrectly acquired a presentation prefix.");
        foreach (byte[] prefix in new byte[][]
        {
            Audio(0x28), Audio(0xC0), Audio(0xFF),
            [0xF1, 0, 0, 0, 0xC8],
            [0xF1, 0, 0, 0, 0xC8, 0x10, 1, .. Audio(0xC8)],
            [0x73, 0x06, 0, 0],
            [0x80, 0x50, 0, 1],
            [0x12, 0],
            [0xFF]
        })
        {
            FieldMachine machine = Machine(Polling(prefix)).Machine;
            string before = JsonSerializer.Serialize(machine.Capture());
            Check(!machine.ReadCurrentInputDemands().Any(demand => demand.AwaitingInput && demand.PresentationPolling), "An effectful, querying, writing, cyclic or unsupported prefix qualified as presentation polling.");
            Check(before == JsonSerializer.Serialize(machine.Capture()), "Rejected polling inspection changed live native state.");
        }
        for (int bank = 1; bank <= 3; bank++)
        {
            foreach (byte selector in new byte[] { 1, 0x10 })
            {
                byte[] prefix = Audio(0xC8);
                prefix[bank] = selector;
                Check(!Machine(Polling(prefix)).Machine.ReadCurrentInputDemands().Any(demand => demand.AwaitingInput && demand.PresentationPolling), "A variable AKAO selector was treated as a literal presentation no-op.");
            }
        }
        Console.WriteLine("Literal presentation polling, unmatched loop classification and source-purity checks passed.");
    }

    private static byte[] Audio(byte operation)
    {
        return [0xF2, 0, 0, 0, operation, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    }

    private static byte[] Polling(byte[] prefix)
    {
        return [.. prefix, 0x31, 0x20, 0, 2, 0, 0x12, (byte)(prefix.Length + 5)];
    }

    private static (FieldProgram Program, FieldMachine Machine) Machine(byte[] body)
    {
        FieldProgram program = new([0, 0, .. body], [new ushort[32]]);
        FieldMachine initialized = new(program);
        initialized.Initialize();
        FieldVmSnapshot source = initialized.Capture();
        source.Entities[0] = source.Entities[0] with { ProgramCounter = 2 };
        return (program, FieldMachine.Restore(program, source));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
