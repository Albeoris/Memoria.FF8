using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

/// <summary>An exact external game-data address selected by the host's startup package order.</summary>
public sealed record RemasteredLooseDataFile(string Path, string Origin, long Revision, Func<FileStream> OpenVersion);

public sealed record RemasteredExternalFileInfo(string Path, long Length, string Origin, long Revision);

public sealed partial class RemasteredGameDataSource
{
    private sealed record ExternalFile(long Length, long Revision, string Origin, Func<Stream> OpenRead, string? PhysicalPath = null);
    private sealed record HeldLooseFile(RemasteredLooseDataFile Source, FileStream Lease, long Length, DateTime LastWrite)
    {
        public void ValidateVersion()
        {
            if (!OperatingSystem.IsWindows() && (RandomAccess.GetLength(Lease.SafeFileHandle) != Length || File.GetLastWriteTimeUtc(Lease.SafeFileHandle) != LastWrite))
            {
                throw new IOException($"Mounted data changed after startup: {Source.Origin}. Restart the game to use the new file.");
            }
        }
    }
    private static readonly string[] TripletExtensions = [".fl", ".fi", ".fs"];
    private Dictionary<string, HeldLooseFile> looseData = new(StringComparer.OrdinalIgnoreCase);
    private bool looseDataSealed;
    private bool looseDataPrepared;

    public void PrepareLooseData(IEnumerable<RemasteredLooseDataFile> files)
    {
        lock (archiveGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (looseDataSealed || looseDataPrepared || archives.Count != 0 || catalog.Count != 0 || this.files.Count != 0)
            {
                throw new InvalidOperationException("Remastered loose data must be prepared before reading game resources.");
            }
            Dictionary<string, HeldLooseFile> selected = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (RemasteredLooseDataFile file in files)
                {
                    string path = new ResourceKey(file.Path).Path;
                    if (!path.StartsWith("data/", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Loose game data must be below data/: {file.Path}", nameof(files));
                    }
                    if (!selected.ContainsKey(path))
                    {
                        FileStream lease = file.OpenVersion();
                        try
                        {
                            selected.Add(path, new(file, lease, lease.Length, File.GetLastWriteTimeUtc(lease.SafeFileHandle)));
                        }
                        catch
                        {
                            lease.Dispose();
                            throw;
                        }
                    }
                }
                looseData = selected;
                foreach (string stem in selected.Keys.Where(IsOuterTripletPath).Select(path => path[..^3]).Distinct(StringComparer.OrdinalIgnoreCase).Where(stem => Contains(stem + ".fl")))
                {
                    ValidateTriplet(stem);
                }
                looseDataPrepared = true;
            }
            catch
            {
                DisposeLooseFiles(selected);
                throw;
            }
        }
    }

    public void SealLooseData()
    {
        lock (archiveGate)
        {
            looseDataSealed = true;
        }
    }

    public void DiscardPreparedLooseData()
    {
        lock (archiveGate)
        {
            if (!looseDataSealed && looseDataPrepared)
            {
                looseDataPrepared = false;
                TableOverrides = null;
                ClearMountedCaches();
                DisposeLooseFiles(looseData);
            }
        }
    }

    public RemasteredExternalFileInfo? DescribeExternalData(string relativePath)
    {
        lock (archiveGate)
        {
            string path = new ResourceKey(relativePath).Path;
            ExternalFile? file = FindExternalFile(path);
            return file is null ? null : new(path, file.Length, file.Origin, file.Revision);
        }
    }

    public bool ContainsExternalData(string relativePath)
    {
        return DescribeExternalData(relativePath) is not null;
    }

    private ExternalFile? FindExternalFile(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (looseData.TryGetValue(path, out HeldLooseFile? file))
        {
            return new(file.Length, file.Source.Revision, file.Source.Origin, () => new BufferedStream(new LooseReadStream(file), 64 * 1024), file.Lease.Name);
        }
        return FindStockFile(path);
    }

    private string ArchiveOrigin(string family, ResourceKey key)
    {
        string localized = $"data/lang-{language}/{family}";
        string stem = Contains(localized + ".fl") ? localized : $"data/{family}";
        string[] sources = TripletExtensions.Where(extension => looseData.ContainsKey(stem + extension))
            .Select(extension => looseData[stem + extension].Source.Origin)
            .ToArray();
        return sources.Length == 0 ? $"{Descriptor.Id}:{key}" : $"{string.Join(", ", sources)} → {key}";
    }

    private static bool IsOuterTripletPath(string path)
    {
        string[] parts = path.Split('/');
        return (parts.Length == 2 || parts.Length == 3 && parts[1].StartsWith("lang-", StringComparison.Ordinal)) &&
            TripletExtensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    private void ValidateTriplet(string stem)
    {
        ExternalFile?[] parts = TripletExtensions.Select(extension => FindExternalFile(stem + extension)).ToArray();
        string sources = string.Join(", ", parts.Select((file, index) => file?.Origin ?? (stem + TripletExtensions[index] + " (missing)")));
        try
        {
            using IFlArchive archive = OpenTriplet(stem);
            using Stream metrics = Open(stem + ".fi");
            using BinaryReader reader = new(metrics);
            byte[] buffer = new byte[64 * 1024];
            foreach (IFlArchiveEntry entry in archive.Entries)
            {
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                FlCompressionMethod compression = (FlCompressionMethod)reader.ReadUInt32();
                if (entry.Size == 0)
                {
                    continue;
                }
                if (entry.Offset >= parts[2]!.Length)
                {
                    throw new InvalidDataException($"{entry.RelativePath} starts beyond the archive content.");
                }
                using Stream content = entry.OpenForRead();
                if (compression != FlCompressionMethod.None)
                {
                    long decoded = 0;
                    int read;
                    while ((read = content.Read(buffer)) != 0)
                    {
                        decoded += read;
                    }
                    if (decoded != entry.Size)
                    {
                        throw new InvalidDataException($"{entry.RelativePath} decoded {decoded} bytes; the index declares {entry.Size}.");
                    }
                }
            }
            Span<byte> remainder = stackalloc byte[512];
            int count;
            while ((count = metrics.Read(remainder)) != 0)
            {
                if (remainder[..count].ContainsAnyExcept((byte)0))
                {
                    throw new InvalidDataException("The index contains records beyond the archive listing.");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            throw new InvalidDataException($"Incompatible archive '{stem}' ({sources}): {exception.Message}", exception);
        }
    }

    private sealed class LooseReadStream(HeldLooseFile file) : Stream
    {
        private long position;
        private bool closed;

        public override bool CanRead => !closed;
        public override bool CanSeek => !closed;
        public override bool CanWrite => false;
        public override long Length => file.Length;

        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            file.ValidateVersion();
            int boundedLength = (int)Math.Min(buffer.Length, Math.Max(0, Length - position));
            int count = boundedLength == 0 ? 0 : RandomAccess.Read(file.Lease.SafeFileHandle, buffer[..boundedLength], position);
            file.ValidateVersion();
            position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            long basis = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => position,
                SeekOrigin.End => Length,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            long next = checked(basis + offset);
            if (next < 0)
            {
                throw new IOException("Cannot seek before the start of mounted data.");
            }
            position = next;
            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            closed = true;
            base.Dispose(disposing);
        }
    }

    private static void DisposeLooseFiles(Dictionary<string, HeldLooseFile> files)
    {
        foreach (HeldLooseFile file in files.Values)
        {
            file.Lease.Dispose();
        }
        files.Clear();
    }
}
