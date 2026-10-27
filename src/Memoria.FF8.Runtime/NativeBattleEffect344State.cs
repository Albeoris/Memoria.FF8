using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect344State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableArray<NativeBattleEffectArena?> BankStorage)
{
    public static NativeBattleEffect344State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.Select(_ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()).ToImmutableArray());

    public static NativeBattleEffect344State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))).ToImmutableArray());
    }

    public byte[] Read(uint address, int length)
    {
        return Storage.Read(Require(address, length), length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect344State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Storage.Write(Require(address, bytes.Length), bytes);
        return this;
    }

    public NativeBattleEffect344State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect344State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect344State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect344State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length);
        return this;
    }

    public NativeBattleEffectArena ReadBank(int index)
    {
        RequireBank(index);
        return BankStorage[index] ?? throw new InvalidDataException("Effect344 inactive bank is owned exclusively by the hierarchy.");
    }

    public NativeBattleEffect344State DetachBank(int index)
    {
        _ = ReadBank(index);
        return this with { BankStorage = BankStorage.SetItem(index, null) };
    }

    public NativeBattleEffect344State AttachBank(int index, NativeBattleEffectArena bank)
    {
        RequireBank(index);
        if (BankStorage[index] is not null)
        {
            throw new InvalidDataException("Effect344 cannot duplicate a retained physical bank.");
        }
        ValidateBank(bank, Pools[index].Stride * Pools[index].Capacity);
        return this with { BankStorage = BankStorage.SetItem(index, bank) };
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
        if (BankStorage.IsDefault || BankStorage.Length != Pools.Length)
        {
            throw new InvalidDataException("Effect344 storage requires its two original physical banks.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            if (BankStorage[index] is { } bank)
            {
                ValidateBank(bank, Pools[index].Stride * Pools[index].Capacity);
            }
        }
    }

    private void RequireBank(int index)
    {
        if ((uint)index >= Pools.Length || BankStorage.IsDefault || BankStorage.Length != Pools.Length)
        {
            throw new InvalidDataException("Effect344 bank index leaves its original physical owners.");
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
        throw new InvalidDataException("Effect344 storage excludes physical task banks, descriptors, shared owners and unobserved gaps.");
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
