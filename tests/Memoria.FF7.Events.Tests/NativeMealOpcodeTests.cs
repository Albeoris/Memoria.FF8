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
        CheckHitPointSubtraction();
        Console.WriteLine("PASS native Gil query/subtraction and party HP operands, lengths and same-pass completion");
    }

    private static void CheckHitPointSubtraction()
    {
        byte[] code = [0, 0x4F, 0xB0, 1, 0xF5, 1, 0x4F, 0xF0, 0, 0, 0x80, 0x4F, 6, 0, 12, 1, 0x4F, 11, 1, 13, 0xAB, 0x4F, 8, 2, 255, 255, 0x80, 0x50, 9, 87, 0x12, 0];
        ushort[] entries = new ushort[32];
        entries[4] = 1;
        FieldProgram program = new(code, [entries]);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 2);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 11, 255);
        machine.Variables.WriteByte(FieldBank.Persistent2Byte, 13, 128);
        machine.Initialize();
        machine.Variables.WriteWord(FieldBank.MapWord, 12, -1234);
        machine.AdvanceTick();
        Check(host.Requests.Select(request => request.ProgramCounter).SequenceEqual(new[] { 1, 6, 11, 16, 21 }), "Native HP subtraction changed its five-byte width or introduced an immediate-operation yield.");
        Check(host.Requests.Select(request => request.Command).SequenceEqual(new FieldCommand[] { new FieldHitPointCommand(2, 501, true), new FieldHitPointCommand(0, short.MinValue, true), new FieldHitPointCommand(0, -1234, true), new FieldHitPointCommand(2, 128, true), new FieldHitPointCommand(255, 0, true) }), "HP subtraction changed literal script-party selection, signed immediate WORDs, low bank selector, native banked BYTE address or empty-slot decoding.");
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "A native HP subtraction prevented its same-pass successor.");
        string before = JsonSerializer.Serialize(machine.Capture());
        int calls = host.Requests.Count;
        FieldScriptAnalysis analysis = machine.AnalyzeScript(0, 4);
        Check(analysis.Complete && Enumerable.Range(9, 3).All(address => analysis.Reads.Contains(new(FieldBank.Persistent1Byte, (ushort)address, false))) && analysis.Reads.Contains(new(FieldBank.MapWord, 0x10C, true)) && analysis.Reads.Contains(new(FieldBank.Persistent2Byte, 0xAB0D, true)), "Read-only HP subtraction analysis lost script-party ownership or typed operand dependencies.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && host.Requests.Count == calls, "HP subtraction analysis changed native execution or dispatched a live host command.");
        PendingHealthHost pendingHost = new();
        FieldMachine pending = new(new([0, 0x4F, 0, 0, 0, 0x80, 0x80, 0x50, 1, 9, 0x12, 0], [new ushort[32]]), host: pendingHost);
        pending.Initialize();
        pending.AdvanceTick();
        FieldVmSnapshot snapshot = pending.Capture();
        Check(snapshot.Entities[0].ProgramCounter == 1 && snapshot.PendingRequests is [{ Command: FieldHitPointCommand { Amount: short.MinValue, Subtract: true } }], "A pending HP subtraction lost its unnegated signed amount or native command ownership.");
        FieldProgram pendingProgram = new([0, 0x4F, 0, 0, 0, 0x80, 0x80, 0x50, 1, 9, 0x12, 0], [new ushort[32]]);
        PendingHealthHost restoredHost = new() { Ready = true };
        FieldMachine restored = FieldMachine.Restore(pendingProgram, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(snapshot))!, restoredHost);
        restored.AdvanceTick();
        Check(restoredHost.Completed == 1 && restored.Variables.ReadByte(FieldBank.MapByte, 1) == 9, "Cold HP subtraction replayed its native effect or lost the first same-pass successor.");
        foreach (int length in Enumerable.Range(1, 4))
        {
            byte[] truncated = [0, .. code.AsSpan(1, length).ToArray()];
            FieldMachine broken = new(new(truncated, [new ushort[32]]), host: new Host());
            broken.Initialize();
            bool rejected = false;
            try
            {
                broken.AdvanceTick();
            }
            catch (FieldExecutionException)
            {
                rejected = true;
            }
            Check(rejected, "A truncated native HP subtraction silently consumed an incomplete operand.");
        }
        FieldCommand legacy = JsonSerializer.Deserialize<FieldCommand>("{\"command\":\"hit-point\",\"Character\":0,\"Amount\":1}")!;
        Check(legacy is FieldHitPointCommand { Subtract: false }, "An older additive HP command acquired subtraction during restoration.");
    }

    private sealed class PendingHealthHost : IFieldHost
    {
        public bool Ready { get; init; }
        public int Completed { get; private set; }

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (Ready)
            {
                Completed++;
            }
            return new(Ready);
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
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(true, request.Command is FieldGilQuery ? 0x12345678 : 0);
        }
    }
}
