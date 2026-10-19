using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleXatmDeath276BodyProjection(ImmutableArray<byte> Bytes, ImmutableArray<NativeBattleProjectedVertex> Vertices);

public static class NativeBattleXatmDeath276Body
{
    public static NativeBattleXatmDeath276BodyProjection Project6A2A90(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleCameraProjection projection, ImmutableArray<byte> retained, IReadOnlySet<int> hidden)
    {
        if (retained.IsDefault || retained.Length != mesh.Vertices.Length * 8)
        {
            throw new InvalidDataException("Native6A2A90 requires its actual shared projected scratch bytes.");
        }
        byte[] bytes = retained.ToArray();
        ImmutableArray<NativeBattleProjectedVertex>.Builder result = ImmutableArray.CreateBuilder<NativeBattleProjectedVertex>();
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            if (hidden.Contains(vertex.Bone))
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 6), 16);
            }
            else
            {
                NativeBattleModuleProjectedVertex point = NativeBattleModuleProjection.Project45FE10(bones[vertex.Bone], vertex.Position, projection);
                if (point.Depth == 0)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 6), 16);
                }
                else
                {
                    short x = unchecked((short)point.Screen);
                    short y = unchecked((short)(point.Screen >> 16));
                    ushort flags = x < 0 ? (ushort)1 : x >= 2560 ? (ushort)2 : (ushort)0;
                    flags |= y < 0 ? (ushort)4 : y >= 1728 ? (ushort)8 : (ushort)0;
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 8), point.Screen);
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 4), point.Depth);
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 6), flags);
                }
            }
            result.Add(new(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 8)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 8 + 4))));
        }
        return new(bytes.ToImmutableArray(), result.ToImmutable());
    }

    public static NativeBattleGfGeometryBatch Packets6859D0(ImmutableArray<NativeBattleRawPolygon> polygons, ImmutableArray<NativeBattleProjectedVertex> projected, uint color, NativeBattleModelTextureBinding? binding, short zsf3, short zsf4)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50(polygons, projected, color, binding);
        ImmutableArray<NativeBattleGfGeometryAttempt> attempts = batch.Attempts.Select(attempt =>
        {
            NativeBattleGeometryPrimitive primitive = attempt.Primitive ?? throw new InvalidDataException("Native6859D0 packet assembly emitted an unexpected rejected allocation.");
            int count = primitive.Words.Length == 10 ? 4 : 3;
            int sum = primitive.VertexPayload4.Take(count).Sum();
            int depth = Math.Clamp(unchecked(sum * (count == 4 ? zsf4 : zsf3)) >> 12, 0, 65535);
            return attempt with { Primitive = primitive with { Bucket = checked(17 + depth) } };
        }).ToImmutableArray();
        return batch with { Attempts = attempts };
    }

    public static NativeBattleGfGeometryBatch White7040B0(int red, int green, int blue, int level)
    {
        uint color = (uint)unchecked((byte)(unchecked(red * level) >> 12)) | (uint)unchecked((byte)(unchecked(green * level) >> 12)) << 8 | (uint)unchecked((byte)(unchecked(blue * level) >> 12)) << 16;
        ImmutableArray<uint> words = [0x05000000, 0x2A000000 | color, 0, 0x140, 0xD80000, 0xD80140];
        byte[] bytes = new byte[24];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(8, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
        return new([new(0, bytes.ToImmutableArray(), mask, primitive)], 24);
    }
}

