using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Reserves engine-owned addresses for hosted task banks.</summary>
public static class NativeBattlePresentationTaskStorage
{
    private const uint StartAddress = 0x80000000;
    private const uint EndAddress = 0xF0000000;
    private const uint BankAlignment = 16;

    public static (NativeBattleEffectMemory Memory, uint BankAddress) Reserve(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, ushort stride, int capacity)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.PresentationRoster is not { } roster)
        {
            throw new InvalidDataException("Hosted task storage requires the current effect's presentation roster.");
        }
        roster.Validate();
        if (!ValidStride(stride))
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }
        if (!ValidCapacity(capacity))
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        uint byteLength = checked((uint)stride * (uint)capacity);
        ValidateCursor(memory.PresentationTaskNextAddress);
        uint next = memory.PresentationTaskNextAddress ?? StartAddress;
        next = FindBankEnd(next, effect.Pools);
        if (memory.DetachedReturnedTask is { } detached)
        {
            next = FindBankEnd(next, detached.Pools);
        }
        uint address = AlignBank(next);
        if (address >= EndAddress || byteLength > EndAddress - address)
        {
            throw new InvalidOperationException("Hosted task storage has no remaining address space for the requested bank.");
        }
        return (memory with { PresentationTaskNextAddress = AlignBank(address + byteLength) }, address);
    }

    public static bool IsHostedBank(uint address, ushort stride, int capacity)
    {
        return address is >= StartAddress and < EndAddress
            && (address & (BankAlignment - 1)) == 0
            && ValidStride(stride)
            && ValidCapacity(capacity)
            && (ulong)address + (ulong)stride * (uint)capacity <= EndAddress;
    }

    internal static void ValidateCursor(uint? address)
    {
        if (address is uint cursor && (cursor is < StartAddress or > EndAddress || (cursor & (BankAlignment - 1)) != 0))
        {
            throw new InvalidDataException("Hosted task storage cursor leaves its aligned address range.");
        }
    }

    private static uint FindBankEnd(uint next, ImmutableArray<NativeBattleEffectTaskPool> pools)
    {
        if (pools.IsDefault)
        {
            throw new InvalidDataException("Hosted task storage requires the current effect's owned pools.");
        }
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            if (pool is null)
            {
                throw new InvalidDataException("Hosted task storage requires the current effect's owned pools.");
            }
            ulong end = (ulong)pool.BankAddress + (ulong)pool.Stride * (uint)pool.Slots.Length;
            if (pool.BankAddress >= EndAddress || (pool.BankAddress < StartAddress && end <= StartAddress))
            {
                continue;
            }
            if (!IsHostedBank(pool.BankAddress, pool.Stride, pool.Slots.Length))
            {
                throw new InvalidDataException("A retained hosted task bank leaves its aligned address range.");
            }
            next = Math.Max(next, checked((uint)end));
        }
        return next;
    }

    private static bool ValidStride(ushort stride) => stride >= 12;

    private static bool ValidCapacity(int capacity) => capacity is >= 1 and <= ushort.MaxValue;

    private static uint AlignBank(uint address) => checked(address + (BankAlignment - 1)) & ~(BankAlignment - 1);
}
