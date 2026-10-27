using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlRibbonSpawn(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, int? Slot);

/// <summary>6C87B0 appends6C8850 into the actual16x80 bank, retaining all fields not written by508360 or the producer.</summary>
public static class NativeBattleQuezacotlRibbonProducer
{
    public static NativeBattleQuezacotlRibbonSpawn Spawn6C87B0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, int localFrame)
    {
        if (localFrame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(localFrame));
        }
        return Spawn6C87B0(memory, effect, checked(0x12AE7F0u + (uint)(localFrame % 10) * 8), 0x12AE840, 12, 7, 256);
    }

    public static NativeBattleQuezacotlRibbonSpawn Spawn6C87B0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, uint pairAddress, uint controlAddress, byte tangentShift, byte decayShift, short step)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C87B0 requires its initialized effect arena.");
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521728);
        pool.Validate();
        if (pool.BankAddress != 0x211B514 || pool.Stride != 0x80 || pool.Slots.Length != 16)
        {
            throw new InvalidDataException("Native6C87B0 lost its phase86 ribbon bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            //508360 reports the exhausted bank and returns NULL;6C87B0 performs no stores in that branch.
            return new(memory, effect, null);
        }
        int? previousTail = pool.Tail;
        (pool, int index) = pool.Append(0x6C8850);
        int address = checked((int)(pool.BankAddress - 0x20DFAB8) + index * pool.Stride);
        //508360 plus6C87B0 cover all128 final bytes, retaining the current flags through the slot owner.
        byte[] raw = new byte[pool.Stride];
        BinaryPrimitives.WriteUInt16LittleEndian(raw, pool.Slots[index].Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), 0x6C8850);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0xC), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0xE), 0);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x10), step);
        raw[0x12] = tangentShift;
        raw[0x13] = decayShift;
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x14), pairAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x18), controlAddress);
        arena.Read(0x8C, 4).CopyTo(raw, 0x1C);
        for (int point = 0; point < 16; point++)
        {
            short width = unchecked((short)(260 * NativeBattleCameraMath.Sine(point * 2048 / 15) >> 12));
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x20 + point * 6), width);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x22 + point * 6), 128);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x24 + point * 6), 0);
        }
        arena = arena.Write(address, raw);
        if (previousTail is int tail)
        {
            byte[] pointer = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(pointer, checked(pool.BankAddress + (uint)index * pool.Stride));
            arena = arena.Write(checked((int)(pool.BankAddress - 0x20DFAB8) + tail * pool.Stride + 4), pointer);
        }
        pool = pool.WriteSlot(index, pool.Slots[index].WriteRetainedBytes(12, raw.AsSpan(12)));
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C87B0 requires its retained secondary descriptor heads.");
        heads = heads.SetItem(0, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0);
        return new(memory with { Arena = arena, QuezacotlSecondaryHeads = heads }, effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() }, index);
    }
}
