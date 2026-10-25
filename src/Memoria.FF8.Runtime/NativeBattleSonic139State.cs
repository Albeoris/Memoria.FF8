using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSonic139State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? OriginalFrames, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, checked((int)(RootBank - BaseAddress))), (CursorGlobal, 4), (MatrixAddress, 32)];
    [JsonRequired]
    public bool PendingColdSources { get; init; }

    public static NativeBattleSonic139State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, NativeBattleEffectArena.Clear()) { PendingColdSources = true };
    public static NativeBattleSonic139State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, MutableFramesLength), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength));
    }

    public NativeBattleSonic139State RequireSeeds(NativeBattleSonic139Resources resources)
    {
        if (!PendingColdSources)
        {
            return this;
        }
        if (OriginalFrames is not null)
        {
            throw new InvalidDataException("Sonic Wave cold source publication already has a retained original frame owner.");
        }
        resources.Validate();
        return this with { OriginalFrames = NativeBattleEffectArena.Clear().Write(0, resources.FramesSeed.Data.AsSpan()), PendingColdSources = false };
    }

    public NativeBattleSonic139State CopyEntrySnapshot6A8860(NativeBattleSonic139Resources resources)
    {
        NativeBattleSonic139State state = RequireSeeds(resources).Write(CopiedMeshAddress, resources.EntryCopySeed.Data.AsSpan());
        return state.WriteView(CopiedMeshAddress + MutableFramesAddress - SourceMeshAddress, state.ReadView(MutableFramesAddress, MutableFramesLength), MutableFramesLength);
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
        if (PendingColdSources && OriginalFrames is not null || !PendingColdSources && OriginalFrames is null)
        {
            throw new InvalidDataException("Sonic Wave requires one original mutable frame owner or an explicit pending cold publication.");
        }
        if (OriginalFrames is { } frames)
        {
            Bounded(frames, MutableFramesLength);
        }
        if (RootBankStorage is { } root)
        {
            Bounded(root, RootBankLength);
        }
    }

    public byte[] Read(uint address, int length) => Owner(address, length).Arena.Read(Owner(address, length).Offset, length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleSonic139State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleSonic139State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleSonic139State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        (NativeBattleEffectArena arena, int offset, bool frames) = Owner(address, bytes.Length);
        return frames ? this with { OriginalFrames = arena.Write(offset, bytes) } : this with { Globals = arena.Write(offset, bytes) };
    }
    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        (NativeBattleEffectArena arena, int offset, _) = Owner(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
    public NativeBattleSonic139State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        (NativeBattleEffectArena arena, int offset, bool frames) = Owner(address, length);
        NativeBattleEffectArena next = NativeBattleEffectNodeContextMemory.Copy(source, 0, arena, offset, length);
        return frames ? this with { OriginalFrames = next } : this with { Globals = next };
    }
    private (NativeBattleEffectArena Arena, int Offset, bool Frames) Owner(uint address, int length)
    {
        if (address >= MutableFramesAddress && length >= 0 && (ulong)address + (uint)length <= MutableFramesAddress + MutableFramesLength)
        {
            return (OriginalFrames ?? throw new NotSupportedException("Sonic Wave requires its explicitly published current original frame bytes."), checked((int)(address - MutableFramesAddress)), true);
        }
        return (Globals, Require(address, length), false);
    }
    private static int Require(uint address, int length)
    {
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && (ulong)address + (uint)length <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Sonic Wave scalar/copy bytes exclude task banks, descriptors, working arena and unused gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
    private static void Bounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Sonic Wave retained bytes leave their single physical owner.");
        }
    }
}
