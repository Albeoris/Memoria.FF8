using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>The original packed model vertex groups and unchanged FT3/FT4 records.</summary>
public static class NativeBattleRawGeometryReader
{
    public static ImmutableArray<NativeBattleRawMesh> Read(byte[] bytes, int boneCount) => ReadCore(bytes, null, boneCount, null, uint.MaxValue);

    public static ImmutableArray<NativeBattleRawMesh> Read(Func<int, int, byte[]> read, int boneCount, Action<int, ImmutableArray<ushort>, int, int>? observeObject = null, uint objectMask = uint.MaxValue)
    {
        return ReadCore(null, read, boneCount, observeObject, objectMask);
    }

    private static ImmutableArray<NativeBattleRawMesh> ReadCore(byte[]? source, Func<int, int, byte[]>? read, int boneCount, Action<int, ImmutableArray<ushort>, int, int>? observeObject, uint objectMask)
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
            if ((objectMask & (1u << (mesh & 31))) == 0)
            {
                meshes.Add(new([], []) { BoneGroups = [], PackedVertexGroups = [] });
                continue;
            }
            int groups = U16(cursor);
            cursor += 2;
            var vertices = ImmutableArray.CreateBuilder<NativeBattleRawVertex>();
            var packed = ImmutableArray.CreateBuilder<NativeBattleRawVertexGroup>();
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
                packed.Add(new(bone, unchecked((short)total), ImmutableCollectionsMarshal.AsImmutableArray(Bytes(cursor, checked(total * 6 + 6)).ToArray())));
                bones.Add(unchecked((short)bone));
                for (int vertex = 0; vertex < total; vertex++)
                {
                    vertices.Add(new(new(unchecked((short)U16(cursor)), unchecked((short)U16(cursor + 2)), unchecked((short)U16(cursor + 4))), bone));
                    cursor += 6;
                }
            }
            cursor = checked((cursor + 3) & ~3);
            ImmutableArray<ushort> counts = [U16(cursor), U16(cursor + 2), U16(cursor + 4), U16(cursor + 6)];
            cursor += 12;
            int facePayload = cursor;
            var polygons = ImmutableArray.CreateBuilder<NativeBattleRawPolygon>();
            for (int group = 0; group < counts.Length; group++)
            {
                bool quad = (group & 1) != 0;
                int size = (quad ? 20 : 16) + (group >= 2 ? 4 : 0);
                for (int face = 0; face < counts[group]; face++)
                {
                    polygons.Add(new(quad, ImmutableCollectionsMarshal.AsImmutableArray(Bytes(cursor, size).ToArray())));
                    cursor += size;
                }
            }
            observeObject?.Invoke(mesh, counts, facePayload, cursor);
            meshes.Add(new(vertices.ToImmutable(), polygons.ToImmutable()) { BoneGroups = bones.ToImmutable(), PackedVertexGroups = packed.ToImmutable() });
        }
        return meshes.ToImmutable();

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Bytes(offset, 2));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Bytes(offset, 4));
        ReadOnlySpan<byte> Bytes(int offset, int length)
        {
            ReadOnlySpan<byte> bytes = source is not null ? source.AsSpan(offset, length) : read!(offset, length) ?? throw new InvalidDataException("Native geometry requires its exact addressed source bytes.");
            return bytes.Length == length ? bytes : throw new InvalidDataException("Native geometry requires its exact addressed source bytes.");
        }
    }
}
