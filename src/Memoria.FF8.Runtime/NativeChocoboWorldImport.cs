using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeChocoboWorldImport(string Format, string ContentSha256, int SelectedBank, uint Revision, uint WorldId, int BokoId, int Level, int CurrentHp, int MaximumHp, int AttackTier, byte EventFlags, ImmutableArray<int> ClassCounts, ImmutableArray<byte> RawState, ImmutableArray<byte> RawWorld)
{
    public string SourceId => $"world:{WorldId:X8}/boko:{BokoId:D4}";
    public uint? BackupRevision { get; init; }
    public bool BackupStructurallyValid { get; init; }
    public bool IsAway => (EventFlags & 2) != 0;
    public bool MiniMogFound => (EventFlags & 4) != 0;
}

/// <summary>The installed game's 128 addressable rows, with A/B/C/D columns.</summary>
public sealed record NativeChocoboWorldTransferTable(ImmutableArray<byte> Entries)
{
    public void Validate()
    {
        if (Entries.IsDefault || Entries.Length != 512 || Entries.Any(item => item >= 199))
        {
            throw new InvalidDataException("Chocobo World transfer table requires 128 complete native item rows.");
        }
    }

    public byte Select(int rewardClass, byte random)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(rewardClass);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rewardClass, 3);
        return Entries[(random & 127) * 4 + rewardClass];
    }
}
