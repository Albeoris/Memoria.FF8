using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleBladeShot12Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBladeShot12State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena MutableVertices, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildABankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildBBankStorage, [property: JsonRequired] bool? VertexSourcesLoaded)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(CasterGlobal, 8), (FirstTargetGlobal, 8), (CursorGlobal, 4), (SourcePointGlobal, 8)];
    private static readonly (uint Address, int Length)[] VertexRanges = [(MutableVerticesA, MutableVerticesLength), (MutableVerticesB, MutableVerticesLength)];

    public static NativeBattleBladeShot12State FromColdProcess() => new(NativeBattleEffectArena.Clear(), UnknownVertices(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), false);

    public static NativeBattleBladeShot12State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, UnknownVertices(), NativeBattleEffectArena.Clear().MarkUnknown(0, PacketBytes), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildABankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildBBankLength), null);
    }

    public NativeBattleBladeShot12State EnsureVertexSources(NativeBattleBladeShot12Resources resources)
    {
        if (VertexSourcesLoaded is null)
        {
            throw new NotSupportedException("Blade Shot requires the imported mutable vertex initialization state.");
        }
        if (VertexSourcesLoaded.Value)
        {
            return this;
        }
        NativeBattleEffectArena vertices = MutableVertices;
        foreach ((uint meshAddress, uint destination) in new[] { (MeshAAddress, MutableVerticesA), (MeshBAddress, MutableVerticesB) })
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(meshAddress);
            if (mesh.Address != meshAddress || mesh.Bytes.IsDefault || mesh.Bytes.Length != 0x5D0 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) != MorphSourceLength || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != MutableVertexCount)
            {
                throw new InvalidDataException("Blade Shot requires its original bounded initial PE vertex front.");
            }
            vertices = vertices.Write(checked((int)(destination - MutableVerticesA)), mesh.Bytes.AsSpan().Slice(8, MutableVerticesLength));
        }
        return this with { MutableVertices = vertices, VertexSourcesLoaded = true };
    }

    public void Validate()
    {
        ValidateRanges(Globals, BaseAddress, GlobalRanges);
        ValidateRanges(MutableVertices, MutableVerticesA, VertexRanges);
        ValidateBounded(Packets, PacketBytes);
        if (RootBankStorage is { } root)
        {
            ValidateBounded(root, RootBankLength);
        }
        if (ChildABankStorage is { } childA)
        {
            ValidateBounded(childA, ChildABankLength);
        }
        if (ChildBBankStorage is { } childB)
        {
            ValidateBounded(childB, ChildBBankLength);
        }
    }

    public byte[] Read(uint address, int length)
    {
        (NativeBattleEffectArena arena, int offset) = Owner(address, length);
        return arena.Read(offset, length);
    }
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        (NativeBattleEffectArena arena, int offset) = Owner(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
    public byte[] ReadPacket(uint address, int length)
    {
        RequireRange(address, length, PacketBase, PacketBytes);
        return Packets.Read(checked((int)(address - PacketBase)), length);
    }
    public NativeBattleBladeShot12State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        (NativeBattleEffectArena arena, int offset) = Owner(address, bytes.Length);
        return Replace(address, arena.Write(offset, bytes), bytes.Length);
    }
    public NativeBattleBladeShot12State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleBladeShot12State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleBladeShot12State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleBladeShot12State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        (NativeBattleEffectArena arena, int offset) = Owner(address, length);
        return Replace(address, NativeBattleEffectNodeContextMemory.Copy(source, 0, arena, offset, length), length);
    }

    private (NativeBattleEffectArena Arena, int Offset) Owner(uint address, int length)
    {
        if (Contains(address, length, PacketBase, PacketBytes))
        {
            return (Packets, checked((int)(address - PacketBase)));
        }
        if (VertexRanges.Any(range => Contains(address, length, range.Address, range.Length)))
        {
            if (VertexSourcesLoaded != true)
            {
                throw new NotSupportedException("Blade Shot mutable PE vertices must be explicitly initialized before consumption.");
            }
            return (MutableVertices, checked((int)(address - MutableVerticesA)));
        }
        RequireRanges(address, length, GlobalRanges);
        return (Globals, checked((int)(address - BaseAddress)));
    }
    private NativeBattleBladeShot12State Replace(uint address, NativeBattleEffectArena arena, int length)
    {
        if (Contains(address, length, PacketBase, PacketBytes))
        {
            return this with { Packets = arena };
        }
        if (VertexRanges.Any(range => Contains(address, length, range.Address, range.Length)))
        {
            return this with { MutableVertices = arena };
        }
        return this with { Globals = arena };
    }
    private static NativeBattleEffectArena UnknownVertices() => NativeBattleEffectArena.Clear().MarkUnknown(0, MutableVerticesLength).MarkUnknown(checked((int)(MutableVerticesB - MutableVerticesA)), MutableVerticesLength);
    private static bool Contains(uint address, int length, uint start, int count) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)count;
    private static void RequireRange(uint address, int length, uint start, int count)
    {
        if (!Contains(address, length, start, count))
        {
            throw new InvalidDataException("Blade Shot bytes leave their single physical owner.");
        }
    }
    private static void RequireRanges(uint address, int length, (uint Address, int Length)[] ranges)
    {
        if (!ranges.Any(range => Contains(address, length, range.Address, range.Length)))
        {
            throw new InvalidDataException("Blade Shot sparse bytes exclude task banks, descriptors, packet aliases and unused gaps.");
        }
    }
    private static void ValidateRanges(NativeBattleEffectArena arena, uint basis, (uint Address, int Length)[] ranges)
    {
        arena.Validate();
        foreach (NativeBattleEffectMemoryRegion region in arena.Regions)
        {
            RequireRanges(checked(basis + (uint)region.Offset), region.Data.Length, ranges);
        }
        foreach (NativeBattleEffectUnknownRegion region in arena.UnknownRegions)
        {
            RequireRanges(checked(basis + (uint)region.Offset), region.Length, ranges);
        }
    }
    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Blade Shot retained physical bank exceeds its original allocation.");
        }
    }
}
