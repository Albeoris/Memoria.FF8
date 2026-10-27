using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect316State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks)
{
    public static NativeBattleEffect316State FromColdProcess() => new(NativeBattleEffectArena.Clear(), FixedPools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()));

    public static NativeBattleEffect316State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, FixedPools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))));
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect316State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        int offset = Require(address, bytes.Length);
        Storage.Write(offset, bytes);
        return this;
    }
    public NativeBattleEffect316State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect316State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect316State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect316State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        int offset = Require(address, length);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length);
        return this;
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect316State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect316State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect316 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect316State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect316State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = FixedPools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect316 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect316 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateDataRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateUnknownRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (FixedBanks is null || FixedBanks.Count != FixedPools.Length)
        {
            throw new InvalidDataException("Effect316 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in FixedPools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect316 is missing a physical bank owner.");
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
        throw new InvalidDataException("Effect316 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= FixedPools.Length)
        {
            throw new InvalidDataException("Effect316 pool index leaves its original fixed bank.");
        }
        return FixedPools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
