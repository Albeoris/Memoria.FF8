using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A transient description of slots within the current resource arena, without a second byte owner.</summary>
public sealed record NativeBattleWaterSlotBank(int FileOffset, int FileLength, int Capacity, int SlotBaseOffset)
{
    internal int SlotOffset(NativeBattleEffectArena arena, int index)
    {
        arena.Validate();
        if (FileOffset < 0 || FileLength < 0x20 || FileOffset > NativeBattleEffectArena.Length - FileLength || Capacity <= 0 || index < 0 || index >= Capacity)
        {
            throw new InvalidDataException("Native slot bank requires its bounded current resource and physical index.");
        }
        if (SlotBaseOffset < FileOffset || (long)SlotBaseOffset + (long)Capacity * 0x100 > (long)FileOffset + FileLength)
        {
            throw new InvalidDataException("Native slot bank leaves its current resource extent.");
        }
        return checked(SlotBaseOffset + index * 0x100);
    }
}

public static class NativeBattleWaterSlots
{
    public static NativeBattleEffectArena InitializeB65160(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index, ushort commandTickC, ushort commandTickE, ushort headerWord1C)
    {
        SlotAccess slot = new(arena, bank, index);
        slot.Arena = slot.Arena.Write(slot.Offset + 4, new byte[0xFC]);
        slot.Byte(0x92, 0x20);
        slot.Dword(0x18, 0x03000000);
        slot.Dword(0xCC, 0x00808080);
        slot.Word(0xC8, commandTickC);
        slot.Word(0xCA, commandTickE);
        slot.Word(0x9A, headerWord1C);
        return slot.Arena;
    }

    public static int LookupB65370(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int currentIndex, ImmutableArray<byte> activeOrder, int operand)
    {
        SlotAccess current = new(arena, bank, currentIndex);
        if (activeOrder.IsDefault || activeOrder.Length != 128 || !activeOrder.Contains((byte)0xFF))
        {
            throw new NotSupportedException("Native slot lookup requires its complete terminated active order.");
        }
        foreach (byte entry in activeOrder)
        {
            if (entry == 0xFF)
            {
                break;
            }
            _ = new SlotAccess(arena, bank, entry & 0x7F);
        }
        ushort selector = unchecked((ushort)operand);
        int id;
        if (selector < 0xFFF0)
        {
            id = (selector & 0x4000) != 0 ? (selector & 0x3FFF) + current.Byte(0x1B) : selector;
        }
        else
        {
            int kind = ~selector & 3;
            if (kind < 2)
            {
                id = current.Word(kind == 0 ? 0x14 : 0x16);
            }
            else
            {
                if (!Find(current.Word(0x16), out int parent))
                {
                    return 0;
                }
                SlotAccess reference = new(arena, bank, parent);
                id = reference.Word(kind == 2 ? 0x14 : 0x16);
            }
        }
        return Find(id, out int found) ? found : 0;

        bool Find(int wanted, out int foundIndex)
        {
            foreach (byte entry in activeOrder)
            {
                if (entry == 0xFF)
                {
                    break;
                }
                int index = entry & 0x7F;
                if (new SlotAccess(arena, bank, index).Word(0x12) == wanted)
                {
                    foundIndex = index;
                    return true;
                }
            }
            foundIndex = 0;
            return false;
        }
    }

    public static NativeBattleEffectArena CachePositionAA17D0(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index)
    {
        SlotAccess slot = new(arena, bank, index);
        for (int lane = 0; lane < 3; lane++)
        {
            slot.Word(0x8C + 2 * lane, unchecked((ushort)(slot.Dword(0x50 + 4 * lane) >> 16)));
        }
        return slot.Arena;
    }

    public static NativeBattleEffectArena RefreshAccelerationAA27F0(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index)
    {
        SlotAccess slot = new(arena, bank, index);
        byte flags = 0;
        for (int lane = 0; lane < 6; lane++)
        {
            if (slot.Word(0x80 + 2 * lane) != 0)
            {
                flags |= (byte)(lane < 3 ? 1 : 8);
            }
        }
        slot.Byte(0x1A, flags);
        return slot.Arena;
    }

