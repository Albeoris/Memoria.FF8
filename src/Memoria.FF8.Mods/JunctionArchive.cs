using System.Buffers.Binary;
using System.Text;
using SharpCompress.Compressors.LZMA;

namespace Memoria.FF8.Mods;

/// <summary>Reads the normal IROS 1.0-1.2 container used by Junction VIII without loading movie payloads into memory.</summary>
public sealed class JunctionArchive
{
    private const int Signature = 0x534f5249;
    private readonly Dictionary<string, JunctionArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    public JunctionArchive(string path)
    {
        SourcePath = Path.GetFullPath(path);
        using FileStream stream = File.OpenRead(SourcePath);
        using BinaryReader reader = new(stream, Encoding.Unicode, true);
        if (stream.Length < 20 || reader.ReadInt32() != Signature)
        {
            throw new InvalidDataException("Junction archive requires an IROS header.");
        }
        int version = reader.ReadInt32();
        if (version < 0x10000 || version > 0x10002)
        {
            throw new NotSupportedException($"Unsupported Junction IROS version 0x{version:X}.");
        }
        if (reader.ReadInt32() != 0)
        {
            throw new NotSupportedException("Junction patch archives are not standalone file replacement packages.");
        }
        long directory = reader.ReadInt32();
        HashSet<long> visited = [];
        List<(long Start, long End)> directoryRanges = [];
        int count;
        while (true)
        {
            if (directory < 16 || directory > stream.Length - 4 || !visited.Add(directory) || visited.Count > 64)
            {
                throw new InvalidDataException("Junction archive has an invalid or cyclic directory chain.");
            }
            stream.Position = directory;
            count = reader.ReadInt32();
            if (count >= 0)
            {
                break;
            }
            if (count != -1 || stream.Length - stream.Position < 8)
            {
                throw new InvalidDataException("Junction archive has an invalid directory redirect.");
            }
            directoryRanges.Add((directory, directory + 12));
            directory = reader.ReadInt64();
        }
        int fixedSize = version == 0x10000 ? 16 : 20;
        if (count > 1_000_000 || count > (stream.Length - stream.Position) / fixedSize)
        {
            throw new InvalidDataException("Junction archive directory entry count is invalid.");
        }
        for (int index = 0; index < count; index++)
        {
            long start = stream.Position;
            if (stream.Length - start < fixedSize)
            {
                throw new InvalidDataException("Junction archive directory is truncated.");
            }
            int size = reader.ReadUInt16();
            int nameSize = reader.ReadUInt16();
            if (nameSize == 0 || nameSize % 2 != 0 || size < fixedSize + nameSize || size > stream.Length - start)
            {
                throw new InvalidDataException("Junction archive directory entry length is invalid.");
            }
            string name = JunctionModImporter.NormalizePath(new UnicodeEncoding(false, false, true).GetString(reader.ReadBytes(nameSize)));
            int flags = reader.ReadInt32();
            if (flags is not (0 or 1 or 2))
            {
                throw new NotSupportedException($"Unsupported Junction archive flags 0x{flags:X} for {name}.");
            }
            long offset = version == 0x10000 ? reader.ReadInt32() : reader.ReadInt64();
            int length = reader.ReadInt32();
            if (offset < 16 || length < 0 || offset > stream.Length - length)
            {
                throw new InvalidDataException($"Junction archive payload is outside its container: {name}.");
            }
            if (!entries.TryAdd(name, new JunctionArchiveEntry(SourcePath, name, flags, offset, length)))
            {
                throw new InvalidDataException($"Duplicate Junction archive path: {name}.");
            }
            stream.Position = start + size;
        }
        directoryRanges.Add((directory, stream.Position));
        foreach (JunctionArchiveEntry entry in entries.Values)
        {
            if (directoryRanges.Any(range => entry.Offset < range.End && entry.Offset + entry.StoredLength > range.Start))
            {
                throw new InvalidDataException($"Junction archive payload overlaps its directory: {entry.RelativePath}.");
            }
        }
        Entries = Array.AsReadOnly(entries.Values.ToArray());
    }

    public string SourcePath { get; }
    public IReadOnlyList<JunctionArchiveEntry> Entries { get; }

    public JunctionArchiveEntry? Find(string relativePath)
    {
        return entries.GetValueOrDefault(JunctionModImporter.NormalizePath(relativePath));
    }
}

