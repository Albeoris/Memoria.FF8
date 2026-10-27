using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect80State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] ImmutableDictionary<uint, ImmutableArray<byte>> NodeBanks, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena> InitializedSources, [property: JsonRequired] bool ColdSourceOrigin)
{
    public static NativeBattleEffect80State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, NativeBattleEffectArena>.Empty, true);

    public static NativeBattleEffect80State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, NativeBattleEffectArena>.Empty, false);
    }

    public byte[] Read(uint address, int length)
    {
        return ReadView(address, length).Read(0, length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect80State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    }

    public NativeBattleEffect80State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect80State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect80State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        foreach ((uint start, int size) in InitializedSourceLengths)
        {
            if (Contains(start, size, address, length))
            {
                NativeBattleEffectArena source = InitializedSources.TryGetValue(start, out NativeBattleEffectArena? current) ? current : throw new NotSupportedException("Effect80 initialized source requires its actual current owner.");
                return NativeBattleEffectNodeContextMemory.Copy(source, checked((int)(address - start)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
        }
        foreach ((uint start, int size) in NodeSourceLengths)
        {
            if (Contains(start, size, address, length))
            {
                return NativeBattleEffectArena.Clear().Write(0, ReadNodeBank(start).AsSpan().Slice(checked((int)(address - start)), length));
            }
        }
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect80State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        foreach ((uint start, int size) in InitializedSourceLengths)
        {
            if (Contains(start, size, address, length))
            {
                NativeBattleEffectArena destination = InitializedSources.TryGetValue(start, out NativeBattleEffectArena? current) ? current : throw new NotSupportedException("Effect80 initialized writes require an explicitly bound current owner.");
                NativeBattleEffectNodeContextMemory.Copy(source, 0, destination, checked((int)(address - start)), length);
                return this;
            }
        }
        foreach ((uint start, int size) in NodeSourceLengths)
        {
            if (Contains(start, size, address, length))
            {
                ImmutableArray<byte> current = ReadNodeBank(start);
                byte[] bytes = current.ToArray();
                source.Read(0, length).CopyTo(bytes, checked((int)(address - start)));
                return ReplaceNodeBank(start, bytes.ToImmutableArray());
            }
        }
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length);
        return this;
    }

    public NativeBattleEffect80State BindNodeSource(NativeBattleEffect80Resources resources, uint address)
    {
        if (NodeBanks.ContainsKey(address))
        {
            _ = ReadNodeBank(address);
            return this;
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect80 cannot recreate an unknown imported node source from original assets.");
        }
        return ReplaceNodeBank(address, resources.ReadOriginalNodeBank(address));
    }

    public ImmutableArray<byte> ReadNodeBank(uint address) => NodeBanks.TryGetValue(address, out ImmutableArray<byte> bytes) && NodeSourceLengths.TryGetValue(address, out int length) && !bytes.IsDefault && bytes.Length == length ? bytes : throw new NotSupportedException("Effect80 requires its actual current addressed node bank.");

    public NativeBattleEffect80State ReplaceNodeBank(uint address, ImmutableArray<byte> bytes)
    {
        if (!NodeSourceLengths.TryGetValue(address, out int length) || bytes.IsDefault || bytes.Length != length)
        {
            throw new InvalidDataException("Effect80 node source changed its original physical dimensions.");
        }
        return this with { NodeBanks = NodeBanks.SetItem(address, bytes) };
    }

    public NativeBattleEffect80State BindInitializedSource(NativeBattleEffect80Resources resources, uint address)
    {
        if (InitializedSources.ContainsKey(address))
        {
            return this;
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect80 cannot replace unknown imported initialized data with a cold template.");
        }
        if (!InitializedSourceLengths.TryGetValue(address, out int length))
        {
            throw new InvalidDataException("Effect80 initialized source address is unsupported.");
        }
        ImmutableArray<byte> bytes = resources.ReadOriginalInitializedSource(address);
        if (bytes.Length != length)
        {
            throw new InvalidDataException("Effect80 initialized source changed its exact original dimensions.");
        }
        return this with { InitializedSources = InitializedSources.Add(address, NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan())) };
    }

    private static bool Contains(uint start, int size, uint address, int length) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect80State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect80State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect80 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect80State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect80State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect80 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect80 cannot duplicate an already retained physical bank.");
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
        if (NodeBanks is null || InitializedSources is null)
        {
            throw new InvalidDataException("Effect80 requires explicit mutable source dictionaries.");
        }
        foreach ((uint address, ImmutableArray<byte> bytes) in NodeBanks)
        {
            if (!NodeSourceLengths.TryGetValue(address, out int length) || bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect80 current node bank leaves its original physical source.");
            }
        }
        foreach ((uint address, NativeBattleEffectArena source) in InitializedSources)
        {
            if (!InitializedSourceLengths.TryGetValue(address, out int length))
            {
                throw new InvalidDataException("Effect80 initialized owner is not an original source front.");
            }
            ValidateBank(source, length);
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect80 storage has different physical bank dimensions.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect80 is missing an original physical bank owner.");
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
        throw new InvalidDataException("Effect80 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect80 pool index leaves its nine original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
