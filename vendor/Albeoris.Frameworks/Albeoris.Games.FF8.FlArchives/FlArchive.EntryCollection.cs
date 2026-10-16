using Albeoris.Games.Core.NsCapacityCalculator;
using Albeoris.Games.Core.NsCollections;
using Albeoris.Games.Core.NSCompression.LZ4;
using Albeoris.Games.Core.NSCompression.LZS;
using Albeoris.Games.Core.NsStreams;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Albeoris.Games.FF8.FlArchives;

public sealed partial class FlArchive
{
    private sealed class EntryCollection
    {
        internal readonly Stream _listingStream;
        internal readonly Stream _metricsStream;
        internal readonly Stream _contentStream;

        private readonly OrderedDictionary<String, FlArchiveEntry> _entries;
        private readonly CapacityCalculator _capacityCalculator;

        /// <summary>Byte offset in the metrics stream immediately after the last valid 12-byte record.
        /// Bytes between this position and <c>_metricsStream.Length</c> are pre-allocated space reserved
        /// by <see cref="FlArchive.Optimize"/> and must not be treated as valid entries.</summary>
        internal Int64 MetricsLogicalEnd { get; set; }

        public EntryCollection(Stream listingStream, Stream metricsStream, Stream contentStream, OrderedDictionary<String, FlArchiveEntry> entries, CapacityCalculator capacityCalculator, Int64 metricsLogicalEnd)
        {
            ArgumentNullException.ThrowIfNull(listingStream);
            ArgumentNullException.ThrowIfNull(metricsStream);
            ArgumentNullException.ThrowIfNull(contentStream);
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(capacityCalculator);

            _listingStream = listingStream;
            _metricsStream = metricsStream;
            _contentStream = contentStream;
            _entries = entries;
            _capacityCalculator = capacityCalculator;
            MetricsLogicalEnd = metricsLogicalEnd;
        }

        public static EntryCollection CreateEmpty(Stream listingStream, Stream metricsStream, Stream contentStream)
        {
            CapacityCalculator calculator = new CapacityCalculator();
            calculator.RegisterBoundary(contentStream.Length);
            return new EntryCollection(listingStream, metricsStream, contentStream, new OrderedDictionary<String, FlArchiveEntry>(PathComparer), calculator, metricsLogicalEnd: 0);
        }

        public IReadOnlyList<FlArchiveEntry> Entries => _entries.Values;

        private FlArchiveEntry? _openedEntry;
        private Boolean _hasChanges;

        public Stream OpenForRead(FlArchiveEntry entry)
        {
            return DecompressEntry(entry);
        }

        public Stream OpenForWrite(FlArchiveEntry entry, UInt32 desiredSize)
        {
            Boolean isUnplaced = entry.Size == 0;

            if (desiredSize == 0)
            {
                if (!isUnplaced)
                {
                    UnregisterEntryContentPosition(entry);
                    entry.Offset = 0;
                    entry.Size = 0;
                    entry.Compression = FlCompressionMethod.None;
                    UpdateEntryMetrics(entry);
                }
                _hasChanges = true;
                OpenEntry(entry);
                return ProtectEntry(entry, new SegmentStream(_contentStream, 0, 0));
            }

            Int64 capacity = isUnplaced ? -1 : _capacityCalculator.GetCapacity(entry.Offset);

            if (isUnplaced || capacity < desiredSize)
            {
                // Not enough space at the current offset: append to the end of the content file.
                UnregisterEntryContentPosition(entry);
                entry.Offset = checked((UInt32)_contentStream.Length);
                entry.Size = desiredSize;
                entry.Compression = FlCompressionMethod.None;
                IncreaseContentSize(desiredSize);
                RegisterEntryContentPosition(entry);
                UpdateEntryMetrics(entry);
            }
            else if (entry.Size != desiredSize || entry.Compression != FlCompressionMethod.None)
            {
                // Fits in the existing slot — update size/compression in-place.
                entry.Size = desiredSize;
                entry.Compression = FlCompressionMethod.None;
                UpdateEntryMetrics(entry);
            }

            _hasChanges = true;
            OpenEntry(entry);
            SegmentStream segment = new SegmentStream(_contentStream, entry.Offset, entry.Size);
            return ProtectEntry(entry, segment);
        }

        private void OpenEntry(FlArchiveEntry entry)
        {
            FlArchiveEntry? previousValue = Interlocked.CompareExchange(ref _openedEntry, entry, null);
            if (previousValue is not null)
                throw new InvalidOperationException($"The archive is already in use by another stream for entry [{previousValue.RelativePath}].");

            _contentStream.Position = entry.Offset;
        }

        private Stream ProtectEntry(FlArchiveEntry entry, Stream contentStream)
        {
            DisposableStream callback = new DisposableStream(contentStream);
            callback.AfterDispose += AfterDispose;
            return callback;

            void AfterDispose(Stream stream, Boolean managedDisposing) => CloseEntry(entry);
        }

