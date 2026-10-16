using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModelTextureBinding(ImmutableArray<byte?> PaletteRows);
public sealed record NativeBattleModelTextureLoad(NativeBattleEffectHeapAllocation Source, NativeBattleModelTextureBinding Binding, ushort AllocatedSlots, ImmutableArray<int> TimOffsets);

public static class NativeBattleModelTextures
{
    /// <summary>507400's sequential TIM selection and exact rectangle relocation; geometry uses the returned507550 row map.</summary>
    public static NativeBattleModelTextureLoad Prepare507400(NativeBattleEffectHeapAllocation source, ushort blockedSlots)
    {
        if (source.Data.IsDefault || source.Data.Length < 8)
        {
            throw new InvalidDataException("Native507400 requires its original counted TIM pack.");
        }
        byte[] data = source.Data.ToArray();
        int count = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (count is < 0 or > 31)
        {
            throw new InvalidDataException("Native model TIM pack exceeds the successful shared upload bank.");
        }
        int offset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));
        ushort allocated = 0;
        ImmutableArray<byte?> rows = ImmutableArray.CreateRange(new byte?[16]);
        ImmutableArray<int>.Builder offsets = ImmutableArray.CreateBuilder<int>(count);
        for (int index = 0; index < count; index++)
        {
            Require(offset, 20);
            int firstLength = Length(offset + 8);
            int image = checked(offset + 8 + firstLength);
            int imageLength = Length(image);
            short clutX = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 12));
            short clutY = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 14));
            if (clutX < 256 && clutY is >= 224 and < 240)
            {
                int slot = 0;
                while (((1 << slot) & (blockedSlots | allocated)) != 0)
                {
                    slot++;
                    if (slot >= 15)
                    {
                        throw new NotSupportedException("Native507400 has no proved successful free texture slot before its15-slot boundary.");
                    }
                }
                allocated |= (ushort)(1 << slot);
                rows = rows.SetItem(clutY - 224, (byte)slot);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 14), (short)(slot + 224));
                int x = data[image + 4] & 63;
                int y = data[image + 6] & 127;
                if (slot < 12)
                {
                    x += (slot / 2 + 10) * 64;
                    y += (slot & 1) * 128;
                }
                else
                {
                    x += (slot - 2) * 64;
                    y += 128;
                }
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(image + 4), checked((short)x));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(image + 6), checked((short)y));
            }
            offsets.Add(offset);
            offset = checked(image + imageLength);
        }
        return new(source with { Data = data.ToImmutableArray() }, new(rows), allocated, offsets.MoveToImmutable());

        int Length(int block)
        {
            Require(block, 12);
            int length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(block));
            if (length < 12)
            {
                throw new InvalidDataException("Native model TIM block length is invalid.");
            }
            Require(block, length);
            return length;
        }

        void Require(int first, int length)
        {
            if (first < 0 || length < 0 || first > data.Length - length)
            {
                throw new InvalidDataException("Native model TIM cursor leaves its original packed source.");
            }
        }
    }

    public static NativeBattleEffectTextureState Enqueue507400(NativeBattleEffectTextureState state, NativeBattleModelTextureLoad load)
    {
        foreach (int offset in load.TimOffsets)
        {
            state = state.EnqueueTim505E30(load.Source, offset).State;
        }
        return state;
    }
}
