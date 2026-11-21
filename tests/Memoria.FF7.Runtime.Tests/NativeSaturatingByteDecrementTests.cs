using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeSaturatingByteDecrementTests
{
    private sealed class RejectingHost : IFieldHost
    {
        internal int Calls { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Pure byte decrement must not issue a host request.");
        }
    }

    internal static void Run()
    {
        foreach (FieldBank bank in new[] { FieldBank.Persistent0Byte, FieldBank.Persistent1Byte, FieldBank.MapByte, FieldBank.Persistent2Byte, FieldBank.Persistent3Byte, FieldBank.Persistent4Byte })
        {
            foreach (byte value in new byte[] { 0, 1, 128, 255 })
            {
                foreach (byte high in new byte[] { 0, 0xF0 })
                {
                    byte packed = (byte)(high | (byte)bank);
                    byte[] code = [0, 0x7C, packed, 17, 0x80, 0x50, 126, 77, 0x5F, 0];
                    byte[] original = code.ToArray();
                    FieldProgram program = new(code, [new ushort[32]]);
                    RejectingHost sourceHost = new();
                    FieldMachine source = new(program, host: sourceHost);
                    source.Initialize();
                    source.Variables.WriteByte(bank, 17, value);
                    FieldVmSnapshot snapshot = source.Capture();
                    string before = JsonSerializer.Serialize(snapshot);
                    RejectingHost host = new();
                    FieldMachine machine = FieldMachine.Restore(program, snapshot, host);
                    machine.AdvanceTick();
                    Check(machine.Variables.ReadByte(bank, 17) == Math.Max(0, value - 1), "The native unsigned byte decrement wrapped, selected another bank or changed its address.");
                    Check(machine.Variables.ReadByte(FieldBank.MapByte, 126) == 77 && machine.Capture().Entities[0].ProgramCounter == 9, "Three-byte decrement yielded before the next native STORE or advanced the wrong instruction width.");
                    Check(host.Calls == 0 && sourceHost.Calls == 0 && (machine.Capture().PendingRequests ?? []).Length == 0, "Pure decrement created host or presentation debt.");
                    FieldVmSnapshot coldSnapshot = JsonSerializer.Deserialize<FieldVmSnapshot>(before)!;
                    FieldMachine cold = FieldMachine.Restore(program, coldSnapshot, new RejectingHost());
                    cold.AdvanceTick();
                    Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(machine.Capture()), "Cold restoration changed native decrement timing or state.");
                    Check(before == JsonSerializer.Serialize(snapshot) && before == JsonSerializer.Serialize(source.Capture()) && code.SequenceEqual(original), "Decrement mutated the retained source VM, snapshot or script bytes.");
                }
            }
        }
        CheckWrappingCounterparts();
        CheckTruncation();
        Console.WriteLine("Native 7C saturates unsigned byte banks without yielding or issuing host work.");
    }

    private static void CheckWrappingCounterparts()
    {
        foreach ((byte opcode, byte initial, byte expected) in new[] { ((byte)0x7C, (byte)0, (byte)0), ((byte)0x97, (byte)0, (byte)255), ((byte)0x95, (byte)255, (byte)0) })
        {
            FieldMachine machine = new(new FieldProgram([0, opcode, 5, 17, 0x5F, 0], [new ushort[32]]));
            machine.Initialize();
            machine.Variables.WriteByte(FieldBank.MapByte, 17, initial);
            machine.AdvanceTick();
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 17) == expected, "Saturating 7C was conflated with the existing wrapping byte counters.");
        }
    }

    private static void CheckTruncation()
    {
        FieldMachine machine = new(new FieldProgram([0, 0x7C, 5], [new ushort[32]]));
        machine.Initialize();
        bool rejected = false;
        try
        {
            machine.AdvanceTick();
        }
        catch (FieldExecutionException exception)
        {
            rejected = exception.Context.ProgramCounter == 1 && exception.Context.Opcode == 0x7C;
        }
        Check(rejected, "A truncated decrement address was admitted or lost its exact native fault context.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
