using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect320State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> CommonQueueStorage)
{
    [JsonRequired]
    public bool? MutableAnchorSourceBound { get; init; }

    public static NativeBattleEffect320State FromColdProcess()
    {
        return new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), CommonPools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear())) { MutableAnchorSourceBound = false };
    }

    public static NativeBattleEffect320State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(Require(address, length), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), CommonPools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, CommonQueueLength)));
    }

    public byte[] Read(uint address, int length)
    {
        return ReadView(address, length).Read(0, length);
    }
    public byte[] ReadBytes(uint address, int length)
    {
        return Read(address, length);
    }
    public ushort ReadWord(uint address)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    }
    public uint ReadDword(uint address)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    }
    public NativeBattleEffect320State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    }
    public NativeBattleEffect320State WriteBytes(uint address, ReadOnlySpan<byte> bytes)
    {
        return Write(address, bytes);
    }
    public NativeBattleEffect320State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect320State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (WithinCommonQueue(address, length, out uint descriptor))
        {
            return NativeBattleEffectNodeContextMemory.Copy(ReadCommonQueue(descriptor), checked((int)(address - descriptor)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect320State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (WithinCommonQueue(address, length, out uint descriptor))
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, ReadCommonQueue(descriptor), checked((int)(address - descriptor)), length);
            return this;
        }
        int offset = Require(address, length);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length);
        return this;
    }

    public NativeBattleEffectArena ReadCommonQueue(uint descriptor)
    {
        return CommonQueueStorage.TryGetValue(descriptor, out NativeBattleEffectArena? queue) && queue is not null ? queue : throw new InvalidDataException("Effect320 COMMON queue is exclusively owned by its active pool.");
    }

    public NativeBattleEffect320State DetachCommonQueue(uint descriptor)
    {
        _ = ReadCommonQueue(descriptor);
        return this with { CommonQueueStorage = CommonQueueStorage.SetItem(descriptor, null) };
    }

    public NativeBattleEffect320State AttachCommonQueue(uint descriptor, NativeBattleEffectArena queue)
    {
        if (!CommonQueueStorage.TryGetValue(descriptor, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect320 cannot duplicate its retained COMMON queue.");
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

    public NativeBattleEffectArena ReadBank(int index)
    {
        return ReadBank(BankAddress(index));
    }
    public NativeBattleEffect320State DetachBank(int index)
    {
        return DetachBank(BankAddress(index));
    }
    public NativeBattleEffect320State AttachBank(int index, NativeBattleEffectArena bank)
    {
        return AttachBank(BankAddress(index), bank);
    }

    public NativeBattleEffectArena ReadBank(uint address)
    {
        return FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect320 inactive bank is missing or owned exclusively by the hierarchy.");
    }

    public NativeBattleEffect320State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect320State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect320 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect320 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        if (CommonQueueStorage is null || CommonQueueStorage.Count != CommonPools.Length)
        {
            throw new InvalidDataException("Effect320 requires its two exact COMMON queue owners.");
        }
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            if (!CommonQueueStorage.TryGetValue(shape.Descriptor, out NativeBattleEffectArena? queue))
            {
                throw new InvalidDataException("Effect320 is missing a COMMON queue owner.");
            }
            if (queue is not null)
            {
                ValidateBank(queue, CommonQueueLength);
            }
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateDataRanges())
        {
            RequireStorage(region.Offset, region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateUnknownRanges())
        {
            RequireStorage(region.Offset, region.Length);
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect320 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect320 is missing a physical bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
    }

    private static int Require(uint address, int length)
    {
        int packed = 0;
        foreach ((uint start, int size) in OwnedRanges)
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size)
            {
                return checked(packed + (int)(address - start));
            }
            packed = checked(packed + size);
        }
        throw new InvalidDataException("Effect320 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static void RequireStorage(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > StorageLength - length)
        {
            throw new InvalidDataException("Effect320 packed storage exceeds its three exact sparse owners.");
        }
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect320 pool index leaves its original fixed bank.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
