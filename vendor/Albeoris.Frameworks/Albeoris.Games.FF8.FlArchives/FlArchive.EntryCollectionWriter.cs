using Albeoris.Games.Core.NsStreams;

namespace Albeoris.Games.FF8.FlArchives;

public sealed partial class FlArchive
{
    private sealed class EntryCollectionWriter(EntryCollection entryCollection)
    {
        private const Int32 MetricsEntrySize = sizeof(UInt32) * 3;

        public void AppendEntry(FlArchiveEntry entry)
        {
            Stream listingStream = entryCollection._listingStream;
            Stream metricsStream = entryCollection._metricsStream;

            listingStream.Seek(0, SeekOrigin.End);

            // Metrics: write at the logical end (not physical end) so that any pre-allocated
            // zero-bytes reserved by FlArchive.Optimize are overwritten from the front.
            metricsStream.Seek(entryCollection.MetricsLogicalEnd, SeekOrigin.Begin);

            // Record the byte positions before writing so the entry can be updated or removed later.
            entry.ListingPosition = listingStream.Position;
            entry.MetricsPosition = metricsStream.Position;

            using (StreamWriter listingWriter = new StreamWriter(listingStream, PathEncoding, bufferSize: 1024, leaveOpen: true))
                listingWriter.WriteLine(InternalPathPrefix + entry.RelativePath);

            WriteUInt32(entry.Size);
            WriteUInt32(entry.Offset);
            WriteUInt32((UInt32)entry.Compression);

            entryCollection.MetricsLogicalEnd += MetricsEntrySize;
        }

        public void RemoveEntry(FlArchiveEntry entry)
        {
            // Remove from listing: read remaining lines, rewrite from the removed entry's position, truncate.
            Stream listingStream = entryCollection._listingStream;
            listingStream.Seek(entry.ListingPosition, SeekOrigin.Begin);

            Int64 lineEnd;
            IReadOnlyList<String> restLines;
            using (StreamReader reader = new StreamReader(listingStream, PathEncoding, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                _ = reader.ReadLine();
                lineEnd = reader.GetBinaryPosition();
                restLines = reader.ReadAllLines();
            }

            Int64 listingShift = lineEnd - entry.ListingPosition;

            listingStream.Seek(entry.ListingPosition, SeekOrigin.Begin);
            using (StreamWriter writer = new StreamWriter(listingStream, PathEncoding, bufferSize: 1024, leaveOpen: true))
                writer.WriteAllLines(restLines);
            listingStream.SetLength(listingStream.Position);

            // Shift ListingPosition of all entries that followed the removed line.
            foreach (FlArchiveEntry remainingEntry in entryCollection.Entries)
            {
                if (remainingEntry.ListingPosition > entry.ListingPosition)
                    remainingEntry.ListingPosition -= listingShift;
            }

            // Remove from metrics: shift remaining bytes left within the logical region, then truncate.
            Stream metricsStream = entryCollection._metricsStream;
            Int64 readPosition = entry.MetricsPosition + MetricsEntrySize;
            Int64 writePosition = entry.MetricsPosition;
            Int64 logicalEnd = entryCollection.MetricsLogicalEnd;

            metricsStream.SetPosition(readPosition);

            Byte[] buffer = new Byte[64 * 1024];
            Int32 bytesRead;
            while (metricsStream.Position < logicalEnd &&
                   (bytesRead = metricsStream.Read(buffer, 0, (Int32)Math.Min(buffer.Length, logicalEnd - metricsStream.Position))) > 0)
            {
                metricsStream.Seek(writePosition, SeekOrigin.Begin);
                metricsStream.Write(buffer, 0, bytesRead);
                writePosition += bytesRead;
                readPosition += bytesRead;
                metricsStream.Seek(readPosition, SeekOrigin.Begin);
            }

            // Truncate the physical metrics file to the new logical end (dropping any pre-allocated space).
            metricsStream.SetLength(logicalEnd - MetricsEntrySize);
            entryCollection.MetricsLogicalEnd -= MetricsEntrySize;

            // Shift MetricsPosition of all entries that followed the removed record.
            foreach (FlArchiveEntry remainingEntry in entryCollection.Entries)
            {
                if (remainingEntry.MetricsPosition > entry.MetricsPosition)
                    remainingEntry.MetricsPosition -= MetricsEntrySize;
            }
        }

        private void WriteUInt32(UInt32 value) => entryCollection._metricsStream.WriteStruct(value);
    }
}