using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class NativeTextlessScriptTests
{
    internal static void Run()
    {
        byte[] source = Section(106, 106);
        byte[] original = source.ToArray();
        PcFieldScripts scripts = PcFieldScripts.Read(source);
        Check(scripts.TextOffset == source.Length && scripts.Texts.Count == 0, "An exact-end native text offset did not retain an empty text collection.");
        Check(scripts.Entities[0].ScriptOffsets[0] == 104 && scripts.Entities[0].ScriptOffsets.Skip(1).All(entry => entry == 105), "Textless script entries were rewritten or moved outside their actual section.");
        FieldProgram program = FieldProgram.FromPcScripts(scripts);
        FieldProgram exact = new(original, [scripts.Entities[0].ScriptOffsets.ToArray()]);
        Check(program.Identity == exact.Identity, "Textless conversion changed the raw section's native program identity.");
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldExternalScriptAdmission admission = machine.RequestExternalScript(0, 2);
        Check(admission.Accepted && !admission.Started && machine.Capture().Entities[0].ProgramCounter == 105, "The native empty-entry admission or final-byte ambient RET was changed.");
        machine.AdvanceTick();
        Check(machine.Tick == 1 && machine.Capture().Entities[0].ProgramCounter == 105, "The actual final-byte ambient RET no longer executes its normal native loop.");
        Check(source.SequenceEqual(original) && scripts.Data.Span.SequenceEqual(original), "Parsing or executing a textless section changed source bytes.");

        byte[] yielding = Section(108, 108);
        yielding[106] = 0x5F;
        BinaryPrimitives.WriteUInt16LittleEndian(yielding.AsSpan(44), 106);
        FieldMachine requested = new(FieldProgram.FromPcScripts(PcFieldScripts.Read(yielding)));
        List<FieldScriptReturn> returns = [];
        requested.ScriptReturned += returns.Add;
        requested.Initialize();
        Check(requested.RequestExternalScript(0, 2).Started, "A nonempty textless entry did not receive actual native admission.");
        requested.AdvanceTick();
        Check(requested.Capture().Entities[0].ProgramCounter == 107 && returns.Count == 0, "The native yield was skipped or replaced with a synthetic return.");
        requested.AdvanceTick();
        Check(returns.Any(item => item.Entity == 0 && item.Priority == 1 && item.ProgramCounter == 107), "The admitted textless script did not execute its actual final-byte RET.");

        byte[] nonempty = Section(112, 106);
        BinaryPrimitives.WriteUInt16LittleEndian(nonempty.AsSpan(106), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(nonempty.AsSpan(108), 4);
        nonempty[110] = 0x21;
        nonempty[111] = 0xFF;
        PcFieldScripts ordinary = PcFieldScripts.Read(nonempty);
        Check(ordinary.Texts.Count == 1 && ordinary.Texts[0].Span.SequenceEqual(new byte[] { 0x21 }), "A normal nonempty text directory no longer decodes its exact terminated text.");
        Check(PcFieldScripts.Read(Section(108, 106)).Texts.Count == 0, "An ordinary in-section empty text directory was rejected.");

        foreach (ushort textOffset in new ushort[] { 0, 103, 105, 107, ushort.MaxValue })
        {
            Reject(Section(106, textOffset), "An overlapping, truncated or outside-section text directory was accepted.");
        }
        foreach (ushort entry in new ushort[] { 103, 106, ushort.MaxValue })
        {
            byte[] invalid = Section(106, 106);
            BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(44), entry);
            Reject(invalid, "A textless entry in the directory or outside the section was accepted.");
        }
        Console.WriteLine("Exact-end textless native sections preserve bytes, entries, identity and actual RET execution.");
    }

    private static byte[] Section(int length, ushort textOffset)
    {
        byte[] source = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(source, 0x0502);
        source[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(4), textOffset);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8), 512);
        for (int script = 0; script < 32; script++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(40 + script * 2), script == 0 ? (ushort)104 : (ushort)105);
        }
        return source;
    }

    private static void Reject(byte[] source, string message)
    {
        bool rejected = false;
        try
        {
            PcFieldScripts.Read(source);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
