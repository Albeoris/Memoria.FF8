using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect31State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedQueues, [property: JsonRequired] bool ColdSourceOrigin, [property: JsonRequired] bool StaticSourcesBound)
{
    [JsonRequired]
    public NativeBattleStackBytes? CallbackStack { get; init; }

    public NativeBattleStackBytes ReadCallbackStack() => CallbackStack ?? throw new NotSupportedException("Effect31 requires its retained callback stack owner.");

    public static NativeBattleEffect31State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), Pools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), true, false) { CallbackStack = NativeBattleEffect31Stack.Begin() };

    public static NativeBattleEffect31State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), Pools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, 8)), false, false) { CallbackStack = NativeBattleEffect31Stack.Begin() };
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect31State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Storage.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleEffect31State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect31State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleEffect31State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length);
        return this;
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(Pools[index].Bank);
    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect31 physical bank is missing or owned by its active hierarchy.");
    public NativeBattleEffectArena ReadQueue(uint address) => FixedQueues.TryGetValue(address, out NativeBattleEffectArena? queue) && queue is not null ? queue : throw new InvalidDataException("Effect31 queue is missing or owned by its active hierarchy.");

    public NativeBattleEffect31State DetachBank(int index)
    {
        uint address = Pools[index].Bank;
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect31State DetachQueue(uint address)
    {
        _ = ReadQueue(address);
        return this with { FixedQueues = FixedQueues.SetItem(address, null) };
    }

    public NativeBattleEffect31State AttachBank(int index, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools[index];
        if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect31 physical bank already has an inactive owner.");
        }
        ValidateExtent(bank, checked(shape.Stride * shape.Capacity));
        return this with { FixedBanks = FixedBanks.SetItem(shape.Bank, bank) };
    }

    public NativeBattleEffect31State AttachQueue(uint address, NativeBattleEffectArena queue)
    {
        if (!FixedQueues.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect31 queue already has an inactive owner.");
        }
        ValidateExtent(queue, 8);
        return this with { FixedQueues = FixedQueues.SetItem(address, queue) };
    }

    public void Validate()
    {
        Storage.Validate();
        NativeBattleEffect31Stack.Validate(ReadCallbackStack());
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateDataRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateUnknownRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (FixedBanks is null || FixedQueues is null || FixedBanks.Count != Pools.Length || FixedQueues.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect31 requires all seven physical bank and queue owners.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank) || !FixedQueues.TryGetValue(shape.Descriptor, out NativeBattleEffectArena? queue))
            {
                throw new InvalidDataException("Effect31 physical bank or queue owner is absent.");
            }
            if (bank is not null)
            {
                ValidateExtent(bank, checked(shape.Stride * shape.Capacity));
            }
            if (queue is not null)
            {
                ValidateExtent(queue, 8);
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
                if (cursor <= (ulong)start + (uint)size)
                {
                    cursor = Math.Max(cursor, (ulong)start + (uint)size);
                    if (cursor >= end)
                    {
                        return checked((int)(address - BaseAddress));
                    }
                }
            }
        }
        throw new InvalidDataException("Effect31 storage excludes task banks, queues, external streams and unobserved gaps.");
    }

    private static void ValidateExtent(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }
}
