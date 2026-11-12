using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleShadowFace(int A, int B, int C);

/// <summary>The original battle KAGE vertex table and untextured Gouraud triangle records.</summary>
public sealed record Ff7BattleShadowResources(ImmutableArray<Vector3> Vertices, ImmutableArray<Ff7BattleShadowFace> Faces)
{
    public float GroundHeight { get; init; } = -4;

    public static Ff7BattleShadowResources Load(Ff7Installation installation, int locationId)
    {
        return Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleShadow).Path)) with { GroundHeight = Ff7PcExecutableData.LoadBattleShadowGroundHeight(installation, locationId) };
    }

    public static Ff7BattleShadowResources Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20)
        {
            throw new InvalidDataException("FFVII battle shadow has no complete vertex/primitive header.");
        }
        int vertexBytes = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (vertexBytes <= 0 || vertexBytes % 8 != 0 || vertexBytes > data.Length - 20)
        {
            throw new InvalidDataException("FFVII battle shadow vertex table exceeds its input.");
        }
        ImmutableArray<Vector3>.Builder vertices = ImmutableArray.CreateBuilder<Vector3>(vertexBytes / 8);
        for (int offset = 4; offset < vertexBytes + 4; offset += 8)
        {
            vertices.Add(new(BinaryPrimitives.ReadInt16LittleEndian(data[offset..]), BinaryPrimitives.ReadInt16LittleEndian(data[(offset + 2)..]), BinaryPrimitives.ReadInt16LittleEndian(data[(offset + 4)..])));
        }
        int header = vertexBytes + 4;
        if (BinaryPrimitives.ReadUInt16LittleEndian(data[header..]) != 0 || BinaryPrimitives.ReadInt32LittleEndian(data[(header + 4)..]) != 0)
        {
            throw new NotSupportedException("FFVII battle shadow requires the original untextured Gouraud triangle owner.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(data[(header + 8)..]);
        if (count <= 0 || count > (data.Length - header - 16) / 20 || data.Length != header + 16 + count * 20 || BinaryPrimitives.ReadInt32LittleEndian(data[(header + 12 + count * 20)..]) != 0)
        {
            throw new InvalidDataException("FFVII battle shadow triangle table exceeds its input or contains unsupported trailing primitives.");
        }
        ImmutableArray<Ff7BattleShadowFace>.Builder faces = ImmutableArray.CreateBuilder<Ff7BattleShadowFace>(count);
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> face = data.Slice(header + 12 + index * 20, 20);
            int a = ReadIndex(face);
            int b = ReadIndex(face[2..]);
            int c = ReadIndex(face[4..]);
            if ((face[11] & 0xFC) != 0x30 || a >= vertices.Count || b >= vertices.Count || c >= vertices.Count)
            {
                throw new InvalidDataException("FFVII battle shadow contains an invalid Gouraud triangle or vertex offset.");
            }
            faces.Add(new(a, b, c));
        }
        return new(vertices.MoveToImmutable(), faces.MoveToImmutable());

        static int ReadIndex(ReadOnlySpan<byte> value)
        {
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(value);
            if (offset % 8 != 0)
            {
                throw new InvalidDataException("FFVII battle shadow vertex offset is not aligned to its native record.");
            }
            return offset / 8;
        }
    }
}
