using System.Text;

namespace Memoria.FF7.Formats;

public sealed record LgpEntry(int Index, string Name, string ConflictPath, long HeaderOffset, int Length, byte Check, ushort ConflictGroup)
{
    public string FullPath => string.IsNullOrEmpty(ConflictPath) ? Name : $"{ConflictPath}/{Name}";
    public long DataOffset => HeaderOffset + 24;
}

public sealed class LgpArchive : IDisposable
{
    private readonly Stream source;
    private readonly bool leaveOpen;
    private readonly object sync = new();

    public LgpArchive(Stream source, bool leaveOpen = false, int maximumEntries = 1_000_000)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEntries);
        if (!source.CanRead || !source.CanSeek)
        {
            throw new ArgumentException("An LGP source must be readable and seekable.", nameof(source));
        }
        this.source = source;
        this.leaveOpen = leaveOpen;
        try
        {
            Entries = ReadDirectory(maximumEntries);
        }
        catch
        {
            if (!leaveOpen)
            {
                source.Dispose();
            }
            throw;
        }
    }

    public IReadOnlyList<LgpEntry> Entries { get; }

    public static LgpArchive Open(string path, int maximumEntries = 1_000_000)
    {
        return new LgpArchive(File.OpenRead(path), maximumEntries: maximumEntries);
    }

    public IReadOnlyList<LgpEntry> Find(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        string normalized = fullPath.Replace('\\', '/');
        return Array.AsReadOnly(Entries.Where(entry => StringComparer.OrdinalIgnoreCase.Equals(entry.FullPath, normalized)).ToArray());
    }

    public byte[] ReadEntry(LgpEntry entry, int maximumBytes = 64 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (entry.Index < 0 || entry.Index >= Entries.Count || !ReferenceEquals(Entries[entry.Index], entry))
        {
            throw new ArgumentException("The entry belongs to a different LGP directory.", nameof(entry));
        }
        if (entry.Length > maximumBytes)
        {
            throw new InvalidDataException($"LGP member {entry.Index} exceeds read limit {maximumBytes}.");
        }
        lock (sync)
        {
            source.Position = entry.DataOffset;
            byte[] data = new byte[entry.Length];
            source.ReadExactly(data);
            return data;
        }
    }

    public void Dispose()
    {
        if (!leaveOpen)
        {
            source.Dispose();
        }
    }

    private IReadOnlyList<LgpEntry> ReadDirectory(int maximumEntries)
    {
        source.Position = 0;
        using BinaryReader reader = new(source, Encoding.ASCII, true);
        try
        {
            if (reader.ReadUInt16() != 0 || ReadName(reader, 10) != "SQUARESOFT")
            {
                throw new InvalidDataException("Invalid FF7 LGP signature.");
            }
            uint count = reader.ReadUInt32();
            if (count > maximumEntries || 16L + count * 27L + 3602 > source.Length)
            {
                throw new InvalidDataException("LGP directory count exceeds the input or configured limit.");
            }
            LgpEntry[] entries = new LgpEntry[(int)count];
            for (int i = 0; i < entries.Length; i++)
            {
                string name = ReadName(reader, 20);
                long offset = reader.ReadUInt32();
                byte check = reader.ReadByte();
                ushort conflict = reader.ReadUInt16();
                entries[i] = new LgpEntry(i, name, "", offset, 0, check, conflict);
            }
            source.Seek(3600, SeekOrigin.Current);
            ushort groups = reader.ReadUInt16();
            HashSet<int> assignedPaths = new();
            for (int group = 1; group <= groups; group++)
            {
                ushort conflicts = reader.ReadUInt16();
                for (int i = 0; i < conflicts; i++)
                {
                    string path = ReadName(reader, 128).Replace('\\', '/').TrimEnd('/');
                    ushort index = reader.ReadUInt16();
                    if (index >= entries.Length || entries[index].ConflictGroup != group || !assignedPaths.Add(index))
                    {
                        throw new InvalidDataException("LGP conflict table references an invalid or repeated directory entry.");
                    }
                    entries[index] = entries[index] with { ConflictPath = path };
                }
            }
            long directoryEnd = source.Position;
            foreach (LgpEntry entry in entries)
            {
                if (entry.ConflictGroup > groups || (entry.ConflictGroup != 0 && !assignedPaths.Contains(entry.Index)))
                {
                    throw new InvalidDataException($"LGP member {entry.Index} has no matching conflict path.");
                }
                if (entry.HeaderOffset < directoryEnd || entry.HeaderOffset + 24 > source.Length)
                {
                    throw new InvalidDataException($"LGP member {entry.Index} header is outside the data area.");
                }
                source.Position = entry.HeaderOffset;
                string storedName = ReadName(reader, 20);
                uint length = reader.ReadUInt32();
                if (!StringComparer.OrdinalIgnoreCase.Equals(storedName, entry.Name) || length > int.MaxValue || entry.DataOffset + length > source.Length)
                {
                    throw new InvalidDataException($"LGP member {entry.Index} name or length is invalid.");
                }
                entries[entry.Index] = entry with { Length = (int)length };
            }
            return Array.AsReadOnly(entries);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("FF7 LGP directory or member header is truncated.", exception);
        }
    }

    private static string ReadName(BinaryReader reader, int length)
    {
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException();
        }
        int end = Array.IndexOf(bytes, (byte)0);
        return Encoding.ASCII.GetString(bytes, 0, end < 0 ? length : end).TrimEnd(' ');
    }
}
