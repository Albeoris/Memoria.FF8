using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect287State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] NativeBattleEffectArena? MeshQueueStorage)
{
    public static NativeBattleEffect287State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), NativeBattleEffectArena.Clear());

    public static NativeBattleEffect287State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), NativeBattleEffectArena.Clear().MarkUnknown(0, MeshQueueLength));
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect287State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (WithinMeshQueue(address, bytes.Length))
        {
            ReadMeshQueue().Write(checked((int)(address - MeshQueueAddress)), bytes);
            return this;
        }
        int offset = Require(address, bytes.Length);
        Storage.Write(offset, bytes);
        return this;
    }
    public NativeBattleEffect287State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect287State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect287State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (WithinMeshQueue(address, length))
        {
            return NativeBattleEffectNodeContextMemory.Copy(ReadMeshQueue(), checked((int)(address - MeshQueueAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect287State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (WithinMeshQueue(address, length))
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, ReadMeshQueue(), checked((int)(address - MeshQueueAddress)), length);
            return this;
        }
        int offset = Require(address, length);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length);
        return this;
    }

    public NativeBattleEffectArena ReadMeshQueue() => MeshQueueStorage ?? throw new InvalidDataException("Effect287 mesh queue is exclusively owned by its active pool.");

    public NativeBattleEffect287State DetachMeshQueue()
    {
        _ = ReadMeshQueue();
        return this with { MeshQueueStorage = null };
    }

    public NativeBattleEffect287State AttachMeshQueue(NativeBattleEffectArena queue)
    {
        if (MeshQueueStorage is not null)
        {
            throw new InvalidDataException("Effect287 cannot duplicate its retained mesh queue.");
        }
        ValidateBank(queue, MeshQueueLength);
        return this with { MeshQueueStorage = queue };
    }

    private static bool WithinMeshQueue(uint address, int length) => length >= 0 && address >= MeshQueueAddress && (ulong)address + (uint)length <= (ulong)MeshQueueAddress + MeshQueueLength;

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect287State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect287State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect287 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect287State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect287State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect287 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect287 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        if (MeshQueueStorage is { } queue)
        {
            ValidateBank(queue, MeshQueueLength);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateDataRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateUnknownRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect287 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect287 is missing a physical bank owner.");
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
        throw new InvalidDataException("Effect287 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect287 pool index leaves its two original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