        private Stream DecompressEntry(FlArchiveEntry entry)
        {
            OpenEntry(entry);
            switch (entry.Compression)
            {
                case FlCompressionMethod.None:
                {
                    Int64 capacity = _capacityCalculator.GetCapacity(_contentStream.Position);
                    if (capacity < entry.Size)
                        throw new InvalidDataException($"Not enough capacity available: {capacity}. Desired: {entry.Size}");

                    SegmentStream segment = new SegmentStream(_contentStream, _contentStream.Position, entry.Size);
                    return ProtectEntry(entry, segment);
                }
                case FlCompressionMethod.LZS:
                {
                    UInt32 compressedSize = _contentStream.ReadStruct<UInt32>();
                    Int64 capacity = _capacityCalculator.GetCapacity(_contentStream.Position);
                    if (capacity < compressedSize)
                        throw new InvalidDataException($"Not enough capacity available: {capacity}. Desired: {compressedSize}");

                    SegmentStream segment = new SegmentStream(_contentStream, _contentStream.Position, compressedSize);
                    LZSDecompressionStream decompression = new LZSDecompressionStream(segment, compressedSize, entry.Size, leaveOpen: false);
                    return ProtectEntry(entry, decompression);
                }
                case FlCompressionMethod.LZ4:
                {
                    UInt32 compressedSize = _contentStream.ReadStruct<UInt32>();
                    if (compressedSize < 8)
                        throw new InvalidDataException($"Invalid compressed size: {compressedSize}. Minimum size is 8.");
                    compressedSize -= 8;

                    UInt32 magic = _contentStream.ReadStruct<UInt32>(); // LZ4
                    if (magic != 0x5F4C5A34)
                        throw new InvalidDataException($"The magic number is incorrect: {magic}. Expected: 0x5F4C5A34");

                    UInt32 uncompressedSize = _contentStream.ReadStruct<UInt32>();
                    if (uncompressedSize != entry.Size)
                        throw new InvalidDataException($"The uncompressed size is incorrect: {uncompressedSize}. Expected: {entry.Size}");

                    Int64 capacity = _capacityCalculator.GetCapacity(_contentStream.Position);
                    if (capacity < compressedSize)
                        throw new InvalidDataException($"Not enough capacity available: {capacity}. Desired: {compressedSize}");

                    SegmentStream segment = new SegmentStream(_contentStream, _contentStream.Position, compressedSize);
                    LZ4DecompressionStream decompression = new LZ4DecompressionStream(segment, entry.Size, leaveOpen: false);
                    return ProtectEntry(entry, decompression);
                }
                default:
                {
                    throw new NotSupportedException(entry.Compression.ToString());
                }
            }
        }

        private void CloseEntry(FlArchiveEntry entry)
        {
            FlArchiveEntry? previousValue = Interlocked.CompareExchange(ref _openedEntry, null, entry);
            if (ReferenceEquals(previousValue, entry))
                return;

            if (ReferenceEquals(previousValue, null))
                throw new InvalidOperationException("There is no opened entry.");

            throw new InvalidOperationException($"Entry {entry.RelativePath} is not opened.");
        }

        private void RegisterEntryContentPosition(IFlArchiveEntry entry)
        {
            if (entry.Size != 0)
                _capacityCalculator.RegisterBoundary(entry.Offset);
        }

        private void UnregisterEntryContentPosition(IFlArchiveEntry entry)
        {
            if (entry.Size != 0)
                _capacityCalculator.UnregisterBoundary(entry.Offset);
        }

        private void UpdateEntryMetrics(FlArchiveEntry entry)
        {
            _metricsStream.Position = entry.MetricsPosition;
            _metricsStream.WriteStruct(entry.Size);
            _metricsStream.WriteStruct(entry.Offset);
            _metricsStream.WriteStruct((Int32)entry.Compression);
        }

        private void IncreaseContentSize(UInt32 size)
        {
            _capacityCalculator.UnregisterBoundary(_contentStream.Length);
            _contentStream.SetLength(_contentStream.Length + size);
            _capacityCalculator.RegisterBoundary(_contentStream.Length);
        }

        public IFlArchiveEntry AddEntry(String relativePath)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            if (relativePath.Contains('\\'))
                throw new ArgumentException($"Relative path [{relativePath}] contains wrong directory separator. Use '/' instead.");

            FlArchiveEntry entry = new FlArchiveEntry(relativePath, offset: 0, size: 0, FlCompressionMethod.None, metricsPosition: 0, listingPosition: 0);
            if (!_entries.TryAdd(relativePath, entry))
                throw new InvalidOperationException($"The file {relativePath} is already exists inside the archive.");

            entry.AttachToArchive(this);

            EntryCollectionWriter writer = new EntryCollectionWriter(this);
            writer.AppendEntry(entry);
            _hasChanges = true;

            return entry;
        }

        public void RemoveEntry(String relativePath)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            if (!_entries.TryRemove(relativePath, out FlArchiveEntry? entry))
                throw new InvalidOperationException($"The file {relativePath} does not exist inside the archive.");

            entry.DetachFromArchive();

            EntryCollectionWriter writer = new EntryCollectionWriter(this);
            writer.RemoveEntry(entry);
            _hasChanges = true;

            UnregisterEntryContentPosition(entry);
        }

        public void Flush()
        {
            if (!_hasChanges)
                return;

            // The FL listing (.fl) and metrics (.fi) files are kept up to date incrementally by
            // AppendEntry / RemoveEntry. The content (.fs) file is written directly through
            // SegmentStream. All that remains is flushing OS-level write buffers.
            _listingStream.Flush();
            _metricsStream.Flush();
            _contentStream.Flush();

            _hasChanges = false;
        }
    }
}
