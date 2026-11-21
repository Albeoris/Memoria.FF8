using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeEntryInputBranchTests
{
    private static byte[] Key => [0x31, 0x20, 0x02, 4, 0x01, 0, 1, 0];

    internal static void Run()
    {
        foreach (byte opcode in new byte[] { 0x14, 0x15, 0x16, 0x17, 0x18, 0x19 })
        {
            FieldMachine machine = Machine(Branch(opcode, Key, Key));
            foreach (byte value in new byte[] { 0, 1 })
            {
                machine.Variables.WriteByte(FieldBank.MapByte, 0, value);
                string before = JsonSerializer.Serialize(machine.Capture());
                Check(machine.CanSkipScriptEntryForInput(1, 1, new()), "Both mutable conditional arms return under the exact neutral input, but the entry was not skipped.");
                Check(!machine.CanSkipScriptEntryForInput(1, 1, new(Pressed: 0x220, RawPressed: 0x220)), "A true button branch that requests a child was treated as no effect.");
                Check(before == JsonSerializer.Serialize(machine.Capture()), "Entry branch inspection changed native variables, input or execution.");
            }
        }
        foreach (byte[] effect in new byte[][]
        {
            [0x80, 0x50, 1, 1, 0],
            [0x01, 0, 1, 0],
            [0x33, 0, 0],
            [0x3B, 0x66, 1, 2, 0],
            [0x24, 0, 0, 0],
            [0x12, 0],
            [0xFF]
        })
        {
            foreach (bool reverse in new[] { false, true })
            {
                FieldMachine machine = Machine(reverse ? Branch(0x14, effect, [0]) : Branch(0x14, [0], effect));
                machine.Variables.WriteByte(FieldBank.MapByte, 0, reverse ? (byte)1 : (byte)0);
                Check(!machine.CanSkipScriptEntryForInput(1, 1, new()), "The currently unselected variable branch hid a write, request, host effect, wait, cycle or unsupported instruction.");
            }
        }
        Check(!Machine([0x14, 0x50, 0]).CanSkipScriptEntryForInput(1, 1, new()), "A truncated native conditional received a return proof.");
        Check(!Machine(Branch(0x14, Key, Key)).CanSkipScriptEntryForInput(1, 1, new(), instructionBudget: 1), "An exhausted analysis allowance was mistaken for a return proof.");
        CheckConstructiveDemand();
        Console.WriteLine("Native entry-input variable branches require both exact-input arms to return without effects.");
    }

    private static void CheckConstructiveDemand()
    {
        byte[] first = [0x31, 0x20, 0, 14, 0x16, 0x20, 0, 0, 0x9B, 2, 0, 2, 0, 0x01, 0, 1, 0];
        FieldMachine machine = Machine([.. first, 0x31, 0x80, 0, 4, 0x01, 0, 1, 0]);
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 667);
        string before = JsonSerializer.Serialize(machine.Capture());
        Check(machine.ReadEntryInputDemand(1, 1) is { Mask: 0x80, ProgramCounter: 19 }, "A currently returning first button arm hid the next constructive native gate.");
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Constructive input inspection changed story or execution state.");
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 668);
        before = JsonSerializer.Serialize(machine.Capture());
        Check(machine.ReadEntryInputDemand(1, 1) is { Mask: 0x20, ProgramCounter: 2 }, "A changed story condition did not restore the now-effectful first gate.");
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Constructive input refresh changed native state.");
        FieldMachine noEffect = Machine([0x31, 0x20, 0, 2, 0, 0x31, 0x80, 0, 1, 0]);
        Check(noEffect.ReadEntryInputDemand(1, 1) is null, "An all-returning button chain invented a constructive input demand.");
    }

    private static byte[] Branch(byte opcode, byte[] first, byte[] second)
    {
        bool word = opcode >= 0x16;
        bool wide = (opcode & 1) != 0;
        byte[] prefix = word ? [opcode, 0x60, 0, 0, 0, 0, 0] : [opcode, 0x50, 0, 0, 0];
        int distance = first.Length + (wide ? 2 : 1);
        return wide ? [.. prefix, (byte)distance, 0, .. first, .. second] : [.. prefix, (byte)distance, .. first, .. second];
    }

    private static FieldMachine Machine(byte[] body)
    {
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][1] = 2;
        FieldMachine machine = new(new FieldProgram([0, 0, .. body], entries));
        machine.Initialize();
        return machine;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
