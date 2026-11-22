using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDeferredFieldTextTests
{
    internal static void Run()
    {
        byte[] bytes = Section(terminated: false);
        byte[] before = bytes.ToArray();
        PcFieldScripts scripts = PcFieldScripts.Read(bytes);
        Check(scripts.Texts.Count == 1 && scripts.AkaoOffsets.Single() == 113, "An unused native text/audio alias was discarded or its directory changed.");
        FieldMachine machine = new(FieldProgram.FromPcScripts(scripts));
        machine.Initialize();
        Check(machine.Capture().Initialized, "An unused unterminated native text blocked ordinary field initialization.");
        Check(bytes.SequenceEqual(before) && scripts.Data.Span.SequenceEqual(before), "Field loading changed source bytes or raw native program identity.");
        Expect<InvalidDataException>(() => _ = scripts.Texts[0]);
        Expect<InvalidDataException>(() => _ = scripts.Texts.ToArray());
        bytes[^1] = 0xFF;
        Expect<InvalidDataException>(() => _ = scripts.Texts[0]);
        PcFieldScripts valid = PcFieldScripts.Read(bytes);
        byte[] expected = bytes.AsSpan(113, bytes.Length - 114).ToArray();
        bytes[^1] = 0;
        Check(valid.Texts[0].Span.SequenceEqual(expected), "Deferred native text resolution used caller-mutated source bytes.");
        Check(valid.Texts[0].Span.SequenceEqual(expected), "Repeated text resolution changed exact original text bytes.");
        CheckHostBoundary(scripts, valid);
        foreach (ushort relative in new ushort[] { 0, 3, ushort.MaxValue })
        {
            byte[] invalid = Section(true);
            BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(111), relative);
            Expect<InvalidDataException>(() => PcFieldScripts.Read(invalid));
        }
        byte[] badDirectory = Section(true);
        BinaryPrimitives.WriteUInt16LittleEndian(badDirectory.AsSpan(4), 40);
        Expect<InvalidDataException>(() => PcFieldScripts.Read(badDirectory));
        byte[] badCount = Section(true);
        BinaryPrimitives.WriteUInt16LittleEndian(badCount.AsSpan(109), ushort.MaxValue);
        Expect<InvalidDataException>(() => PcFieldScripts.Read(badCount));
        Console.WriteLine("Unused native text aliases load unchanged while actual indexing and MESSAGE reject unterminated bodies.");
    }

    private static void CheckHostBoundary(PcFieldScripts invalid, PcFieldScripts valid)
    {
        Ff7FieldResources Field(PcFieldScripts scripts) => new("text-fixture", null!, scripts, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(Field(invalid), (_, _) => 1);
        string before = JsonSerializer.Serialize(host.Capture());
        Expect<InvalidDataException>(() => host.Execute(new(7, 0, 1, 108, new FieldDialogueCommand(0, 0))));
        Check(before == JsonSerializer.Serialize(host.Capture()), "Failed actual MESSAGE resolution queued UI, a receipt or other native host state.");
        Ff7FieldHost normal = new(Field(valid), (_, _) => 1);
        normal.Execute(new(8, 0, 1, 108, new FieldDialogueCommand(0, 0)));
        Check(normal.Process.Dialogues.Single() is { RequestId: 8, Text: 0 }, "A valid text/audio alias did not retain its actual native dialogue request.");
    }

    private static byte[] Section(bool terminated)
    {
        byte[] source = new byte[119];
        BinaryPrimitives.WriteUInt16LittleEndian(source, 0x0502);
        source[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(4), 109);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(6), 1);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8), 512);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(40), 113);
        for (int script = 0; script < 32; script++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(44 + script * 2), 108);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(109), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(111), 4);
        new byte[] { 0x41, 0x4B, 0x41, 0x4F, 1, terminated ? (byte)0xFF : (byte)0 }.CopyTo(source, 113);
        return source;
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (T)
        {
            rejected = true;
        }
        if (!rejected)
        {
            throw new InvalidOperationException("Malformed requested native text or structural directory was accepted.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
