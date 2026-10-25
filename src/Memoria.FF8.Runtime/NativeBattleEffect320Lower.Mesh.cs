using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Lower
{
    public static NativeBattleEffect320Frame Draw65C750(NativeBattleEffect320Frame frame, uint temporary, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 108);
        uint source = Dword320(context, 0);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(source);
        uint flags = Dword320(context, 0x1C);
        if (flags is not (0x2030 or 0x20F0))
        {
            throw new NotSupportedException("Effect320 custom drawing requires its selected context flags.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int faces = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            if (faces < 0 || (faces != 0 && group is not (2 or 3 or 6 or 7)))
            {
                throw new NotSupportedException("Effect320 custom geometry requires its reached textured groups.");
            }
            cursor = checked(cursor + 4 + faces * strides[group]);
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        uint selected = Dword320(context, 4);
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, ReadVertex, readSelectedPackedVertex: ReadPacked);
        NativeBattleGfGeometryBatch batch = AddCustomModes(drawn.Batch);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
        return inputs.Execute(frame, new NativeBattleEffect320DrawBatch(batch, Cursor));

        NativeBattleEffectArena ReadPacked(int offset)
        {
            return Source320(frame, resources, checked(selected + (uint)offset), 8);
        }

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            NativeBattleEffectArena row = ReadPacked(offset);
            (byte[] bytes, bool[] known) = row.ReadSnapshot(6, 2);
            return (Vector(row), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
    }

    public static NativeBattleGfGeometryBatch AddCustomModes(NativeBattleGfGeometryBatch original)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int extra = 0;
        for (int index = 0; index < original.Attempts.Length; index++)
        {
            NativeBattleGfGeometryAttempt raw = original.Attempts[index];
            int physical = checked(raw.ByteOffsetRelative + extra);
            bool bracketed = raw.Primitive is null && index + 3 < original.Attempts.Length && IsWindow(original.Attempts[index + 1]) && original.Attempts[index + 2].Primitive is not null && IsWindow(original.Attempts[index + 3]) && original.Attempts[index + 2].ByteOffsetRelative == raw.ByteOffsetRelative;
            if (bracketed)
            {
                NativeBattleGfGeometryAttempt linked = original.Attempts[index + 2];
                attempts.Add(raw with { ByteOffsetRelative = physical });
                attempts.Add(Mode(checked(physical + raw.PacketLength), 8, 0xE1000220, linked.Primitive!.Bucket));
                attempts.Add(original.Attempts[index + 1] with { ByteOffsetRelative = checked(original.Attempts[index + 1].ByteOffsetRelative + extra + 8) });
                attempts.Add(linked with { ByteOffsetRelative = physical });
                attempts.Add(original.Attempts[index + 3] with { ByteOffsetRelative = checked(original.Attempts[index + 3].ByteOffsetRelative + extra + 8) });
                index += 3;
                extra = checked(extra + 8);
            }
            else if (raw.Primitive is { } primitive)
            {
                attempts.Add(raw with { ByteOffsetRelative = physical, Primitive = null });
                attempts.Add(Mode(checked(physical + raw.PacketLength), 8, 0xE1000220, primitive.Bucket));
                attempts.Add(raw with { ByteOffsetRelative = physical, WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, raw.PacketLength).ToImmutableArray() });
                extra = checked(extra + 8);
            }
            else
            {
                attempts.Add(raw with { ByteOffsetRelative = physical });
            }
        }
        return original with { Attempts = attempts.ToImmutable(), AllocationBytes = checked(original.AllocationBytes + extra) };

        static bool IsWindow(NativeBattleGfGeometryAttempt attempt)
        {
            return attempt.Primitive is { Words.Length: >= 2 } primitive && (primitive.Words[1] & 0xFF000000) == 0xE2000000;
        }
    }

    private static NativeBattleEffectArena Source320(NativeBattleEffect320Frame frame, NativeBattleEffect320Resources resources, uint address, int length)
    {
        foreach (NativeBattleGfAnimationSource programme in resources.Programmes.Values)
        {
            if (address >= programme.Address && (ulong)address + (uint)length <= (ulong)programme.Address + (uint)programme.Data.Length)
            {
                return NativeBattleEffectArena.Clear().Write(0, programme.Data.AsSpan().Slice(checked((int)(address - programme.Address)), length));
            }
        }
        return ReadBytes(frame, address, length);
    }

    private static uint Dword320(NativeBattleEffectArena arena, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    }
}
