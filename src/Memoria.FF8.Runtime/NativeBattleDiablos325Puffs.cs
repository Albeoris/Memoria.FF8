using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The original 160 physical sixteen-byte puff records and CRT consumption order.</summary>
public static partial class NativeBattleDiablos325Puffs
{
    public const int BaseOffset = 0x4C5C;
    public const int Capacity = 160;

    public static NativeBattleDiablos325Frame Reset6545B0(NativeBattleDiablos325Frame frame)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        using NativeBattleEffectArenaWriter writer = new(frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos puffs require their actual current arena."));
        for (int index = 0; index < Capacity; index++)
        {
            writer.Write(BaseOffset + index * 16 + 15, [0]);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze(), Diablos325 = state with { Mode51F0 = 0, ParticleCursor51F4 = checked(state.ModelBuffer5208 + BaseOffset) } } };
    }

    public static NativeBattleDiablos325Frame Spawn658B90(NativeBattleDiablos325Frame frame, uint count, int y, bool inward)
    {
        if (count == 0)
        {
            return frame;
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos puffs require their actual current arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        using NativeBattleEffectArenaWriter writer = new(owned?.View ?? source);
        NativeCrtRandomState? random = frame.Random;
        for (uint index = 0; index < count; index++)
        {
            int? selected = Allocate658B20(writer, ref state);
            if (selected is not int offset)
            {
                break;
            }
            random = (random ?? throw new NotSupportedException("Native Diablos puff birth requires its actual CRT random owner.")).Draw(out ushort lifeRandom);
            random = random.Draw(out ushort shadeRandom);
            random = random.Draw(out ushort xRandom);
            random = random.Draw(out _);
            int life = (lifeRandom & 7) + 20;
            int x = (xRandom & 0x1FFF) - 4096;
            int cosine = unchecked((sbyte)NativeBattleCameraMath.Cosine(x >> 4));
            int divisor = (-1 - life) * 4;
            byte[] bytes = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xFD67A0);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), unchecked((short)x));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), unchecked((short)(y - cosine)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), unchecked((short)(state.CenterZ51D4 + 4096)));
            bytes[10] = unchecked((byte)(-x >> 7));
            bytes[11] = (byte)((shadeRandom & 15) + 64);
            bytes[12] = unchecked((byte)(x / divisor));
            bytes[13] = inward ? unchecked((byte)(cosine / divisor)) : (byte)16;
            bytes[14] = unchecked((byte)(-4096 / (life * 4)));
            bytes[15] = (byte)life;
            writer.Write(offset, bytes);
            NativeBattlePilotVisualLifetimes.Allocated(owned?.View ?? source, checked(state.ModelBuffer5208 + BaseOffset), (offset - BaseOffset) / 16);
        }
        return frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? writer.Freeze(), Diablos325 = state }, Random = random };
    }

    public static NativeBattleDiablos325Frame Rise658D30(NativeBattleDiablos325Frame frame) => Move(frame, true);
    public static NativeBattleDiablos325Frame Shrink658C80(NativeBattleDiablos325Frame frame) => Move(frame, false);

    private static int? Allocate658B20(NativeBattleEffectArenaWriter writer, ref NativeBattleDiablos325State state)
    {
        long difference = (long)state.ParticleCursor51F4 - state.ModelBuffer5208;
        int offset = difference is >= 0 and <= int.MaxValue ? (int)difference : -1;
        if (offset < BaseOffset || offset >= BaseOffset + Capacity * 16 || ((offset - BaseOffset) & 15) != 0)
        {
            throw new NotSupportedException("Native Diablos puff allocation requires its actual retained record cursor.");
        }
        if (writer.ReadCurrent(offset + 15, 1)[0] != 0)
        {
            offset = BaseOffset;
            while (offset < BaseOffset + Capacity * 16 && writer.ReadCurrent(offset + 15, 1)[0] != 0)
            {
                offset += 16;
            }
            if (offset == BaseOffset + Capacity * 16)
            {
                return null;
            }
        }
        state = state with { Mode51F0 = 3, ParticleCursor51F4 = checked(state.ModelBuffer5208 + (uint)(offset == BaseOffset + (Capacity - 1) * 16 ? BaseOffset : offset + 16)) };
        return offset;
    }

    private static NativeBattleDiablos325Frame Move(NativeBattleDiablos325Frame frame, bool accelerate)
    {
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos puff motion requires its actual arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        using NativeBattleEffectArenaWriter writer = new(owned?.View ?? source);
        for (int index = 0; index < Capacity; index++)
        {
            int offset = BaseOffset + index * 16;
            if (writer.ReadCurrent(offset + 15, 1)[0] == 0)
            {
                continue;
            }
            byte[] position = writer.ReadCurrent(offset + 4, 6);
            byte[] tail = writer.ReadCurrent(offset + 11, 5);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(axis * 2));
                BinaryPrimitives.WriteInt16LittleEndian(position.AsSpan(axis * 2), unchecked((short)(value + (unchecked((sbyte)tail[axis + 1]) << 2))));
            }
            tail[0] = unchecked((byte)(tail[0] - 2));
            tail[4] = unchecked((byte)(tail[4] - 1));
            if (accelerate)
            {
                tail[2] = unchecked((byte)(tail[2] - 3));
            }
            writer.Write(offset + 4, position).Write(offset + 11, tail);
        }
        return frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? writer.Freeze() } };
    }
}
