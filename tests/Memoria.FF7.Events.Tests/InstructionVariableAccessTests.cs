using System.Text.Json;
using Memoria.FF7.Events;

internal static class InstructionVariableAccessTests
{
    public static void Run()
    {
        byte[] code = [0, 0x80, 0x11, 24, 16, 0x81, 0x22, 26, 15, 0, 0x10, 5, 0x80, 0x11, 28, 22, 0];
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram program = new(code, [entries]);
        FieldMachine machine = new(program);
        machine.Initialize();
        machine.Variables.WriteByte(FieldBank.Persistent0Byte, 16, 7);
        FieldVmSnapshot before = machine.Capture();
        List<(FieldVariableRead Access, bool Write)> accesses = [];
        void Observe(FieldVariableRead access, bool write)
        {
            accesses.Add((access, write));
        }
        machine.InstructionVariableAccessed += Observe;
        machine.TryRequestScript(0, 1);
        machine.AdvanceTick();
        Check(accesses.Contains((new(FieldBank.Persistent0Byte, 16, false), false)), "The observer lost an executed byte read.");
        Check(accesses.Contains((new(FieldBank.Persistent0Word, 15, true), false)), "The observer lost a word operand aliasing the clock high byte.");
        Check(accesses.Contains((new(FieldBank.Persistent0Byte, 24, false), true)) && accesses.Contains((new(FieldBank.Persistent0Word, 26, true), true)), "The observer lost executed byte/word destinations.");
        Check(!accesses.Any(access => access.Access.Operand == 22), "The observer reported a skipped branch operand.");
        int count = accesses.Count;
        machine.Variables.ReadByte(FieldBank.Persistent0Byte, 16);
        machine.Variables.WriteByte(FieldBank.Persistent0Byte, 24, 7);
        Check(accesses.Count == count, "The observer leaked outside native instruction execution.");
        machine.InstructionVariableAccessed -= Observe;
        machine.TryRequestScript(0, 1);
        machine.AdvanceTick();
        Check(accesses.Count == count, "A removed observer still received operands.");
        FieldMachine plain = FieldMachine.Restore(program, before);
        plain.TryRequestScript(0, 1);
        plain.AdvanceTick();
        FieldMachine observed = FieldMachine.Restore(program, before);
        observed.InstructionVariableAccessed += (_, _) =>
        {
        };
        observed.TryRequestScript(0, 1);
        observed.AdvanceTick();
        Check(JsonSerializer.Serialize(plain.Capture()) == JsonSerializer.Serialize(observed.Capture()), "Operand observation changed native execution.");
        Console.WriteLine("PASS executed operand observation, byte/word aliases, skipped branches and unchanged native execution");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
