using Albeoris.Games.Core.NsCapacityCalculator;
using Albeoris.Games.Core.NsCollections;
using Albeoris.Games.Core.NsStreams;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Albeoris.Games.FF8.FlArchives;

public sealed partial class FlArchive
{
    private sealed class EntryCollectionReader(Stream listingStream, Stream metricsStream, Stream contentStream)
    {
        public static EntryCollection Read(Stream listingStream, Stream metricsStream, Stream contentStream)
        {
            ArgumentNullException.ThrowIfNull(listingStream);
            ArgumentNullException.ThrowIfNull(metricsStream);
            ArgumentNullException.ThrowIfNull(contentStream);

            EntryCollectionReader reader = new EntryCollectionReader(listingStream, metricsStream, contentStream);
            return reader.ReadEntries();
        }

        private EntryCollection ReadEntries()
        {
            // Entry count is driven by the listing file, not by metrics file size.
            // This allows the metrics file to have pre-allocated zero-bytes at the end
            // (written by FlArchive.Optimize) without disrupting the reader.
            List<(String RelativePath, Int64 ListingPosition)> listingEntries = ReadListingEntries();
            Int32 recordCount = listingEntries.Count;

            const Int64 recordSize = sizeof(Int32) * 3;
            Int64 metricsAvailable = metricsStream.Length - metricsStream.Position;
            if (metricsAvailable < recordCount * recordSize)
                throw new FormatException($"Metrics file is too short. Expected at least {recordCount * recordSize} bytes for {recordCount} entries, but only {metricsAvailable} bytes are available.");

            FlArchiveEntry[] entries = new FlArchiveEntry[recordCount];
            for (Int32 i = 0; i < recordCount; i++)
            {
                Int64 metricPosition = metricsStream.Position;
                (String relativePath, Int64 listingPosition) = listingEntries[i];
                UInt32 uncompressedContentSize = ReadUInt32();
                UInt32 contentOffset = ReadUInt32();
                FlCompressionMethod compression = (FlCompressionMethod)ReadInt32();

                entries[i] = new FlArchiveEntry(relativePath, contentOffset, uncompressedContentSize, compression, metricPosition, listingPosition);
            }

            // Position after the last valid metrics record; any bytes beyond this are pre-allocated space.
            Int64 metricsLogicalEnd = metricsStream.Position;

            CapacityCalculator capacityCalculator = new(boundariesCount: recordCount + 1);

            // Register content boundaries in offset order so the capacity calculator can binary-search.
            foreach (FlArchiveEntry entry in entries.OrderBy(o => o.Offset))
            {
                if (entry.Size > 0)
                    capacityCalculator.RegisterBoundary(entry.Offset);
            }

            capacityCalculator.RegisterBoundary(contentStream.Length);

            // Populate the dictionary in LISTING order so that Entries always reflects the order
            // declared in the .fl file, regardless of physical content offsets.
            OrderedDictionary<String, FlArchiveEntry> dictionary = new(capacity: recordCount, PathComparer);
            foreach (FlArchiveEntry entry in entries)
                dictionary.Add(entry.RelativePath, entry);

            EntryCollection collection = new EntryCollection(listingStream, metricsStream, contentStream, dictionary, capacityCalculator, metricsLogicalEnd);
            foreach (FlArchiveEntry entry in entries)
            {
                ValidateEntry(entry, capacityCalculator);
                entry.AttachToArchive(collection);
            }

            return collection;
        }

        private List<(String RelativePath, Int64 ListingPosition)> ReadListingEntries()
        {
            List<(String, Int64)> result = new();
            using (StreamReader listingReader = new StreamReader(listingStream, PathEncoding, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                while (!listingReader.EndOfStream)
                {
                    Int64 listingPosition = listingReader.GetBinaryPosition();
                    String? line = listingReader.ReadLine();
                    if (String.IsNullOrWhiteSpace(line))
                        continue;

                    if (!line.StartsWith(InternalPathPrefix, StringComparison.OrdinalIgnoreCase))
                        throw new FormatException($"Invalid internal path: {line}. Expected prefix: {InternalPathPrefix}");

                    String relativePath = line.Substring(InternalPathPrefix.Length);
                    result.Add((relativePath, listingPosition));
                }
            }

            return result;
        }

        private Int32 ReadInt32() => metricsStream.ReadStruct<Int32>();
        private UInt32 ReadUInt32() => metricsStream.ReadStruct<UInt32>();
    }
}