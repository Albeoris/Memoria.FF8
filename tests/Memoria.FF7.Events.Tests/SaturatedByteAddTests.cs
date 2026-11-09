using System.Text.Json;
using Memoria.FF7.Events;

internal static class SaturatedByteAddTests
{
    public static void Run()
    {
        RunCases(0x76, [(0, 0, 0), (127, 1, 128), (254, 1, 255), (250, 20, 255), (255, 255, 255)], 2);
    }

    public static void RunSubtract()
    {
        RunCases(0x78, [(0, 0, 0), (0, 255, 0), (1, 2, 0), (128, 127, 1), (255, 128, 127), (255, 255, 0), (255, 0, 255)], 6);
    }

    private static void RunCases(byte opcode, (int Left, int Right, int Expected)[] cases, int handlersCompleted)
    {
        foreach (var (left, right, expected) in cases)
        {
            FieldProgram program = Program([0, opcode, 0x10, 4, (byte)right, 0x80, 0x50, 9, 87, 0x5F, 0x12, 0]);
            var machine = new FieldMachine(program);
            machine.Initialize();
            machine.Variables.WriteWord(FieldBank.Persistent0Word, 4, unchecked((short)(0xAB00 | left)));
            FieldVmSnapshot snapshot = machine.Capture();
            // The reached command can resume after earlier handlers in the same saved cycle.
            snapshot = snapshot with { CycleCursor = new(0, handlersCompleted, 1) };
            var cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(snapshot))!);
            cold.AdvanceTick();
            Equal((byte)expected, cold.Variables.ReadByte(FieldBank.Persistent0Byte, 4));
            Equal((byte)0xAB, cold.Variables.ReadByte(FieldBank.Persistent0Byte, 5));
            Equal((byte)87, cold.Variables.ReadByte(FieldBank.MapByte, 9));
            Equal(handlersCompleted == 6 ? 9 : 10, cold.Capture().Entities[0].ProgramCounter);
            Equal(1L, cold.Tick);
            Equal<FieldCycleCursor?>(null, cold.CycleCursor);
        }

        FieldProgram banked = Program([0, opcode, 0x65, 4, 7, 0x5F, 0x12, 0]);
        var aliases = new FieldMachine(banked);
        aliases.Initialize();
        aliases.Variables.WriteWord(FieldBank.MapWord, 4, unchecked((short)0x9CF0));
        aliases.Variables.WriteByte(FieldBank.MapByte, 7, 32);
        aliases.AdvanceTick();
        Equal(unchecked((short)(opcode == 0x76 ? 0x9CFF : 0x9CD0)), aliases.Variables.ReadWord(FieldBank.MapWord, 4));
        Equal((byte)32, aliases.Variables.ReadByte(FieldBank.MapByte, 7));

        var body = new List<byte> { 0 };
        for (int index = 0; index < 9; index++)
        {
            body.AddRange([opcode, 0x50, 0, 1]);
        }
        body.AddRange([0x12, 0]);
        var budget = new FieldMachine(Program(body.ToArray()));
        budget.Initialize();
        if (opcode == 0x78)
        {
            budget.Variables.WriteByte(FieldBank.MapByte, 0, 9);
        }
        budget.AdvanceTick();
        Equal((byte)(opcode == 0x76 ? 8 : 1), budget.Variables.ReadByte(FieldBank.MapByte, 0));
        Equal(33, budget.Capture().Entities[0].ProgramCounter);
    }

    private static FieldProgram Program(byte[] code)
    {
        return new(code, [Enumerable.Repeat((ushort)0, 32).ToArray()]);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
