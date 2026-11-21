using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeWordMultiplyTests
{
    private sealed class RejectingHost : IFieldHost
    {
        internal int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Word multiplication must not issue host work.");
        }
    }

    internal static void Run()
    {
        FieldBank[] words = [FieldBank.Persistent0Word, FieldBank.Persistent1Word, FieldBank.MapWord, FieldBank.Persistent2Word, FieldBank.Persistent3Word, FieldBank.Persistent4Word];
        foreach (FieldBank destination in words)
        {
            foreach ((short left, short right) in new (short, short)[] { (0, -1), (1, short.MinValue), (-1, short.MinValue), (short.MinValue, short.MinValue), (short.MaxValue, short.MaxValue), (300, 300), (-123, 257) })
            {
                CheckExecution(destination, FieldBank.Immediate, left, right);
                CheckExecution(destination, FieldBank.MapWord, left, right);
            }
        }
        foreach (FieldBank source in words)
        {
            CheckExecution(FieldBank.MapWord, source, -32760, -257);
        }
        foreach (int length in new[] { 1, 2, 3, 4 })
        {
            byte[] complete = [0, 0x8A, 0x60, 17, 2, 0];
            FieldMachine machine = new(new FieldProgram(complete[..(length + 1)], [new ushort[32]]));
            machine.Initialize();
            bool rejected = false;
            try
            {
                machine.AdvanceTick();
            }
            catch (FieldExecutionException exception)
            {
                rejected = exception.Context.ProgramCounter == 1 && exception.Context.Opcode == 0x8A;
            }
            Check(rejected, "A truncated word multiply lost its exact instruction fault context.");
        }
        Console.WriteLine("Native word multiplication preserves packed bank addressing, low-word overflow and five-byte nonyielding execution.");
    }

    private static void CheckExecution(FieldBank destination, FieldBank sourceBank, short left, short right)
    {
        ushort operand = sourceBank == FieldBank.Immediate ? unchecked((ushort)right) : (ushort)0xAB29;
        byte[] code = [0, 0x8A, (byte)(((byte)destination << 4) | (byte)sourceBank), 17, (byte)operand, (byte)(operand >> 8), 0x80, 0x50, 126, 77, 0x5F, 0];
        byte[] original = code.ToArray();
        FieldProgram program = new(code, [new ushort[32]]);
        RejectingHost sourceHost = new();
        FieldMachine source = new(program, host: sourceHost);
        source.Initialize();
        source.Variables.WriteWord(destination, 17, left);
        if (sourceBank != FieldBank.Immediate)
        {
            source.Variables.WriteWord(sourceBank, 41, right);
        }
        FieldVmSnapshot snapshot = source.Capture();
        string before = JsonSerializer.Serialize(snapshot);
        RejectingHost host = new();
        FieldMachine machine = FieldMachine.Restore(program, snapshot, host);
        int returns = 0;
        machine.ScriptReturned += _ => returns++;
        machine.AdvanceTick();
        Check(machine.Variables.ReadWord(destination, 17) == unchecked((short)(left * right)), "Word multiplication changed packed destination/source banks, variable low-byte addressing or low-16-bit arithmetic.");
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 126) == 77 && machine.Capture().Entities[0].ProgramCounter == 11, "Five-byte multiplication yielded or advanced into an operand instead of the next native STORE.");
        Check(host.Calls == 0 && sourceHost.Calls == 0 && returns == 0 && (machine.Capture().PendingRequests ?? []).Length == 0, "Pure arithmetic created host ownership, a synthetic return or pending work.");
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(before)!, new RejectingHost());
        cold.AdvanceTick();
        Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(machine.Capture()), "Cold restoration changed multiplication results or callback timing.");
        Check(before == JsonSerializer.Serialize(snapshot) && before == JsonSerializer.Serialize(source.Capture()) && original.SequenceEqual(code), "Multiplication mutated its retained source snapshot, machine or script.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
