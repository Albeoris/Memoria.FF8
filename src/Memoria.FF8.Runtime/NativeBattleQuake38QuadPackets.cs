using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38QuadPackets
{
    public static NativeBattleGfGeometryBatch Emit(ImmutableArray<NativeBattleVector> positions, ImmutableArray<byte> fields, ImmutableArray<NativeBattleGfPacketByteState> fieldMask, NativeBattleCameraProjection projection, short zsf4, bool rejectOutside = true)
    {
        if (positions.IsDefault || positions.Length != 4 || fields.IsDefault || fields.Length != 40 || fieldMask.IsDefault || fieldMask.Length != 40)
        {
            throw new InvalidDataException("Native Quake quad requires four actual vertices and its exact packet field writes.");
        }
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native Quake quad requires its current camera matrix.");
        NativeBattleModuleProjectedVertex[] vertices = positions.Select(position => NativeBattleModuleProjection.Project45FE10(camera, position, projection)).ToArray();
        if (NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) <= 0)
        {
            return new([], 0);
        }
        byte[] bytes = new byte[40];
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8 + index * 8), vertices[index].Screen);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, 8 + index * 8, 4);
        }
        bool rejected = rejectOutside && vertices.All(vertex => (ushort)vertex.Screen >= 0xA00) && vertices.All(vertex => (ushort)(vertex.Screen >> 16) >= 0x780);
        NativeBattleGeometryPrimitive? primitive = null;
        if (!rejected)
        {
            for (int index = 0; index < fields.Length; index++)
            {
                if (fieldMask[index] != NativeBattleGfPacketByteState.Untouched)
                {
                    bytes[index] = fields[index];
                    mask[index] = fieldMask[index];
                }
            }
            uint sum = unchecked((uint)vertices[0].Depth + vertices[1].Depth + vertices[2].Depth + vertices[3].Depth);
            uint depth = unchecked(sum * (uint)(int)zsf4) >> 12;
            int bucket = unchecked(17 + (int)(depth >> 2));
            ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            primitive = new(bucket, words, vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray());
        }
        return new([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive)], rejected ? 0 : 40);
    }
}
