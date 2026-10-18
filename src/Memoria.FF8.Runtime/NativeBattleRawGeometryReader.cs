using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The original packed model vertex groups and unchanged FT3/FT4 records.</summary>
public static class NativeBattleRawGeometryReader
{
    public static ImmutableArray<NativeBattleRawMesh> Read(byte[] bytes, int boneCount)
    {
        int count = I32(0);
        if (count is < 0 or > 256)
        {
            throw new InvalidDataException("Native enemy geometry object directory exceeds its original resource.");
        }
        var meshes = ImmutableArray.CreateBuilder<NativeBattleRawMesh>(count);
        for (int mesh = 0; mesh < count; mesh++)
        {
            int cursor = I32(4 + mesh * 4);
            int groups = U16(cursor);
            cursor += 2;
            var vertices = ImmutableArray.CreateBuilder<NativeBattleRawVertex>();
            var bones = ImmutableArray.CreateBuilder<short>();
            for (int group = 0; group < groups; group++)
            {
                ushort bone = U16(cursor);
                int total = U16(cursor + 2);
                cursor += 4;
                if (bone >= boneCount)
                {
                    throw new InvalidDataException("Native enemy geometry references an absent skeleton bone.");
                }
                bones.Add(unchecked((short)bone));
                for (int vertex = 0; vertex < total; vertex++)
                {
                    vertices.Add(new(new(unchecked((short)U16(cursor)), unchecked((short)U16(cursor + 2)), unchecked((short)U16(cursor + 4))), bone));
                    cursor += 6;
                }
            }
            cursor = checked((cursor + 3) & ~3);
            int triangles = unchecked((short)U16(cursor));
            int quads = unchecked((short)U16(cursor + 2));
            cursor += 12;
            var polygons = ImmutableArray.CreateBuilder<NativeBattleRawPolygon>();
            for (int face = 0; face < triangles + quads; face++)
            {
                bool quad = face >= triangles;
                int size = quad ? 20 : 16;
                polygons.Add(new(quad, bytes.AsSpan(cursor, size).ToArray().ToImmutableArray()));
                cursor += size;
            }
            meshes.Add(new(vertices.ToImmutable(), polygons.ToImmutable()) { BoneGroups = bones.ToImmutable() });
        }
        return meshes.ToImmutable();

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }
}
