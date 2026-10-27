namespace Memoria.FF8.Runtime;

internal readonly record struct NativeBattleEffectStorageRange(int Offset, int Length);

/// <summary>Metadata traversal without publishing storage arrays or copying their payload.</summary>
internal readonly struct NativeBattleArenaRanges(NativeBattleArenaCoverage coverage)
{
    public Enumerator GetEnumerator() => new(coverage);

    public bool Contains(int offset)
    {
        foreach (NativeBattleEffectStorageRange range in this)
        {
            if (offset >= range.Offset && offset < range.Offset + range.Length)
            {
                return true;
            }
        }
        return false;
    }

    public bool Any(Func<NativeBattleEffectStorageRange, bool> predicate)
    {
        foreach (NativeBattleEffectStorageRange range in this)
        {
            if (predicate(range))
            {
                return true;
            }
        }
        return false;
    }

    internal struct Enumerator(NativeBattleArenaCoverage coverage)
    {
        private int index = -1;
        public NativeBattleEffectStorageRange Current
        {
            get
            {
                var range = coverage.Ranges[index];
                return new(range.Offset, range.End - range.Offset);
            }
        }
        public bool MoveNext() => ++index < coverage.Ranges.Count;
    }
}

public partial record NativeBattleEffectArena
{
    private NativeBattleArenaCoverage? projectedDataRanges;
    private NativeBattleArenaCoverage? projectedUnknownRanges;
    private NativeBattleArenaCoverage? projectedClassification;
    private NativeBattleArenaCoverage? snapshotDataRanges;
    private NativeBattleArenaCoverage? snapshotUnknownRanges;

    internal NativeBattleArenaRanges EnumerateDataRanges(bool committed = false)
    {
        Validate();
        if (!committed && TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int offset))
        {
            return new(ProjectRanges(transaction!.DataRanges(), offset, ref projectedDataRanges));
        }
        if (snapshot)
        {
            if (snapshotDataRanges is null)
            {
                snapshotDataRanges = new();
                foreach (NativeBattleEffectMemoryRegion region in initialRegions)
                {
                    snapshotDataRanges.Ranges.Add((region.Offset, region.Offset + region.Data.Length));
                }
            }
            return new(snapshotDataRanges);
        }
        return new(ProjectPhysicalRanges(Working.DataFootprint, ref projectedDataRanges));
    }

    internal NativeBattleArenaRanges EnumerateUnknownRanges(bool committed = false)
    {
        Validate();
        if (!committed && TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int offset))
        {
            return new(ProjectRanges(transaction!.UnknownRanges(), offset, ref projectedUnknownRanges));
        }
        if (snapshot)
        {
            if (snapshotUnknownRanges is null)
            {
                snapshotUnknownRanges = new();
                foreach (NativeBattleEffectUnknownRegion region in initialUnknown)
                {
                    snapshotUnknownRanges.Ranges.Add((region.Offset, region.Offset + region.Length));
                }
            }
            return new(snapshotUnknownRanges);
        }
        return new(ProjectPhysicalRanges(Working.UnknownCoverage, ref projectedUnknownRanges));
    }

    internal NativeBattleArenaCoverage Classification => snapshot ? SnapshotCoverage.GetValue(this, static arena => arena.ImportCoverage()) : ProjectPhysicalRanges(Working.ExplicitCoverage, ref projectedClassification);

    private NativeBattleArenaCoverage ProjectPhysicalRanges(NativeBattleArenaCoverage physical, ref NativeBattleArenaCoverage? target)
    {
        if (workingOffset == 0 && bound == Working.LogicalLength)
        {
            return physical;
        }
        return ProjectRanges(new(physical), workingOffset, ref target);
    }

    private NativeBattleArenaCoverage ProjectRanges(NativeBattleArenaRanges source, int offset, ref NativeBattleArenaCoverage? target)
    {
        target ??= new();
        target.Clear();
        foreach (NativeBattleEffectStorageRange range in source)
        {
            int first = Math.Max(range.Offset, offset);
            int end = Math.Min(range.Offset + range.Length, offset + bound);
            if (first < end)
            {
                target.Ranges.Add((first - offset, end - offset));
            }
        }
        return target;
    }
}
