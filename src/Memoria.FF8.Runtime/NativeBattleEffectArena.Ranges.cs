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
    private NativeBattleArenaCoverage? dataCoverage;
    private NativeBattleArenaCoverage? unknownCoverage;
    private long metadataVersion = -1;
    private NativeBattleArenaCoverage? effectiveDataRanges;
    private NativeBattleArenaCoverage? effectiveUnknownRanges;

    internal NativeBattleArenaRanges EnumerateDataRanges(bool committed = false)
    {
        Validate();
        if (!committed && TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int offset))
        {
            return ProjectRanges(transaction!.DataRanges(), offset, ref effectiveDataRanges);
        }
        if (dataCoverage is null)
        {
            InitializeRangeMetadata();
        }
        if (borrowed && !snapshot)
        {
            dataCoverage!.Add(0, CommittedStoredLength);
        }
        return new(dataCoverage!);
    }

    internal NativeBattleArenaRanges EnumerateUnknownRanges(bool committed = false)
    {
        Validate();
        if (!committed && TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int offset))
        {
            return ProjectRanges(transaction!.UnknownRanges(), offset, ref effectiveUnknownRanges);
        }
        EnsureUnknownMetadata();
        return new(unknownCoverage!);
    }

    private NativeBattleArenaRanges ProjectRanges(NativeBattleArenaRanges source, int offset, ref NativeBattleArenaCoverage? target)
    {
        target ??= new();
        target.Clear();
        foreach (NativeBattleEffectStorageRange range in source)
        {
            int first = Math.Max(range.Offset, offset);
            int end = Math.Min(range.Offset + range.Length, offset + bound);
            if (first < end)
            {
                target.Add(first - offset, end - first);
            }
        }
        return new(target);
    }

    private void InitializeRangeMetadata()
    {
        dataCoverage = new();
        unknownCoverage = new();
        foreach (NativeBattleEffectMemoryRegion region in initialRegions)
        {
            dataCoverage.Ranges.Add((region.Offset, region.Offset + region.Data.Length));
        }
        foreach (NativeBattleEffectUnknownRegion region in initialUnknown)
        {
            unknownCoverage.Add(region.Offset, region.Length);
        }
    }

    private void EnsureUnknownMetadata()
    {
        if (unknownCoverage is null)
        {
            InitializeRangeMetadata();
        }
        if (snapshot || metadataVersion == Working.Version)
        {
            return;
        }
        unknownCoverage!.Reserve(checked(Math.Min(bound, Working.Capacity) / 2 + 1));
        unknownCoverage.Clear();
        Span<byte> bytes = stackalloc byte[256];
        Span<bool> known = stackalloc bool[256];
        int extent = Working.InitialKnownZero ? CommittedStoredLength : bound;
        int first = -1;
        for (int offset = 0; offset < extent;)
        {
            int count = Math.Min(bytes.Length, extent - offset);
            Working.ReadWithKnownness(workingOffset + offset, bytes[..count], known[..count]);
            for (int index = 0; index < count; index++)
            {
                if (!known[index] && first < 0)
                {
                    first = offset + index;
                }
                else if (known[index] && first >= 0)
                {
                    unknownCoverage.Add(first, offset + index - first);
                    first = -1;
                }
            }
            offset += count;
        }
        if (first >= 0)
        {
            unknownCoverage.Add(first, extent - first);
        }
        metadataVersion = Working.Version;
    }

    internal void EnlistRangeMetadata()
    {
        if (!Working.HasCheckpoint)
        {
            return;
        }
        int depth = Working.CheckpointDepth;
        coverage!.ReserveCheckpointDepth(depth);
        dataCoverage!.ReserveCheckpointDepth(depth);
        unknownCoverage!.ReserveCheckpointDepth(depth);
        Working.EnlistCheckpointParticipant(coverage!);
        Working.EnlistCheckpointParticipant(dataCoverage!);
        Working.EnlistCheckpointParticipant(unknownCoverage!);
    }

    internal void RecordDataCoverage(int offset, int length) => dataCoverage!.Add(offset, length);

    internal void RecordKnowledge(int offset, int length, bool known)
    {
        if (known)
        {
            unknownCoverage!.Remove(offset, length);
        }
        else
        {
            unknownCoverage!.Add(offset, length);
        }
        metadataVersion = Working.Version;
    }
}
