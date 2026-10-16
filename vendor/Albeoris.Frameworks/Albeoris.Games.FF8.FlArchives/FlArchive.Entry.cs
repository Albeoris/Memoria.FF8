using System.Diagnostics.CodeAnalysis;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Albeoris.Games.FF8.FlArchives;

public sealed partial class FlArchive
{
    private sealed class FlArchiveEntry : IFlArchiveEntry
    {
        private EntryCollection? _collection;

        public String RelativePath { get; }
        public UInt32 Offset { get; internal set; }
        public UInt32 Size { get; internal set; }
        public FlCompressionMethod Compression { get; internal set; }

        public Int64 MetricsPosition { get; internal set; }
        public Int64 ListingPosition { get; internal set; }

        public FlArchiveEntry(String relativePath, UInt32 offset, UInt32 size, FlCompressionMethod compression, Int64 metricsPosition, Int64 listingPosition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath, nameof(relativePath));
            if (!Enum.IsDefined(typeof(FlCompressionMethod), compression))
                throw new ArgumentOutOfRangeException(nameof(compression), compression, $"Invalid FlCompressionMethod: {compression}.");

            RelativePath = relativePath;
            Offset = offset;
            Size = size;
            Compression = compression;

            MetricsPosition = metricsPosition;
            ListingPosition = listingPosition;
        }

        public Stream OpenForRead()
        {
            ThrowIfDetachedFromArchive();

            return _collection.OpenForRead(this);
        }

        public Stream OpenForWrite(UInt32 desiredSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(desiredSize, nameof(desiredSize));
            ThrowIfDetachedFromArchive();

            return _collection.OpenForWrite(this, desiredSize);
        }

        internal void AttachToArchive(EntryCollection entries)
        {
            ThrowIfAttachedToArchive();

            _collection = entries;
        }

        internal void DetachFromArchive()
        {
            ThrowIfDetachedFromArchive();

            _collection = null;
        }

        private void ThrowIfAttachedToArchive()
        {
            if (_collection is not null)
                throw new InvalidOperationException($"The entry {RelativePath} has been added to an archive.");
        }

        [MemberNotNull(nameof(_collection))]
        private void ThrowIfDetachedFromArchive()
        {
            if (_collection is null)
                throw new InvalidOperationException($"The entry {RelativePath} has been removed from an archive.");
        }
    }
}