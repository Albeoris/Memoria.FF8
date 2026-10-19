using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleUltraWaves154Drawing
{
    public static NativeBattleGfGeometryBatch Ring(NativeBattleStaticMeshResource resource, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint color, uint flags, int fade, NativeBattleSummonColors colors)
    {
        ImmutableArray<byte> source = resource.Bytes;
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan());
        int allocated = 0;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
            cursor += 4;
            if (group is 0 or 1 or 4 or 5)
            {
                if (count != 0)
                {
                    throw new InvalidDataException("Native Ultra Waves requires its original empty skipped primitive groups.");
                }
                continue;
            }
            for (int polygon = 0; polygon < count; polygon++)
            {
                ImmutableArray<byte> face = source.Slice(cursor, strides[group]);
                int countVertices = (group & 1) == 0 ? 3 : 4;
                ushort[] offsets = Enumerable.Range(0, countVertices).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + index * 2))).ToArray();
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face.AsSpan(), new NativeBattleGfGeometryPackets.ProjectedCorners(offsets, Project), group, new(flags, 0, fade, null, null, 0, 0, 17, 2), colors, allocated, variant: new(color, color, false, true));
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated += attempt.PacketLength;
                }
                cursor += strides[group];
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            ReadOnlySpan<byte> vertex = source.AsSpan().Slice(8 + offset * 4, 8);
            return NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..])), projection);
        }
    }

    public static NativeBattleGfGeometryBatch Environment(ImmutableArray<byte> data, int allocation, int writtenEnd, int bucket)
    {
        int length = 4 + data[3] * 4;
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, writtenEnd - 3);
        byte[] stores = new byte[allocation];
        data.AsSpan().CopyTo(stores);
        return new([new(0, stores.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, words, [0, 0, 0, 0]))], allocation);
    }
}
