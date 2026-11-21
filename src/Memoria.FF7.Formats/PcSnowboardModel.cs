using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public sealed record PcSnowboardVertex(short X, short Y, short Z, ushort Padding);
public sealed record PcSnowboardPrimitive(byte OutputWordCount, byte InputWordCount, byte Flags, byte Mode, IReadOnlyList<uint> Words);
public sealed record PcSnowboardObject(int Scale, IReadOnlyList<PcSnowboardVertex> Vertices, IReadOnlyList<PcSnowboardVertex> Normals, IReadOnlyList<PcSnowboardPrimitive> Primitives);

/// <summary>Unrelocated native snowboard TMD records; primitive payloads retain their original word representation.</summary>
public sealed class PcSnowboardModel
{
    private PcSnowboardModel(PcSnowboardObject[] objects)
    {
        Objects = Array.AsReadOnly(objects);
    }

    public IReadOnlyList<PcSnowboardObject> Objects { get; }

    public static PcSnowboardModel Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(source) != 0x41)
        {
            throw new InvalidDataException("Invalid native snowboard TMD header.");
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(source[4..]) != 0)
        {
            throw new InvalidDataException("Snowboard TMD input must contain unrelocated relative pointers.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(source[8..]);
        Range(source.Length, 12, count, 28);
        PcSnowboardObject[] objects = new PcSnowboardObject[(int)count];
        for (int index = 0; index < objects.Length; index++)
        {
            ReadOnlySpan<byte> entry = source.Slice(12 + index * 28, 28);
            uint vertexOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            uint vertexCount = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            uint normalOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            uint normalCount = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            uint primitiveOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry[16..]);
            uint primitiveCount = BinaryPrimitives.ReadUInt32LittleEndian(entry[20..]);
            int scale = BinaryPrimitives.ReadInt32LittleEndian(entry[24..]);
            PcSnowboardVertex[] vertices = ReadVectors(source, vertexOffset, vertexCount);
            PcSnowboardVertex[] normals = ReadVectors(source, normalOffset, normalCount);
            int cursor = Range(source.Length, 12L + primitiveOffset, primitiveCount, 4);
            PcSnowboardPrimitive[] primitives = new PcSnowboardPrimitive[(int)primitiveCount];
            for (int primitive = 0; primitive < primitives.Length; primitive++)
            {
                Range(source.Length, cursor, 1, 4);
                byte outputWords = source[cursor];
                byte inputWords = source[cursor + 1];
                byte flags = source[cursor + 2];
                byte mode = source[cursor + 3];
                cursor += 4;
                Range(source.Length, cursor, inputWords, 4);
                uint[] words = new uint[inputWords];
                for (int word = 0; word < words.Length; word++)
                {
                    words[word] = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(cursor + word * 4, 4));
                }
                cursor += inputWords * 4;
                primitives[primitive] = new(outputWords, inputWords, flags, mode, Array.AsReadOnly(words));
            }
            objects[index] = new(scale, Array.AsReadOnly(vertices), Array.AsReadOnly(normals), Array.AsReadOnly(primitives));
        }
        return new(objects);
    }

    private static PcSnowboardVertex[] ReadVectors(ReadOnlySpan<byte> source, uint offset, uint count)
    {
        int start = Range(source.Length, 12L + offset, count, 8);
        PcSnowboardVertex[] vectors = new PcSnowboardVertex[(int)count];
        for (int index = 0; index < vectors.Length; index++)
        {
            ReadOnlySpan<byte> record = source.Slice(start + index * 8, 8);
            vectors[index] = new(BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadUInt16LittleEndian(record[6..]));
        }
        return vectors;
    }

    private static int Range(int length, long start, uint count, int stride)
    {
        if (start < 0 || start > length || (long)count * stride > length - start)
        {
            throw new InvalidDataException("Snowboard TMD record range exceeds its source.");
        }
        return (int)start;
    }
}
