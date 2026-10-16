using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Albeoris.Games.FF8.FlArchives;

public sealed partial class FlArchive
{
    /// <summary>
    /// Creates a compacted copy of this archive at <paramref name="newPath"/>, eliminating all
    /// unused space ("holes") between entries in the content file. The new archive contains the
    /// same entries in the same order, each occupying exactly the space needed for its content.
    /// </summary>
    /// <param name="newPath">
    /// Base path of the output archive (without extension). The three component files
    /// (<c>.fl</c>, <c>.fi</c>, <c>.fs</c>) are created alongside this path.
    /// </param>
    public void Compact(String newPath)
    {
        ArgumentNullException.ThrowIfNull(newPath);
        Flush();

        using (FlArchive target = (FlArchive)FlArchive.Create(newPath, FlArchiveRepresentation.Files))
        {
            CopyEntries(source: this, target, reservedSizeFn: static (entry) => entry.Size);
        }
    }

    /// <summary>
    /// Creates an optimized copy of this archive at <paramref name="newPath"/>. Each entry's
    /// content slot is padded according to <paramref name="spec"/>, leaving room for in-place
    /// growth without requiring relocation. The metrics file is additionally extended with
    /// pre-allocated zero-byte records for future entries.
    /// </summary>
    /// <param name="spec">Reservation parameters.</param>
    /// <param name="newPath">
    /// Base path of the output archive (without extension). The three component files
    /// (<c>.fl</c>, <c>.fi</c>, <c>.fs</c>) are created alongside this path.
    /// </param>
    public void Optimize(FlOptimizeSpec spec, String newPath)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(newPath);
        Flush();

        Int32 absoluteReserve = spec.AbsoluteReserveBytes;
        Single relativeFraction = spec.RelativeReserveFraction;

        UInt32 ReservedSizeFn(IFlArchiveEntry entry)
        {
            UInt32 extra = (UInt32)(entry.Size * relativeFraction) + (UInt32)absoluteReserve;
            return entry.Size + extra;
        }

        using (FlArchive target = (FlArchive)FlArchive.Create(newPath, FlArchiveRepresentation.Files))
        {
            CopyEntries(source: this, target, reservedSizeFn: ReservedSizeFn);

            if (spec.ExpectedNewEntries > 0)
            {
                // Extend the metrics file with pre-allocated zero-records so future AddEntry calls
                // overwrite them in-place (MetricsLogicalEnd tracks where valid data ends).
                Int64 extraBytes = (Int64)spec.ExpectedNewEntries * sizeof(UInt32) * 3;
                Stream metricsStream = target._entries._metricsStream;
                metricsStream.SetLength(target._entries.MetricsLogicalEnd + extraBytes);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static void CopyEntries(FlArchive source, FlArchive target, Func<IFlArchiveEntry, UInt32> reservedSizeFn)
    {
        Byte[] buffer = new Byte[MovingBufferSize];

        foreach (IFlArchiveEntry sourceEntry in source.Entries)
        {
            IFlArchiveEntry targetEntry = target.AddEntry(sourceEntry.RelativePath);
            UInt32 reservedSize = reservedSizeFn(sourceEntry);

            using (Stream output = targetEntry.OpenForWrite(reservedSize))
            {
                if (sourceEntry.Size > 0)
                {
                    using (Stream input = sourceEntry.OpenForRead())
                    {
                        Int32 read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            output.Write(buffer, 0, read);
                    }
                }
                // Any remaining bytes in the slot (reservedSize - sourceEntry.Size) stay as padding.
            }
        }
    }
}
