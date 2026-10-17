namespace Memoria.FF8.Runtime;

public enum NativeEventMemoryOwner
{
    Generic,
    SeeD,
    Economy,
    PreviousField,
    PreviousWorldEntity,
    WorldControls,
    MusicChannels,
    DrawPoints,
    Junction,
    PartyBindings,
    SoundVolume,
    MusicProcess,
    Disc,
    Cards,
    BattleMode,
    Timer,
    Fade,
    WorldEntry,
    WorldMusicSelection,
    FieldLoadReason,
    BattleMusic
}

public readonly record struct NativeEventMemoryRegion(int Address, int Length, NativeEventMemoryOwner Owner)
{
    public bool Overlaps(int address, int width) => address < (long)Address + Length && Address < (long)address + width;
    public bool Contains(int address, int width) => address >= Address && (long)address + width <= (long)Address + Length;
}

/// <summary>Resolves one physical span before any read, operand pop or publication.</summary>
public static partial class NativeEventMemoryOwnership
{
    public static NativeEventMemoryOwner Resolve(int address, int width, IReadOnlyList<NativeEventMemoryRegion> regions)
    {
        if (address < 0 || address > NativeEventMemory.MaximumAddress || width is not (1 or 2 or 4))
        {
            throw new NotSupportedException("Native event access requires a represented address and BYTE, WORD or DWORD width.");
        }
        NativeEventMemoryOwner owner = NativeEventMemoryOwner.Generic;
        bool found = false;
        foreach (NativeEventMemoryRegion region in regions)
        {
            if (!region.Overlaps(address, width))
            {
                continue;
            }
            if (found || !region.Contains(address, width))
            {
                throw new NotSupportedException($"Native event access {address}/{width} crosses memory owners.");
            }
            owner = region.Owner;
            found = true;
        }
        return owner;
    }

    public static void Validate(NativeEventMemory memory, IReadOnlyList<NativeEventMemoryRegion> regions)
    {
        memory.Validate();
        for (int index = 0; index < regions.Count; index++)
        {
            NativeEventMemoryRegion region = regions[index];
            if (region.Owner == NativeEventMemoryOwner.Generic || region.Address < 0 || region.Length <= 0 || (long)region.Address + region.Length > ushort.MaxValue + 1L || regions.Skip(index + 1).Any(other => region.Overlaps(other.Address, other.Length)))
            {
                throw new InvalidDataException("Native event memory has overlapping or invalid owner registrations.");
            }
        }
        foreach ((int address, NativeMemoryValue value) in memory.Cells)
        {
            if (regions.Any(region => region.Overlaps(address, value.Width)))
            {
                throw new InvalidDataException($"Generic event cell {address}/{value.Width} overlaps a canonical memory owner.");
            }
        }
    }
}
