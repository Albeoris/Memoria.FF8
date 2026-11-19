using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeTrigonometryOpcodeTests
{
    internal static void Run()
    {
        foreach (var vector in new (short Angle, short Amplitude, short Additive, int Sin, int Cos)[]
        {
            (511, 168, 5, 118, 118),
            (-511, -32768, -32768, 23120, -23208),
            (32767, 32767, 32767, -40, 32767),
            (-32768, 32767, -32768, -8, 32759),
            (1, 32767, 0, 47, 32759),
            (1024, 32767, 32767, 32774, 7),
            (0, 1, -1, -1, 0)
        })
        {
            foreach (byte opcode in new byte[] { 0xD4, 0xD5 })
            {
                byte[] code = [opcode, 0, 6, unchecked((byte)vector.Angle), unchecked((byte)(vector.Angle >> 8)), unchecked((byte)vector.Amplitude), unchecked((byte)(vector.Amplitude >> 8)), unchecked((byte)vector.Additive), unchecked((byte)(vector.Additive >> 8)), 80, 0x80, 0x50, 82, 77, 0];
                FieldProgram program = new([0, ..code, 0x12, 0], [new ushort[32]]);
                FieldMachine machine = new(program);
                machine.Initialize();
                machine.AdvanceTick();
                short expected = unchecked((short)(opcode == 0xD4 ? vector.Sin : vector.Cos));
                Check(machine.Variables.ReadWord(FieldBank.MapWord, 80) == expected && machine.Variables.ReadByte(FieldBank.MapByte, 82) == 77, "Native SIN/COS lost signed inputs, native truncation, additive-before-shift, output word wrapping or ten-byte same-pass continuation.");
                Check((machine.Capture().PendingRequests?.Length ?? 0) == 0, "Pure native trigonometry fabricated a host request or blocking lifetime.");
            }
        }
        FieldProgram banked = new([0, 0xD4, 0x62, 0x46, 11, 0xA5, 21, 0xB6, 31, 0xC7, 80, 0x80, 0x50, 82, 77, 0x12, 0], [new ushort[32]]);
        FieldMachine original = new(banked);
        original.Initialize();
        original.Variables.WriteWord(FieldBank.MapWord, 11, -511);
        original.Variables.WriteWord(FieldBank.Persistent0Word, 21, -32768);
        original.Variables.WriteWord(FieldBank.Persistent1Word, 31, -32768);
        List<(FieldVariableRead Access, bool Write)> accesses = [];
        original.InstructionVariableAccessed += (access, write) => accesses.Add((access, write));
        FieldVmSnapshot saved = original.Capture();
        FieldMachine cold = FieldMachine.Restore(banked, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(saved))!);
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(accesses.Contains((new(FieldBank.MapWord, 0xA50B, true), false)) && accesses.Contains((new(FieldBank.Persistent0Word, 0xB615, true), false)) && accesses.Contains((new(FieldBank.Persistent1Word, 0xC71F, true), false)) && accesses.Contains((new(FieldBank.MapWord, 80, true), true)), "Trigonometry omitted actual executed word dependencies or its destination write.");
        Check(original.Variables.ReadWord(FieldBank.MapWord, 80) == 23120 && original.Variables.ReadByte(FieldBank.MapByte, 82) == 77, "Native SIN bank nibbles, signed word addressing or output byte address changed.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold trigonometry continuation changed VM ownership, progress, PC or native timing.");
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram analyzedProgram = new([0, 0xD4, 0x22, 0x26, 11, 0, 21, 0, 31, 0, 80, 0], [entries]);
        FieldMachine analyzed = new(analyzedProgram);
        analyzed.Initialize();
        string beforeAnalysis = JsonSerializer.Serialize(analyzed.Capture());
        FieldScriptAnalysis analysis = analyzed.AnalyzeScript(0, 1);
        Check(analysis.Complete && analysis.Reads.Contains(new(FieldBank.Persistent0Word, 11, true)) && analysis.Reads.Contains(new(FieldBank.Persistent0Word, 21, true)) && analysis.Reads.Contains(new(FieldBank.Persistent0Word, 31, true)) && analysis.ProgramCounters.Contains(11), "Script analysis did not decode the ten-byte trigonometry successor and persistent inputs.");
        Check(beforeAnalysis == JsonSerializer.Serialize(analyzed.Capture()), "Trigonometry analysis mutated the live native VM.");
        Console.WriteLine("PASS native SIN/COS operand widths, bank selectors, signed arithmetic, native precision and cold same-pass continuation");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
