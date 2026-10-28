using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleWorkingMemory
{
    internal NativeBattleArenaCoverage DataFootprint { get; } = new();
    internal NativeBattleArenaCoverage ExplicitCoverage { get; } = new();
    internal NativeBattleArenaCoverage UnknownCoverage { get; } = new();

    internal void ReserveStorageReceipts(int additional)
    {
        DataFootprint.Reserve(additional);
        ExplicitCoverage.Reserve(additional);
        UnknownCoverage.Reserve(additional);
    }

    private void PrepareStorageReceipts(int unknownRanges = 1)
    {
        NativeBattleTaskOperation.EnlistRetained(this);
        DataFootprint.Reserve(1, checkpoint);
        ExplicitCoverage.Reserve(1, checkpoint);
        UnknownCoverage.Reserve(Math.Max(1, unknownRanges), checkpoint);
        if (checkpoint)
        {
            DataFootprint.ReserveCheckpointDepth(CheckpointDepth);
            ExplicitCoverage.ReserveCheckpointDepth(CheckpointDepth);
            UnknownCoverage.ReserveCheckpointDepth(CheckpointDepth);
            EnlistCheckpointParticipant(DataFootprint);
            EnlistCheckpointParticipant(ExplicitCoverage);
            EnlistCheckpointParticipant(UnknownCoverage);
        }
    }

    private void RecordStorageReceipts(int offset, int length, bool storedData)
    {
        if (storedData)
        {
            DataFootprint.Add(offset, length);
        }
        ExplicitCoverage.Add(offset, length);
    }

    private void RecordKnownness(int offset, int length, bool knownValue)
    {
        if (knownValue)
        {
            UnknownCoverage.Remove(offset, length);
        }
        else
        {
            UnknownCoverage.Add(offset, length);
        }
    }

    private void RefreshKnownness(int offset, int length)
    {
        UnknownCoverage.Remove(offset, length);
        for (int index = offset; index < offset + length;)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < offset + length && !known[index])
            {
                index++;
            }
            UnknownCoverage.Add(first, index - first);
        }
    }

    private static int CountUnknownRanges(ReadOnlySpan<bool> flags)
    {
        int count = 0;
        for (int index = 0; index < flags.Length; index++)
        {
            if (!flags[index] && (index == 0 || flags[index - 1]))
            {
                count++;
            }
        }
        return count;
    }

    private static ImmutableArray<NativeBattleEffectUnknownRegion> CaptureReceipts(NativeBattleArenaCoverage source)
    {
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>(source.Ranges.Count);
        foreach ((int offset, int end) in source.Ranges)
        {
            ranges.Add(new(offset, end - offset));
        }
        return ranges.MoveToImmutable();
    }

    private void RestoreStorageReceipts(NativeBattleWorkingMemorySnapshot snapshot)
    {
        UnknownCoverage.Remove(0, snapshot.StoredLength);
        foreach (NativeBattleEffectUnknownRegion unknown in snapshot.UnknownRegions)
        {
            UnknownCoverage.Add(unknown.Offset, unknown.Length);
        }
        if (snapshot.DataFootprint.IsDefault)
        {
            foreach (NativeBattleEffectMemoryRegion region in snapshot.Regions)
            {
                DataFootprint.Ranges.Add((region.Offset, region.Offset + region.Data.Length));
            }
        }
        else
        {
            RestoreReceipts(DataFootprint, snapshot.DataFootprint, snapshot.StoredLength);
        }
        if (snapshot.ClassifiedRanges.IsDefault)
        {
            foreach (NativeBattleEffectMemoryRegion region in snapshot.Regions)
            {
                ExplicitCoverage.Add(region.Offset, region.Data.Length);
            }
            foreach (NativeBattleEffectUnknownRegion region in snapshot.UnknownRegions)
            {
                ExplicitCoverage.Add(region.Offset, region.Length);
            }
        }
        else
        {
            ExplicitCoverage.Clear();
            RestoreReceipts(ExplicitCoverage, snapshot.ClassifiedRanges, snapshot.LogicalLength);
        }
        foreach ((int offset, int end) in DataFootprint.Ranges)
        {
            bool classified = false;
            foreach ((int first, int last) in ExplicitCoverage.Ranges)
            {
                if (offset >= first && end <= last)
                {
                    classified = true;
                    break;
                }
            }
            if (!classified)
            {
                throw new InvalidDataException("Native data footprint requires explicit physical storage classification.");
            }
        }
    }

    private static void RestoreReceipts(NativeBattleArenaCoverage target, ImmutableArray<NativeBattleEffectUnknownRegion> source, int maximum)
    {
        int end = 0;
        foreach (NativeBattleEffectUnknownRegion region in source)
        {
            if (region is null || region.Length <= 0 || region.Offset < end || region.Offset > maximum - region.Length)
            {
                throw new InvalidDataException("Native physical storage receipts overlap or exceed their owner.");
            }
            target.Ranges.Add((region.Offset, region.Offset + region.Length));
            end = region.Offset + region.Length;
        }
    }
}
