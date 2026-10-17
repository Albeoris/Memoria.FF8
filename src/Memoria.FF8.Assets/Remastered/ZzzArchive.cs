using System.Collections.ObjectModel;
using System.Text;

namespace Memoria.FF8.Assets.Remastered;

public sealed record ZzzArchiveEntry(string Path, long Offset, long Length);

/// <summary>Reads the Remastered ZZZ index and opens independent, bounded entry streams.</summary>
public sealed class ZzzArchive : IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string path;
    private readonly FileStream archive;
    private bool disposed;

    public ZzzArchive(string path)
    {
        this.path = System.IO.Path.GetFullPath(path);
        archive = new FileStream(this.path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            using BinaryReader reader = new(archive, StrictUtf8, true);
            RequireRemaining(archive, 4);
            uint count = reader.ReadUInt32();
            if (count > int.MaxValue || count > (ulong)(archive.Length - archive.Position) / 17)
            {
                throw new InvalidDataException("ZZZ entry count exceeds the index bounds.");
            }
            Dictionary<string, ZzzArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
            for (uint index = 0; index < count; index++)
            {
                RequireRemaining(archive, 4);
                uint nameLength = reader.ReadUInt32();
                if (nameLength == 0 || nameLength > 32767 || nameLength > (ulong)Math.Max(0, archive.Length - archive.Position - 12))
                {
                    throw new InvalidDataException("ZZZ entry name exceeds the supported path or index bounds.");
                }
                string name;
                try
                {
                    name = NormalizePath(StrictUtf8.GetString(reader.ReadBytes((int)nameLength)));
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException("ZZZ entry name is not valid UTF-8.", exception);
                }
                ulong offset = reader.ReadUInt64();
                uint length = reader.ReadUInt32();
                if (offset > (ulong)archive.Length || length > (ulong)archive.Length - offset)
                {
                    throw new InvalidDataException($"ZZZ entry {name} exceeds the archive bounds.");
                }
                if (!entries.TryAdd(name, new ZzzArchiveEntry(name, (long)offset, length)))
                {
                    throw new InvalidDataException($"Duplicate ZZZ entry {name}.");
                }
            }
            long previousEnd = archive.Position;
            foreach (ZzzArchiveEntry entry in entries.Values.OrderBy(entry => entry.Offset).ThenBy(entry => entry.Length))
            {
                if (entry.Offset < previousEnd)
                {
                    throw new InvalidDataException($"ZZZ entry {entry.Path} overlaps the index or another entry.");
                }
                previousEnd = entry.Offset + entry.Length;
            }
            Entries = new ReadOnlyDictionary<string, ZzzArchiveEntry>(entries);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public IReadOnlyDictionary<string, ZzzArchiveEntry> Entries { get; }

    public ZzzArchiveEntry? Find(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return Entries.GetValueOrDefault(NormalizePath(path));
    }

    public Stream Open(string path)
    {
        ZzzArchiveEntry entry = Find(path) ?? throw new FileNotFoundException($"ZZZ entry {path} was not found.");
        return Open(entry);
    }

    public Stream Open(ZzzArchiveEntry entry)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(entry);
        if (!Entries.TryGetValue(entry.Path, out ZzzArchiveEntry? stored) || stored != entry)
        {
            throw new ArgumentException("The entry does not belong to this ZZZ archive.", nameof(entry));
        }
        FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new EntryStream(file, entry.Offset, entry.Length);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        archive.Dispose();
    }

    private static string NormalizePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        if (normalized.Length == 0 || parts.Any(part => part.Length == 0 || part is "." or "..") || normalized.Any(character => char.IsControl(character) || character is ':' or '<' or '>' or '"' or '|' or '?' or '*'))
        {
            throw new InvalidDataException("ZZZ paths must be relative paths without traversal or invalid characters.");
        }
        return normalized;
    }

    private static void RequireRemaining(Stream stream, long count)
    {
        if (count > stream.Length - stream.Position)
        {
            throw new InvalidDataException("Truncated ZZZ index.");
        }
    }

    private sealed class EntryStream : Stream
    {
        private readonly FileStream file;
        private readonly long offset;
        private readonly long length;
        private long position;
        private bool disposed;

        public EntryStream(FileStream file, long offset, long length)
        {
            this.file = file;
            this.offset = offset;
            this.length = length;
            file.Position = offset;
        }

        public override bool CanRead => !disposed;
        public override bool CanSeek => !disposed;
        public override bool CanWrite => false;
        public override long Length
        {
            get
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return length;
            }
        }
        public override long Position
        {
            get
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return position;
            }
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            int count = (int)Math.Min(buffer.Length, length - position);
            int read = file.Read(buffer[..count]);
            if (read == 0 && count != 0)
            {
                throw new InvalidDataException("Truncated ZZZ entry payload.");
            }
            position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            long basis = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => position,
                SeekOrigin.End => length,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (offset < -basis || offset > length - basis)
            {
                throw new IOException("Cannot seek outside the ZZZ entry.");
            }
            position = basis + offset;
            file.Position = this.offset + position;
            return position;
        }

        public override void Flush()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
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
            if (disposing && !disposed)
            {
                disposed = true;
                file.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
