using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect345State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableArray<NativeBattleEffectArena?> BankStorage, [property: JsonRequired] ImmutableDictionary<uint, ImmutableArray<byte>>? NodeData, [property: JsonRequired] bool ColdSourceOrigin)
{
    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleEffectOwnedModelState> OwnedModels { get; init; } = ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty;
    [JsonRequired]
    public bool OwnedModelSourceBound { get; init; }


    public static NativeBattleEffect345State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.Select(_ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()).ToImmutableArray(), null, true);

    public static NativeBattleEffect345State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))).ToImmutableArray(), null, false);
    }

    public NativeBattleEffect345State BindNodeSources(NativeBattleEffect345Resources resources)
    {
        if (NodeData is not null)
        {
            return this;
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect345 unknown node sources require an explicit original-owner migration.");
        }
        resources.Validate();
        return this with { NodeData = resources.Nodes.ToImmutableDictionary(bank => bank.Address, bank => bank.Data) };
    }

    public NativeBattleEffect345State ReplaceNodeBank(uint address, ImmutableArray<byte> bytes)
    {
        if (NodeData is null || !NodeData.ContainsKey(address) || bytes.IsDefault || bytes.Length != NodeLength(address))
        {
            throw new InvalidDataException("Effect345 current node writes require their bound exact source owner.");
        }
        return this with { NodeData = NodeData.SetItem(address, bytes) };
    }

    public byte[] Read(uint address, int length)
    {
        if (TryNodeSource(address, length, out uint nodeAddress))
        {
            ImmutableArray<byte> data = NodeData is not null && NodeData.TryGetValue(nodeAddress, out ImmutableArray<byte> current) ? current : throw new NotSupportedException("Effect345 requires its explicitly bound current node source.");
            return data.AsSpan().Slice(checked((int)(address - nodeAddress)), length).ToArray();
        }
        return Storage.Read(Require(address, length), length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect345State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (TryNodeSource(address, bytes.Length, out uint nodeAddress))
        {
            byte[] data = (NodeData is not null && NodeData.TryGetValue(nodeAddress, out ImmutableArray<byte> current) ? current : throw new NotSupportedException("Effect345 requires its bound current node source before writes.")).ToArray();
            bytes.CopyTo(data.AsSpan(checked((int)(address - nodeAddress))));
            return ReplaceNodeBank(nodeAddress, data.ToImmutableArray());
        }
        return this with { Storage = Storage.Write(Require(address, bytes.Length), bytes) };
    }

    public NativeBattleEffect345State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect345State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect345State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (TryNodeSource(address, length, out uint nodeAddress))
        {
            return NativeBattleEffectArena.Clear().Write(0, Read(address, length));
        }
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect345State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (TryNodeSource(address, length, out uint nodeAddress))
        {
            return Write(address, source.Read(0, length));
        }
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length) };
    }

    public NativeBattleEffectArena ReadBank(int index)
    {
        RequireBank(index);
        return BankStorage[index] ?? throw new InvalidDataException("Effect345 bank bytes are owned exclusively by the current hierarchy.");
    }

    public NativeBattleEffect345State DetachBank(int index)
    {
        _ = ReadBank(index);
        return this with { BankStorage = BankStorage.SetItem(index, null) };
    }

    public NativeBattleEffect345State AttachBank(int index, NativeBattleEffectArena bank)
    {
        RequireBank(index);
        if (BankStorage[index] is not null)
        {
            throw new InvalidDataException("Effect345 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, checked(Pools[index].Stride * Pools[index].Capacity));
        return this with { BankStorage = BankStorage.SetItem(index, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Storage.Regions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Storage.UnknownRegions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (BankStorage.IsDefault || BankStorage.Length != Pools.Length || OwnedModels is null)
        {
            throw new InvalidDataException("Effect345 process storage has different original owner dimensions.");
        }
        if (NodeData is not null)
        {
            if (NodeData.Count != 2 || !NodeData.ContainsKey(0x1643698) || !NodeData.ContainsKey(0x164808C))
            {
                throw new InvalidDataException("Effect345 requires both current node source owners.");
            }
            foreach ((uint address, ImmutableArray<byte> data) in NodeData)
            {
                if (data.IsDefault || data.Length != NodeLength(address))
                {
                    throw new InvalidDataException("Effect345 current node source has a different physical extent.");
                }
            }
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
            if (address != model.TaskAddress || model.ResourceAddress != NativeBattleEffect345Resources.ModelAddress)
            {
                throw new InvalidDataException("Effect345 decoded model sidecar has a different physical task or embedded resource.");
            }
        }
    }

    private static int NodeLength(uint address) => address switch { 0x1643698 => 18932, 0x164808C => 9708, _ => throw new InvalidDataException("Effect345 node address leaves its two selected sources.") };

    private static bool TryNodeSource(uint address, int length, out uint nodeAddress)
    {
        foreach (uint start in new uint[] { 0x1643698, 0x164808C })
        {
            if (length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)NodeLength(start))
            {
                nodeAddress = start;
                return true;
            }
        }
        nodeAddress = 0;
        return false;
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
        throw new InvalidDataException("Effect345 storage excludes pool banks, descriptors, COMMON, the shared view and unobserved gaps.");
    }

    private static void RequireBank(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect345 physical bank index leaves its eight original pools.");
        }
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect345 retained bank bytes leave their actual physical allocation.");
        }
    }
}