    public static NativeBattleEffectArena UpdateSelected(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index, ImmutableArray<byte> activeOrder)
    {
        SlotAccess slot = new(arena, bank, index);
        switch (slot.Byte(0x18))
        {
            case 0:
            case 1:
                for (int lane = 0; lane < 3; lane++)
                {
                    slot.Word(0x94 + 2 * lane, unchecked((ushort)(slot.Dword(0x5C + 4 * lane) >> 16)));
                }
                break;
            case 7:
                uint updateBlock = unchecked((uint)slot.Dword(0xA0));
                if (updateBlock != 0)
                {
                    int blockOffset = NativeBattleWaterExecutionFields.ArenaOffset(updateBlock, 2);
                    short selector = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(blockOffset, 2));
                    int selectedIndex = LookupB65370(arena, bank, index, activeOrder, selector);
                    SlotAccess selected = new(arena, bank, selectedIndex);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        slot.Word(0x94 + lane * 2, unchecked((ushort)((slot.Dword(0x5C + lane * 4) >> 16) + selected.Word(0x94 + lane * 2))));
                    }
                }
                break;
            case 3:
                int referenceIndex = LookupB65370(arena, bank, index, activeOrder, unchecked((short)slot.Word(0xB0)));
                SlotAccess reference = new(arena, bank, referenceIndex);
                int angle = slot.Dword(0x64) >> 16;
                int radius = slot.Dword(0x5C) >> 8;
                int z = unchecked(radius * (NativeBattleCameraMath.Cosine(angle) >> 4)) >> 16;
                slot.Word(0x98, unchecked((ushort)(z + (short)reference.Word(0x98))));
                int x = unchecked(radius * (NativeBattleCameraMath.Sine(angle) >> 4)) >> 16;
                slot.Word(0x94, unchecked((ushort)(x + (short)reference.Word(0x94))));
                int y = slot.Dword(0x60) >> 16;
                slot.Word(0x96, unchecked((ushort)(y + (short)reference.Word(0x96))));
                break;
            default:
                throw new NotSupportedException("Native slot update requires a proved selected type.");
        }
        return slot.Arena;
    }

    public static NativeBattleEffectArena AdvancePhysicsA9BB90(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index, ImmutableArray<byte> activeOrder)
    {
        return UpdateSelected(IntegratePhysicsA9BB90(arena, bank, index), bank, index, activeOrder);
    }

    public static NativeBattleEffectArena IntegratePhysicsA9BB90(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index)
    {
        SlotAccess slot = new(arena, bank, index);
        byte flags = slot.Byte(0x1A);
        for (int lane = 0; lane < 6; lane++)
        {
            if ((flags & (lane < 3 ? 1 : 8)) != 0)
            {
                int acceleration = unchecked((short)slot.Word(0x80 + 2 * lane) << 12);
                slot.Dword(0x68 + 4 * lane, unchecked(slot.Dword(0x68 + 4 * lane) + acceleration));
            }
        }
        for (int lane = 0; lane < 6; lane++)
        {
            slot.Dword(0x50 + 4 * lane, unchecked(slot.Dword(0x50 + 4 * lane) + slot.Dword(0x68 + 4 * lane)));
        }
        return CachePositionAA17D0(slot.Arena, bank, index);
    }

    public static NativeBattleEffectArena ApplyVectorPostAction(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index, ImmutableArray<byte> activeOrder, NativeBattleWaterVectorPostAction action)
    {
        return action switch
        {
            NativeBattleWaterVectorPostAction.None => arena,
            NativeBattleWaterVectorPostAction.RefreshAcceleration => RefreshAccelerationAA27F0(arena, bank, index),
            NativeBattleWaterVectorPostAction.RefreshPosition => CachePositionAA17D0(UpdateSelected(arena, bank, index, activeOrder), bank, index),
            _ => throw new NotSupportedException("Native vector post-action is unsupported.")
        };
    }

    private sealed class SlotAccess
    {
        public NativeBattleEffectArena Arena { get; set; }
        public int Offset { get; }

        public SlotAccess(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index)
        {
            Offset = bank.SlotOffset(arena, index);
            Arena = arena;
        }

        public byte Byte(int offset) => Arena.Read(Offset + offset, 1)[0];
        public ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Arena.Read(Offset + offset, 2));
        public int Dword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Arena.Read(Offset + offset, 4));

        public void Byte(int offset, byte value)
        {
            Arena = Arena.Write(Offset + offset, new byte[] { value });
        }

        public void Word(int offset, ushort value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            Arena = Arena.Write(Offset + offset, bytes);
        }

        public void Dword(int offset, int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            Arena = Arena.Write(Offset + offset, bytes);
        }
    }
}
