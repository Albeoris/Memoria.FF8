using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableArray<NativeBattleEffectArena?> BankStorage, [property: JsonRequired] NativeBattleNodeSource? NodeData, [property: JsonRequired] bool ColdSourceOrigin)
{
    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleEffectOwnedModelState> OwnedModels { get; init; } = ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty;
    [JsonRequired]
    public bool OwnedModelSourceBound { get; init; }


    public static NativeBattleEffect83State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.Select(_ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()).ToImmutableArray(), null, true);

    public static NativeBattleEffect83State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))).ToImmutableArray(), null, false);
    }

    public NativeBattleEffect83State BindNodeSource(NativeBattleEffect83Resources resources)
    {
        if (NodeData is not null)
        {
            return this;
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect83's old unknown node source requires an explicit original-owner migration.");
        }
        resources.Nodes.Validate();
        return this with { NodeData = resources.Nodes.Data };
    }

    public NativeBattleEffect83State ReplaceNodeBank(NativeBattleNodeSource bytes)
    {
        if (NodeData is null || bytes.IsDefault || bytes.Length != NodeSourceLength)
        {
            throw new InvalidDataException("Effect83 current node writes require their already bound exact source owner.");
        }
        return this with { NodeData = bytes };
    }

    public byte[] Read(uint address, int length)
    {
        if (InNodeSource(address, length))
        {
            NativeBattleNodeSource data = NodeData ?? throw new NotSupportedException("Effect83 requires its explicitly bound current node source.");
            return data.Read(checked((int)(address - NodeSourceAddress)), length);
        }
        return Storage.Read(Require(address, length), length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect83State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (InNodeSource(address, bytes.Length))
        {
            (NodeData ?? throw new NotSupportedException("Effect83 requires its bound current node source before writes.")).Write(checked((int)(address - NodeSourceAddress)), bytes);
            return this;
        }
        Storage.Write(Require(address, bytes.Length), bytes);
        return this;
    }

    public NativeBattleEffect83State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect83State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect83State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (InNodeSource(address, length))
        {
            return NativeBattleEffectArena.Clear().Write(0, Read(address, length));
        }
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect83State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (InNodeSource(address, length))
        {
            return Write(address, source.Read(0, length));
        }
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length);
        return this;
    }

    public NativeBattleEffectArena ReadBank(int index)
    {
        RequireBank(index);
        return BankStorage[index] ?? throw new InvalidDataException("Effect83 bank bytes are owned exclusively by the current hierarchy.");
    }

    public NativeBattleEffect83State DetachBank(int index)
    {
        _ = ReadBank(index);
        return this with { BankStorage = BankStorage.SetItem(index, null) };
    }

    public NativeBattleEffect83State AttachBank(int index, NativeBattleEffectArena bank)
    {
        RequireBank(index);
        if (BankStorage[index] is not null)
        {
            throw new InvalidDataException("Effect83 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, checked(Pools[index].Stride * Pools[index].Capacity));
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
        if (BankStorage.IsDefault || BankStorage.Length != Pools.Length || OwnedModels is null || NodeData is { } bytes && (bytes.IsDefault || bytes.Length != NodeSourceLength))
        {
            throw new InvalidDataException("Effect83 process storage has different original owner dimensions.");
        }
        for (int index = 0; index < BankStorage.Length; index++)
        {
            if (BankStorage[index] is { } bank)
            {
                ValidateBank(bank, checked(Pools[index].Stride * Pools[index].Capacity));
            }
        }
        foreach ((uint address, NativeBattleEffectOwnedModelState model) in OwnedModels)
        {
            if (address != model.TaskAddress || model.ResourceAddress != ModelAddress)
            {
                throw new InvalidDataException("Effect83 decoded model sidecar has a different physical task or embedded resource.");
            }
        }
    }

    private static bool InNodeSource(uint address, int length) => length >= 0 && address >= NodeSourceAddress && (ulong)address + (uint)length <= (ulong)NodeSourceAddress + NodeSourceLength;

    private static int Require(uint address, int length)
    {
        if (length < 0 || !OwnedRanges.Any(range => address >= range.Address && (ulong)address + (uint)length <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Effect83 storage excludes pool banks, descriptors, COMMON, the shared view and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }

    private static void RequireBank(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect83 physical bank index leaves its eight original pools.");
        }
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
