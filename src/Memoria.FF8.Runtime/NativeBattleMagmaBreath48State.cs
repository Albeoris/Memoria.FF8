using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMagmaBreath48State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena SourceHeader, [property: JsonRequired] NativeBattleEffectArena Channels, [property: JsonRequired] NativeBattleEffectArena MutablePe, [property: JsonRequired] ImmutableArray<NativeBattleEffectArena?> InactiveBanks, [property: JsonRequired] bool? SourcesLoaded)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(0x26481B4, 10), (0x26481D4, 8), (0x26489A8, 2), (0x26489AC, 12), (0x26495B8, 16), (0x26495D0, 4), (0x26495D8, 20)];

    public static NativeBattleMagmaBreath48State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, SourceLength), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, MutablePeLength), Pools.Select(_ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()).ToImmutableArray(), false);

    public static NativeBattleMagmaBreath48State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        NativeBattleEffectArena channels = NativeBattleEffectArena.Clear();
        foreach (NativeBattleMagmaBreath48Range region in ChannelRegions)
        {
            channels = channels.MarkUnknown(checked((int)(region.Address - ChannelBase)), region.Length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, SourceLength), channels, NativeBattleEffectArena.Clear().MarkUnknown(0, MutablePeLength), Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, pool.Stride * pool.Capacity)).ToImmutableArray(), null);
    }

    public NativeBattleMagmaBreath48State EnsureSources(NativeBattleMagmaBreath48Resources resources)
    {
        Validate();
        if (SourcesLoaded is null)
        {
            throw new NotSupportedException("Magma Breath current original source owners are unknown; original seeds cannot replace imported state.");
        }
        if (SourcesLoaded == true)
        {
            return this;
        }
        resources.Validate();
        NativeBattleTaskOperation.AdmitCurrentRetainedArena(SourceHeader);
        NativeBattleTaskOperation.AdmitCurrentRetainedArena(MutablePe);
        using NativeBattleEffectArenaTransaction header = new(SourceHeader);
        using NativeBattleEffectArenaTransaction meshes = new(MutablePe);
        header.View.Write(0, resources.Executable.Data.AsSpan());
        meshes.View.Write(0, resources.ReadMesh(MeshAAddress).Bytes.AsSpan());
        meshes.View.Write(MeshLength, resources.ReadMesh(MeshBAddress).Bytes.AsSpan());
        NativeBattleEffectArenaTransaction.CommitGroup([header, meshes]);
        return this with { SourcesLoaded = true };
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateUnknownRanges())
        {
            RequireGlobal(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        Channels.Validate();
        foreach (NativeBattleEffectStorageRange region in Channels.EnumerateDataRanges())
        {
            RequireChannel(checked(ChannelBase + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Channels.EnumerateUnknownRanges())
        {
            RequireChannel(checked(ChannelBase + (uint)region.Offset), region.Length);
        }
        ValidateBounded(SourceHeader, SourceLength);
        ValidateBounded(MutablePe, MutablePeLength);
        if (SourcesLoaded == true && SourceHeader.ReadUInt32(0) != 1)
        {
            throw new InvalidDataException("Magma Breath requires its retained original flag-one absolute source header.");
        }
        if (InactiveBanks.IsDefault || InactiveBanks.Length != Pools.Length)
        {
            throw new InvalidDataException("Magma Breath requires its six exclusive inactive hierarchy banks.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            if (InactiveBanks[index] is { } bank)
            {
                ValidateBounded(bank, Pools[index].Stride * Pools[index].Capacity);
            }
        }
    }

    public byte[] Read(uint address, int length)
    {
        var owner = Resolve(address, length);
        return owner.Arena.Read(owner.Offset, length);
    }
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        var owner = Resolve(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(owner.Arena, owner.Offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
    public NativeBattleMagmaBreath48State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        var owner = Resolve(address, bytes.Length);
        owner.Arena.Write(owner.Offset, bytes);
        return this;
    }
    public NativeBattleMagmaBreath48State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleMagmaBreath48State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleMagmaBreath48State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleMagmaBreath48State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        var owner = Resolve(address, length);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, owner.Arena, owner.Offset, length);
        return this;
    }
    public NativeBattleStaticMeshResource ReadMesh(NativeBattleMagmaBreath48Resources resources, uint address)
    {
        NativeBattleStaticMeshResource seed = resources.ReadMesh(address);
        NativeBattleEffectArena view = ReadView(address, seed.Bytes.Length);
        byte[] header = view.Read(0, 8);
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (vertices != 182 || BinaryPrimitives.ReadInt32LittleEndian(header) != 8 + vertices * 8)
        {
            throw new InvalidDataException("Magma Breath current geometry must retain its original vertex header.");
        }
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, seed.Bytes.Length);
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index] && !(index >= 8 && index < 8 + vertices * 8 && ((index - 8) & 7) >= 6))
            {
                throw new NotSupportedException("Magma Breath current geometry consumes an unobserved header, coordinate or face byte.");
            }
        }
        return seed with { Bytes = bytes.ToImmutableArray() };
    }

    private (NativeBattleEffectArena Arena, int Offset, int Kind) Resolve(uint address, int length)
    {
        if (Contains(SourceAddress, SourceLength, address, length))
        {
            RequireLoaded();
            return (SourceHeader, checked((int)(address - SourceAddress)), 1);
        }
        if (Contains(MeshAAddress, MutablePeLength, address, length))
        {
            RequireLoaded();
            return (MutablePe, checked((int)(address - MeshAAddress)), 2);
        }
        if (address >= ChannelBase)
        {
            return (Channels, RequireChannel(address, length), 3);
        }
        return (Globals, RequireGlobal(address, length), 0);
    }
    private void RequireLoaded()
    {
        if (SourcesLoaded != true)
        {
            throw new NotSupportedException("Magma Breath requires its actual current seeded PE source owners.");
        }
    }
    private static bool Contains(uint first, int count, uint address, int length) => length >= 0 && address >= first && (ulong)address + (uint)length <= (ulong)first + (uint)count;
    private static int RequireGlobal(uint address, int length)
    {
        if (!GlobalRanges.Any(region => Contains(region.Address, region.Length, address, length)))
        {
            throw new InvalidDataException("Magma Breath globals exclude hierarchy banks, descriptors and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static int RequireChannel(uint address, int length)
    {
        if (!ChannelRegions.Any(region => Contains(region.Address, region.Length, address, length)))
        {
            throw new InvalidDataException("Magma Breath channel access leaves its exact typed physical intervals.");
        }
        return checked((int)(address - ChannelBase));
    }
    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }
}
