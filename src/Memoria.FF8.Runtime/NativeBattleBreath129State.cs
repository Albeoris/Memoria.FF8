using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreath129State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? MutableSources, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage)
{
    [JsonRequired]
    public bool PendingColdSources { get; init; }
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, 0x54), (CursorGlobal, 4)];

    public static NativeBattleBreath129State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, NativeBattleEffectArena.Clear()) { PendingColdSources = true };
    public static NativeBattleBreath129State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, MutableSourcesLength), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength));
    }

    public NativeBattleBreath129State RequireSeeds(NativeBattleBreath129Resources resources)
    {
        if (!PendingColdSources)
        {
            return this;
        }
        if (MutableSources is not null)
        {
            throw new InvalidDataException("Breath already has its retained mutable source owner.");
        }
        resources.Validate();
        return this with { MutableSources = NativeBattleEffectArena.Clear().Write(0, resources.SourceSeeds.Data.AsSpan()), PendingColdSources = false };
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Globals.UnknownRegions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (PendingColdSources && MutableSources is not null || !PendingColdSources && MutableSources is null)
        {
            throw new InvalidDataException("Breath requires one current mutable source owner or an explicit pending cold publication.");
        }
        if (MutableSources is { } sources)
        {
            Bounded(sources, MutableSourcesLength);
        }
        if (RootBankStorage is { } root)
        {
            Bounded(root, RootBankLength);
        }
    }

    public byte[] Read(uint address, int length)
    {
        (NativeBattleEffectArena arena, int offset, _) = Owner(address, length);
        return arena.Read(offset, length);
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleBreath129State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleBreath129State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleBreath129State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        (NativeBattleEffectArena arena, int offset, bool sources) = Owner(address, bytes.Length);
        return sources ? this with { MutableSources = arena.Write(offset, bytes) } : this with { Globals = arena.Write(offset, bytes) };
    }
    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        (NativeBattleEffectArena arena, int offset, _) = Owner(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
    public NativeBattleBreath129State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        (NativeBattleEffectArena arena, int offset, bool sources) = Owner(address, length);
        NativeBattleEffectArena next = NativeBattleEffectNodeContextMemory.Copy(source, 0, arena, offset, length);
        return sources ? this with { MutableSources = next } : this with { Globals = next };
    }
    private (NativeBattleEffectArena Arena, int Offset, bool Sources) Owner(uint address, int length)
    {
        if (address >= MeshAddress && length >= 0 && (ulong)address + (uint)length <= MeshAddress + MutableSourcesLength)
        {
            return (MutableSources ?? throw new NotSupportedException("Breath requires its explicitly published current mutable source bytes."), checked((int)(address - MeshAddress)), true);
        }
        return (Globals, Require(address, length), false);
    }
    private static int Require(uint address, int length)
    {
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && (ulong)address + (uint)length <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Breath scalar bytes exclude physical task banks, descriptors, working arena and unused gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static void Bounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Breath retained bytes leave their single physical owner.");
        }
    }
}
