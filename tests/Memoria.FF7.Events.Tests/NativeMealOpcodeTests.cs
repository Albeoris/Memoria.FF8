using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeMealOpcodeTests
{
    public static void Run()
    {
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0, 0x3B, 0x66, 0, 2, 0x3A, 0x66, 0, 0, 2, 0, 0x3A, 0, 255, 255, 255, 255, 0x4D, 0, 1, 0x10, 0x27, 0x80, 0x50, 9, 87, 0x12, 0], [entries]);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 2);
        machine.Initialize();
        machine.AdvanceTick();
        Check(machine.Variables.ReadWord(FieldBank.MapWord, 0) == 0x5678 && machine.Variables.ReadWord(FieldBank.MapWord, 2) == 0x1234, "CHGLD must split the full unsigned Gil value into two bank-selected words.");
        Check(host.Requests.Select(request => request.ProgramCounter).SequenceEqual(new[] { 1, 5, 11, 17 }), "Meal commands lost their native lengths or introduced a yield.");
        Check(host.Requests[1].Command == new FieldGilCommand(0x12345678, Subtract: true) && host.Requests[2].Command == new FieldGilCommand(uint.MaxValue, Subtract: true) && host.Requests[3].Command == new FieldHitPointCommand(2, 10000), "Meal operands must preserve unsigned banked/immediate Gil and signed HP amounts plus script-party identity.");
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "The instruction after native meal commands must run in the same pass.");
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, new Host());
        Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(machine.Capture()), "A cold meal command continuation changed its completed native state.");
        Console.WriteLine("PASS native Gil query/subtraction and party HP operands, lengths and same-pass completion");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Host : IFieldHost
    {
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(true, request.Command is FieldGilQuery ? 0x12345678 : 0);
        }
    }
}
