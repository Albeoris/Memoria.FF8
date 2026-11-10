using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeWindowParameterTests
{
    public static void Run()
    {
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        byte[] code = [0, 0x42, 0x56, 1, 9, 0x34, 0x12, 0x80, 0x50, 9, 87, 0];
        FieldProgram program = new(code, [entries]);
        Host host = new();
        FieldMachine original = new(program, host: host);
        original.Initialize();
        original.Variables.WriteByte(FieldBank.MapByte, 9, 7);
        original.TryRequestScript(0, 1);
        original.AdvanceTick();
        FieldHostRequest request = host.Requests.Single();
        Check(request.ProgramCounter == 1 && request.Command == new FieldWindowCommand(FieldWindowOperation.Parameter, 1, Parameter: 7, ParameterBank: FieldBank.MapWord, ParameterAddress: 0x1234), "MPRA2 lost its banked byte slot, literal window or full raw ushort source address.");
        Check(original.Capture().Entities[0].ProgramCounter == 1, "A pending binding advanced its native owner PC.");
        Host coldHost = new() { Complete = true };
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
        host.Complete = true;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(original.Variables.ReadByte(FieldBank.MapByte, 9) == 87 && JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "MPRA2 must complete six bytes and execute its successor in the same pass, including cold pending ownership.");
        FieldVariables variables = new();
        variables.WriteWord(FieldBank.Persistent1Word, 12, -2);
        Check(variables.ReadWindowParameter(FieldBank.Persistent0Word, 0x10C) == 65534, "A numeric binding's 16-bit address must not be silently truncated to a field operand byte.");
        Check(variables.ReadWindowParameter(FieldBank.Immediate, 65535) == 65535 && variables.ReadWindowParameter((FieldBank)8, 42) == 0, "Numeric immediate/default selectors must preserve native unsigned word behavior.");
        FieldMachine immediate = new(program, host: new Host { Complete = true });
        immediate.Initialize();
        immediate.Variables.WriteByte(FieldBank.MapByte, 9, 7);
        immediate.TryRequestScript(0, 1);
        immediate.AdvanceTick();
        Check(immediate.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "An immediate native binding must not insert an extra callback before its successor.");
        for (int length = 2; length < 7; length++)
        {
            Host truncatedHost = new() { Complete = true };
            FieldMachine truncated = new(new FieldProgram(code[..length], [entries]), host: truncatedHost);
            truncated.Initialize();
            truncated.TryRequestScript(0, 1);
            bool rejected = false;
            try
            {
                truncated.AdvanceTick();
            }
            catch (FieldExecutionException error) when (error.InnerException is InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected && truncatedHost.Requests.Count == 0, "A truncated MPRA2 operand must fail before publishing a partial binding.");
        }
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
        public bool Complete { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];
        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(Complete);
        }
    }
}
