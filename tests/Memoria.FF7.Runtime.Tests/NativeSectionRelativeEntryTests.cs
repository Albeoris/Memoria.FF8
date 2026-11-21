using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class NativeSectionRelativeEntryTests
{
    internal static void Run()
    {
        byte[] source = Section();
        byte[] original = source.ToArray();
        PcFieldScripts scripts = PcFieldScripts.Read(source);
        Check(scripts.Entities[0].ScriptOffsets[3] == scripts.TextOffset && scripts.Entities[0].ScriptOffsets[4] == 116, "In-section script table aliases were rejected or remapped to a code-region sentinel.");
        Check(scripts.Data.Span.SequenceEqual(original) && source.SequenceEqual(original), "Parsing changed raw section bytes.");
        FieldProgram program = FieldProgram.FromPcScripts(scripts);
        FieldProgram exact = new(original, [scripts.Entities[0].ScriptOffsets.ToArray()]);
        Check(program.Identity == exact.Identity, "PC script conversion changed the actual raw entries or program identity.");
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldExternalScriptAdmission admission = machine.RequestExternalScript(0, 4);
        Check(admission.Started && machine.Capture().Entities[0].ProgramCounter == 116, "A requested raw in-section entry was replaced with RET or another script.");
        bool unsupported = false;
        try
        {
            machine.AdvanceTick();
        }
        catch (FieldExecutionException error) when (error.Context.ProgramCounter == 116 && error.Context.Opcode == 0xFF && error.InnerException is NotSupportedException)
        {
            unsupported = true;
        }
        Check(unsupported && machine.Capture().Entities[0].ProgramCounter == 116, "An actually executed unsupported alias silently completed or lost its exact native PC.");
        foreach (ushort offset in new ushort[] { 103, 128, ushort.MaxValue })
        {
            byte[] invalid = Section();
            BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(40 + 3 * 2), offset);
            bool rejected = false;
            try
            {
                PcFieldScripts.Read(invalid);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected, "An entry in the directory or outside the section was accepted.");
        }
        Console.WriteLine("Raw section-relative script aliases and actual execution validation passed.");
    }

    private static byte[] Section()
    {
        byte[] section = new byte[128];
        BinaryPrimitives.WriteUInt16LittleEndian(section, 0x0502);
        section[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(4), 106);
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(8), 512);
        for (int slot = 0; slot < 32; slot++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(40 + slot * 2), 104);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(46), 106);
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(48), 116);
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(106), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(108), 4);
        section[110] = 0xFF;
        section[116] = 0xFF;
        return section;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