public sealed class JunctionArchiveEntry
{
    private readonly string archivePath;
    private readonly int compression;

    internal JunctionArchiveEntry(string archivePath, string relativePath, int compression, long offset, int storedLength)
    {
        this.archivePath = archivePath;
        this.compression = compression;
        RelativePath = relativePath;
        Offset = offset;
        StoredLength = storedLength;
    }

    public string RelativePath { get; }
    internal long Offset { get; }
    internal int StoredLength { get; }

    public ModFileSegment? FileSegment => compression == 0 ? new(archivePath, Offset, StoredLength) : null;

    public long? Length
    {
        get
        {
            if (compression == 0)
            {
                return StoredLength;
            }
            if (compression == 1)
            {
                return null;
            }
            using Stream stream = OpenStored();
            return ReadLzmaHeader(stream).Length;
        }
    }

    public Stream OpenRead()
    {
        Stream input = OpenStored();
        try
        {
            if (compression == 0)
            {
                return input;
            }
            if (compression == 1)
            {
                return new JunctionLzsStream(input);
            }
            (int length, byte[] properties) = ReadLzmaHeader(input);
            return LzmaStream.Create(properties, input, StoredLength - 13, length);
        }
        catch
        {
            input.Dispose();
            throw;
        }
    }

    private Stream OpenStored()
    {
        return new JunctionBoundedStream(archivePath, Offset, StoredLength);
    }

    private static (int Length, byte[] Properties) ReadLzmaHeader(Stream input)
    {
        Span<byte> header = stackalloc byte[13];
        input.ReadExactly(header);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        int propertiesLength = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        uint dictionary = BinaryPrimitives.ReadUInt32LittleEndian(header[9..]);
        if (length < 0 || propertiesLength != 5 || header[8] >= 225 || dictionary > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Junction LZMA header is invalid or exceeds its 64 MiB dictionary limit.");
        }
        return (length, header[8..].ToArray());
    }
}

internal abstract class JunctionReadStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class JunctionBoundedStream : JunctionReadStream
{
    private readonly FileStream input;
    private readonly long length;
    private long remaining;

    public JunctionBoundedStream(string path, long offset, long length)
    {
        input = File.OpenRead(path);
        if (offset < 0 || length < 0 || offset > input.Length - length)
        {
            input.Dispose();
            throw new InvalidDataException("Junction payload changed or was truncated after indexing.");
        }
        input.Position = offset;
        this.length = length;
        remaining = length;
    }

    public override long Length => length;
    public override long Position
    {
        get => length - remaining;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        int read = input.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
        if (read == 0 && remaining != 0 && buffer.Length != 0)
        {
            throw new EndOfStreamException("Junction payload is truncated.");
        }
        remaining -= read;
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            input.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Junction's LZS bit flags and zero-initialized 4096-byte ring, starting at position 4078.</summary>
internal sealed class JunctionLzsStream(Stream input) : JunctionReadStream
{
    private readonly byte[] ring = new byte[4096];
    private int position = 4078;
    private int flags;
    private int bits;
    private int matchPosition;
    private int matchRemaining;
    private long produced;

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        int written = 0;
        while (written < buffer.Length)
        {
            int value;
            if (matchRemaining > 0)
            {
                value = ring[matchPosition];
                matchPosition = (matchPosition + 1) & 4095;
                matchRemaining--;
            }
            else
            {
                if (bits == 0)
                {
                    flags = input.ReadByte();
                    if (flags < 0)
                    {
                        break;
                    }
                    bits = 8;
                }
                value = input.ReadByte();
                if (value < 0)
                {
                    break;
                }
                bool literal = (flags & 1) != 0;
                flags >>= 1;
                bits--;
                if (!literal)
                {
                    int second = input.ReadByte();
                    if (second < 0)
                    {
                        throw new InvalidDataException("Junction LZS back reference is truncated.");
                    }
                    matchPosition = value | ((second & 0xf0) << 4);
                    matchRemaining = (second & 0x0f) + 3;
                    continue;
                }
            }
            if (++produced > int.MaxValue)
            {
                throw new InvalidDataException("Junction LZS decoded size exceeds the supported payload limit.");
            }
            buffer[written++] = (byte)value;
            ring[position] = (byte)value;
            position = (position + 1) & 4095;
        }
        return written;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            input.Dispose();
        }
        base.Dispose(disposing);
    }
}
