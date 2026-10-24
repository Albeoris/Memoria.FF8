using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect207Lower
{
    public static NativeBattleEffect207Frame Draw45E0(NativeBattleEffect207Frame frame, uint temporary, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 108);
        uint source = L207U(context, 0);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(source);
        uint flags = L207U(context, 0x1C);
        if (flags is not (0x2030 or 0x20F0) || L207S(context, 0x20) != 0 || L207S(context, 0x22) != 0)
        {
            throw new NotSupportedException("Effect207 custom drawing requires its selected zero-offset context.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int groupSeen = -1;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            if (count != 0)
            {
                if (group is not (3 or 7) || groupSeen != -1)
                {
                    throw new NotSupportedException("Effect207 custom source requires its selected quad group.");
                }
                groupSeen = group;
            }
            cursor = checked(cursor + 4 + count * strides[group]);
        }
        uint selected = L207U(context, 4);
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, 63, ReadVertex);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int extra = 0;
        foreach (NativeBattleGfGeometryAttempt original in drawn.Batch.Attempts)
        {
            NativeBattleGfGeometryAttempt body = original with { ByteOffsetRelative = checked(original.ByteOffsetRelative + extra) };
            if (body.Primitive is { } primitive)
            {
                byte[] bytes = new byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xE1000220);
                NativeBattleGeometryPrimitive mode = new(primitive.Bucket, [0x01000000, 0xE1000220], [0, 0, 0, 0]);
                attempts.Add(new(checked(body.ByteOffsetRelative + body.PacketLength), bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), mode));
                extra = checked(extra + 8);
            }
            attempts.Add(body);
        }
        NativeBattleGfGeometryBatch batch = new(attempts.ToImmutable(), checked(drawn.Batch.AllocationBytes + extra));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
        return inputs.Execute(frame, new NativeBattleEffect207DrawBatch(batch, L207Cursor));

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            NativeBattleEffectArena row = L207Source(frame, resources, checked(selected + (uint)offset), 8);
            (byte[] bytes, bool[] known) = row.ReadSnapshot(6, 2);
            return (Vector(row), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
    }
}
