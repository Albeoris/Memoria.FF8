using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Assets;

public readonly record struct FieldMovieMaskTriangle(Vector3 A, Vector3 B, Vector3 C);

/// <summary>The original MSK bank: a four-byte header followed by three eight-byte SVECTORs per triangle.</summary>
public sealed record FieldMovieMask(IReadOnlyList<FieldMovieMaskTriangle> Triangles)
{
    public static FieldMovieMask Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
        {
            throw new InvalidDataException("A field movie mask requires its original four-byte header.");
        }
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if (bytes.Length != 4 + count * 24)
        {
            throw new InvalidDataException("Field movie mask size differs from its original triangle count.");
        }
        FieldMovieMaskTriangle[] triangles = new FieldMovieMaskTriangle[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = bytes.Slice(4 + index * 24, 24);
            triangles[index] = new FieldMovieMaskTriangle(Vertex(record), Vertex(record[8..]), Vertex(record[16..]));
        }
        return new FieldMovieMask(Array.AsReadOnly(triangles));
    }

    private static Vector3 Vertex(ReadOnlySpan<byte> bytes)
    {
        return new Vector3(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
    }
}
