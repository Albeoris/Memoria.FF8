using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>6DA980's group selection and the reached6DA760/6D7B30 independent GT3 corner expansion.</summary>
public static class NativeBattleIceMeshVertices
{
    public static (int Offset, int Count) Group(ReadOnlySpan<byte> mesh, int requested)
    {
        if ((uint)requested >= 8 || mesh.Length < 8)
        {
            throw new InvalidDataException("Native ice mesh requires its eight-group directory.");
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh));
        for (int group = 0; group <= requested; group++)
        {
            if (cursor < 8 || cursor > mesh.Length - 4)
            {
                throw new InvalidDataException("Native ice mesh group count leaves its current source.");
            }
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh[cursor..]);
            int stride = group switch
            {
                0 or 1 => 12,
                2 or 4 => 20,
                3 or 5 => 24,
                6 => 28,
                _ => 36
            };
            cursor = checked(cursor + 4);
            if (count < 0 || count > (mesh.Length - cursor) / stride)
            {
                throw new InvalidDataException("Native ice mesh group records leave their current source.");
            }
            if (group == requested)
            {
                return (cursor, count);
            }
            cursor = checked(cursor + count * stride);
        }
        throw new InvalidDataException("Native ice mesh group is absent.");
    }

    public static void ExpandGt3(Span<byte> mesh, ReadOnlySpan<byte> sourceVertices, Span<byte> expanded)
    {
        (int first, int count) = Group(mesh, 6);
        if (expanded.Length != checked(count * 3 * 8))
        {
            throw new InvalidDataException("Native ice expansion requires exactly three independent eight-byte records per GT3 face.");
        }
        int output = 0;
        for (int face = 0; face < count; face++)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                int selector = first + face * 28 + 4 + corner * 2;
                int source = BinaryPrimitives.ReadUInt16LittleEndian(mesh[selector..]) * 4;
                if (source > sourceVertices.Length - 8)
                {
                    throw new InvalidDataException("Native ice corner selector has no complete original vertex owner.");
                }
                sourceVertices.Slice(source, 8).CopyTo(expanded[output..]);
                BinaryPrimitives.WriteUInt16LittleEndian(mesh[selector..], checked((ushort)(output / 4)));
                output += 8;
            }
        }
    }
}
