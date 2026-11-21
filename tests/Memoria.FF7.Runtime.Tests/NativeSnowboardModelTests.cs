using System.Buffers.Binary;
using Memoria.FF7.Formats;

internal static class NativeSnowboardModelTests
{
    internal static void Run()
    {
        byte[] source = Fixture();
        byte[] before = source.ToArray();
        PcSnowboardModel model = PcSnowboardModel.Read(source);
        PcSnowboardObject item = model.Objects.Single();
        Check(item.Scale == -3 && item.Vertices.Single() == new PcSnowboardVertex(-12, 34, short.MinValue, 0xFEDC), "Relative vertex records lost signed coordinates, scale or padding.");
        Check(item.Normals.Single() == new PcSnowboardVertex(4096, -4096, 0, 0x1234), "Relative normal records were interpreted as relocated pointers or vertex data.");
        Check(item.Primitives.Count == 2 && item.Primitives[0] is { OutputWordCount: 7, InputWordCount: 5, Flags: 1, Mode: 0x31 } && item.Primitives[1] is { OutputWordCount: 9, InputWordCount: 6, Flags: 1, Mode: 0x25 }, "Primitive input lengths or native type metadata were changed.");
        Check(item.Primitives[0].Words.SequenceEqual(new uint[] { 0x80000000, 1, 2, 3, uint.MaxValue }) && item.Primitives[1].Words.SequenceEqual(new uint[] { 6, 5, 4, 3, 2, 1 }), "Primitive payload words were normalized or decoded speculatively.");
        Check(source.SequenceEqual(before), "Structural parsing modified the original resource bytes.");
        source[40] = 0;
        Check(item.Vertices[0].X == -12, "Parsed records borrowed a mutable source buffer.");
        foreach (Action<byte[]> corrupt in new Action<byte[]>[]
        {
            bytes => Word(bytes, 0, 0x42),
            bytes => Word(bytes, 4, 0x80000000),
            bytes => Word(bytes, 4, 1),
            bytes => Word(bytes, 8, uint.MaxValue),
            bytes => Word(bytes, 12, uint.MaxValue),
            bytes => Word(bytes, 16, uint.MaxValue),
            bytes => Word(bytes, 20, uint.MaxValue),
            bytes => Word(bytes, 28, uint.MaxValue),
            bytes => Word(bytes, 32, uint.MaxValue),
            bytes => bytes[57] = 255
        })
        {
            byte[] invalid = Fixture();
            corrupt(invalid);
            Reject(invalid);
        }
        Reject(before.AsSpan(0, 11));
        Reject(before.AsSpan(0, before.Length - 1));
        Console.WriteLine("Native snowboard TMD structure, raw primitive preservation and bounds passed.");
    }

    private static byte[] Fixture()
    {
        byte[] data = new byte[108];
        Word(data, 0, 0x41);
        Word(data, 8, 1);
        Word(data, 12, 28);
        Word(data, 16, 1);
        Word(data, 20, 36);
        Word(data, 24, 1);
        Word(data, 28, 44);
        Word(data, 32, 2);
        Word(data, 36, unchecked((uint)-3));
        Vector(data, 40, -12, 34, short.MinValue, 0xFEDC);
        Vector(data, 48, 4096, -4096, 0, 0x1234);
        data[56] = 7;
        data[57] = 5;
        data[58] = 1;
        data[59] = 0x31;
        uint[] first = [0x80000000, 1, 2, 3, uint.MaxValue];
        for (int index = 0; index < first.Length; index++)
        {
            Word(data, 60 + index * 4, first[index]);
        }
        data[80] = 9;
        data[81] = 6;
        data[82] = 1;
        data[83] = 0x25;
        for (int index = 0; index < 6; index++)
        {
            Word(data, 84 + index * 4, (uint)(6 - index));
        }
        return data;
    }

    private static void Word(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);

    private static void Vector(byte[] data, int offset, short x, short y, short z, ushort padding)
    {
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), x);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 2), y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), z);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 6), padding);
    }

    private static void Reject(ReadOnlySpan<byte> source)
    {
        try
        {
            PcSnowboardModel.Read(source);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Invalid native TMD structure was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
