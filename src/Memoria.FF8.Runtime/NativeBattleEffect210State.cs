using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect210State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> CommonQueueStorage)
{
    public static NativeBattleEffect210State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), CommonPools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()));

    public static NativeBattleEffect210State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), CommonPools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, CommonQueueLength)));
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect210State Write(uint address, ReadOnlySpan<byte> bytes) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    public NativeBattleEffect210State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect210State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect210State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (WithinCommonQueue(address, length, out uint descriptor))
        {
            return NativeBattleEffectNodeContextMemory.Copy(ReadCommonQueue(descriptor), checked((int)(address - descriptor)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect210State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (WithinCommonQueue(address, length, out uint descriptor))
        {
            return this with { CommonQueueStorage = CommonQueueStorage.SetItem(descriptor, NativeBattleEffectNodeContextMemory.Copy(source, 0, ReadCommonQueue(descriptor), checked((int)(address - descriptor)), length)) };
        }
        int offset = Require(address, length);
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length) };
    }

    public NativeBattleEffectArena ReadCommonQueue(uint descriptor) => CommonQueueStorage.TryGetValue(descriptor, out NativeBattleEffectArena? queue) && queue is not null ? queue : throw new InvalidDataException("Effect210 COMMON queue is exclusively owned by its active pool.");

    public NativeBattleEffect210State DetachCommonQueue(uint descriptor)
    {
        _ = ReadCommonQueue(descriptor);
        return this with { CommonQueueStorage = CommonQueueStorage.SetItem(descriptor, null) };
    }

    public NativeBattleEffect210State AttachCommonQueue(uint descriptor, NativeBattleEffectArena queue)
    {
        if (!CommonQueueStorage.TryGetValue(descriptor, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect210 cannot duplicate its retained COMMON queue.");
        }
        ValidateBank(queue, CommonQueueLength);
        return this with { CommonQueueStorage = CommonQueueStorage.SetItem(descriptor, queue) };
    }

    private static bool WithinCommonQueue(uint address, int length, out uint descriptor)
    {
        foreach (NativeBattleCureFamilyPool pool in CommonPools)
        {
            if (length >= 0 && address >= pool.Descriptor && (ulong)address + (uint)length <= (ulong)pool.Descriptor + CommonQueueLength)
            {
                descriptor = pool.Descriptor;
                return true;
            }
        }
        descriptor = 0;
        return false;
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect210State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect210State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect210 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect210State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect210State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect210 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect210 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        if (CommonQueueStorage is null || CommonQueueStorage.Count != CommonPools.Length)
        {
            throw new InvalidDataException("Effect210 requires its six exact COMMON queue owners.");
        }
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            if (!CommonQueueStorage.TryGetValue(shape.Descriptor, out NativeBattleEffectArena? queue))
            {
                throw new InvalidDataException("Effect210 is missing a COMMON queue owner.");
            }
            if (queue is not null)
            {
                ValidateBank(queue, CommonQueueLength);
            }
        }
        foreach (NativeBattleEffectMemoryRegion region in Storage.Regions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Storage.UnknownRegions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect210 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect210 is missing a physical bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
    }

    private static int Require(uint address, int length)
    {
        if (length >= 0)
        {
            ulong cursor = address;
            ulong end = (ulong)address + (uint)length;
            foreach ((uint start, int size) in OwnedRanges)
            {
                if (cursor < start)
                {
                    break;
                }
                if (cursor >= start && cursor <= (ulong)start + (uint)size)
                {
                    cursor = Math.Max(cursor, (ulong)start + (uint)size);
                    if (cursor >= end)
                    {
                        return checked((int)(address - BaseAddress));
                    }
                }
            }
        }
        throw new InvalidDataException("Effect210 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect210 pool index leaves its two original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect210 retained bytes leave their physical bank allocation.");
        }
    }
}
