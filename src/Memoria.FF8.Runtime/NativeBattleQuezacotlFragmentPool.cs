using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlFragmentWrite(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

/// <summary>6C4725/6C4732's32x2C tasks live in the current EXE resource bank, independently of the effect arena.</summary>
public static class NativeBattleQuezacotlFragmentPool
{
    public const uint Descriptor = 0x25216E8;
    public const uint Bank = 0x12A53E0;
    public const ushort Stride = 0x2C;
    public const ushort Capacity = 32;
    public const uint Callback = 0x6C6660;

    public static NativeBattleQuezacotlFragmentWrite Initialize6C4725(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleQuezacotlExecutableMemory executable = Executable(memory);
        //508330..46 clears every stride DWORD and remainder BYTE, not only the allocation flag WORD.
        executable = executable.Write(Bank, new byte[Capacity * Stride]);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(Descriptor, Bank, Stride, Capacity);
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C4725 requires its current secondary descriptor owners.");
        memory = memory with { QuezacotlExecutableMemory = executable, QuezacotlSecondaryHeads = heads.SetItem(4, 0) };
        return new(memory, effect with { Pools = effect.Pools.Where(existing => existing.DescriptorAddress != Descriptor).Append(pool).ToImmutableArray() });
    }

    public static NativeBattleQuezacotlFragmentWrite SpawnTwo6C4732(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.SingleOrDefault(existing => existing.DescriptorAddress == Descriptor) ?? throw new NotSupportedException("Native6C4732 requires its initialized current EXE task pool.");
        Validate(memory, pool);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (!pool.Slots.Any(task => (task.Flags & 1) == 0))
            {
                continue;
            }
            int? previousTail = pool.Tail;
            (pool, int index) = pool.Append(Callback);
            byte[] raw = ReadTask(memory, index).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(raw, pool.Slots[index].Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), Callback);
            Word(0x0C, 0);
            Word(0x10, (short)((Next() & 0xFFF) - 2048));
            Word(0x12, 0);
            Word(0x14, unchecked((short)((Next() & 0xFFF) + (effect.Quezacotl?.TargetZ1770 ?? throw new NotSupportedException("Native6C476A requires its current targetZ1770 DWORD.")) - 2048)));
            Word(0x18, (short)((Next() & 0x3F) - 32));
            Word(0x1C, (short)((Next() & 0x3F) - 32));
            Word(0x1A, (short)(-128 - (Next() & 0x7F)));
            Word(0x20, unchecked((short)Next()));
            Word(0x22, unchecked((short)Next()));
            Word(0x24, unchecked((short)Next()));
            raw[0x28] = unchecked((byte)Next());
            raw[0x29] = unchecked((byte)Next());
            raw[0x2A] = unchecked((byte)Next());
            memory = WriteTask(memory, index, raw.ToImmutableArray());
            if (previousTail is int tail)
            {
                memory = WriteDword(memory, Bank + (uint)tail * Stride + 4, Bank + (uint)index * Stride);
            }
            pool = pool.WriteSlot(index, pool.Slots[index].WriteRetainedBytes(12, raw.AsSpan(12)));

            ushort Next()
            {
                memory = memory.DrawSharedEffectRandom7059E0(out ushort random);
                return random;
            }

            void Word(int offset, short value)
            {
                BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(offset), value);
            }
        }
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C4732 requires its current secondary descriptor heads.");
        memory = memory with { QuezacotlSecondaryHeads = heads.SetItem(4, pool.Head is int head ? Bank + (uint)head * Stride : 0) };
        return new(memory, effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == Descriptor ? pool : existing).ToImmutableArray() });
    }

    public static ImmutableArray<byte> ReadTask(NativeBattleEffectMemory memory, int index)
    {
        return Executable(memory).Data.AsSpan().Slice(Offset(index), Stride).ToArray().ToImmutableArray();
    }

    public static NativeBattleEffectMemory WriteTask(NativeBattleEffectMemory memory, int index, ImmutableArray<byte> task)
    {
        if (task.IsDefault || task.Length != Stride)
        {
            throw new InvalidDataException("Native6C6660 requires its actual2C-byte current EXE task.");
        }
        _ = Offset(index);
        return memory with { QuezacotlExecutableMemory = Executable(memory).Write(Bank + (uint)index * Stride, task.AsSpan()) };
    }

    public static NativeBattleEffectMemory WriteDword(NativeBattleEffectMemory memory, uint address, uint value)
    {
        Span<byte> raw = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
        return memory with { QuezacotlExecutableMemory = Executable(memory).Write(address, raw) };
    }

    public static void Validate(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        if (pool.DescriptorAddress != Descriptor || pool.BankAddress != Bank || pool.Stride != Stride || pool.Slots.Length != Capacity)
        {
            throw new InvalidDataException("Native6C4732 lost its original32x2C current EXE task bank.");
        }
    }

    public static void Validate(NativeBattleEffectMemory memory, NativeBattleEffectTaskPool pool)
    {
        ValidateHead(memory, pool);
        NativeBattleQuezacotlExecutableMemory executable = Executable(memory);
        for (int index = 0; index < Capacity; index++)
        {
            ReadOnlySpan<byte> raw = executable.Data.AsSpan().Slice(Offset(index), Stride);
            NativeBattleModulePoolTask task = pool.Slots[index];
            uint next = task.Next is int linked ? Bank + (uint)linked * Stride : 0;
            if (BinaryPrimitives.ReadUInt16LittleEndian(raw) != task.Flags || BinaryPrimitives.ReadUInt16LittleEndian(raw[2..]) != task.State || BinaryPrimitives.ReadUInt32LittleEndian(raw[4..]) != next || BinaryPrimitives.ReadUInt32LittleEndian(raw[8..]) != task.Callback || !raw[12..].SequenceEqual(task.ReadRawPayloadSpan()))
            {
                throw new InvalidDataException("Native fragment typed task disagrees with its current physical EXE bytes.");
            }
        }
    }

    public static void ValidateHead(NativeBattleEffectMemory memory, NativeBattleEffectTaskPool pool)
    {
        Validate(pool);
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native fragment pool requires its retained descriptor head DWORD.");
        uint head = pool.Head is int indexHead ? Bank + (uint)indexHead * Stride : 0;
        if (heads.IsDefault || heads.Length != 5 || heads[4] != head)
        {
            throw new InvalidDataException("Native fragment descriptor head disagrees with its current typed pool.");
        }
    }

    private static NativeBattleQuezacotlExecutableMemory Executable(NativeBattleEffectMemory memory)
    {
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native Quezacotl fragments require their current mutable EXE resource bank.");
        executable.Validate();
        return executable;
    }

    private static int Offset(int index)
    {
        if ((uint)index >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return checked((int)(Bank - NativeBattleQuezacotlExecutableMemory.Address) + index * Stride);
    }
}
