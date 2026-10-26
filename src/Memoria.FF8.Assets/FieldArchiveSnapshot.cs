using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

public sealed record FieldArchiveMember(string Name, byte[] Data);

/// <summary>Decodes an in-memory field archive without interpreting archive labels as filesystem paths.</summary>
public static class FieldArchiveSnapshot
{
    /// <summary>Discovers canonical member ownership from the listing without opening index or content data.</summary>
    public static IReadOnlyList<string> ReadMemberNames(string fieldName, byte[] listing)
    {
        ArgumentException.ThrowIfNullOrEmpty(fieldName);
        ArgumentNullException.ThrowIfNull(listing);
        if (!fieldName.All(IsIdentifierCharacter))
        {
            throw new ArgumentException("A field name must be a canonical archive basename.", nameof(fieldName));
        }
        if (listing.Any(value => value > 127 || value < 32 && value is not (10 or 13)))
        {
            throw new InvalidDataException("The field archive listing must contain ASCII paths and line endings.");
        }
        List<string> names = [];
        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
        using (StringReader lines = new(Encoding.ASCII.GetString(listing)))
        {
            while (lines.ReadLine() is string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (!line.StartsWith(FlArchive.InternalPathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The field archive listing has an invalid internal path prefix.");
                }
                string[] components = line[FlArchive.InternalPathPrefix.Length..].Replace('\\', '/').Split('/');
                if (components.Any(component => component.Length == 0 || component is "." or ".." || component.Any(character => character < 32 || character > 126 || ":*?\"<>|".Contains(character))))
                {
                    throw new InvalidDataException("The field archive listing contains an unsafe path label.");
                }
                string name = components[^1].ToLowerInvariant();
                string prefix = fieldName + ".";
                if (name != "chara.one" && (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.Length == prefix.Length || !name[prefix.Length..].All(IsIdentifierCharacter)))
                {
                    throw new InvalidDataException($"Field archive member '{name}' does not belong to '{fieldName}'.");
                }
                if (!unique.Add(name))
                {
                    throw new InvalidDataException($"Field archive member '{name}' has duplicate canonical ownership.");
                }
                names.Add(name);
            }
        }
        return names.AsReadOnly();
    }

    public static IReadOnlyList<FieldArchiveMember> Read(string fieldName, byte[] listing, byte[] index, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(content);
        IReadOnlyList<string> names = ReadMemberNames(fieldName, listing);
        long logicalIndexLength = (long)names.Count * 12;
        if (logicalIndexLength > index.Length || index.AsSpan((int)logicalIndexLength).ContainsAnyExcept((byte)0))
        {
            throw new InvalidDataException("The field archive index does not match its listing and zero-filled preallocated space.");
        }
        long[] boundaries = Enumerable.Range(0, names.Count).Where(number => BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(number * 12)) != 0).Select(number => (long)BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(number * 12 + 4))).Append(content.Length).Distinct().Order().ToArray();
        for (int number = 0; number < names.Count; number++)
        {
            ReadOnlySpan<byte> record = index.AsSpan(number * 12, 12);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(record);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
            int compression = BinaryPrimitives.ReadInt32LittleEndian(record[8..]);
            if (compression is < 0 or > 2 || size > Array.MaxLength)
            {
                throw new InvalidDataException($"Field archive member '{names[number]}' has unsupported metrics.");
            }
            // Native empty entries have no frame and may carry a sentinel offset.
            if (size == 0)
            {
                continue;
            }
            long end;
            if (compression == 0)
            {
                end = (long)offset + size;
            }
            else
            {
                int headerSize = compression == 1 ? 4 : 12;
                if ((long)offset + headerSize > content.Length)
                {
                    throw new InvalidDataException($"Field archive member '{names[number]}' has a truncated compression frame.");
                }
                ReadOnlySpan<byte> frame = content.AsSpan((int)offset);
                uint framedSize = BinaryPrimitives.ReadUInt32LittleEndian(frame);
                if (compression == 2 && (framedSize < 8 || BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]) != 0x5F4C5A34 || BinaryPrimitives.ReadUInt32LittleEndian(frame[8..]) != size))
                {
                    throw new InvalidDataException($"Field archive member '{names[number]}' has an invalid LZ4 frame.");
                }
                end = (long)offset + 4 + framedSize;
            }
            if (end > content.Length)
            {
                throw new InvalidDataException($"Field archive member '{names[number]}' exceeds the content bounds.");
            }
            int boundary = Array.BinarySearch(boundaries, (long)offset);
            if (end > boundaries[boundary + 1])
            {
                throw new InvalidDataException($"Field archive member '{names[number]}' overlaps the next content allocation.");
            }
        }
        using MemoryStream listingStream = new(listing, writable: false);
        using MemoryStream indexStream = new(index, writable: false);
        using MemoryStream contentStream = new(content, writable: false);
        using IFlArchive archive = FlArchive.Open(listingStream, indexStream, contentStream, leaveOpen: true, FlArchiveRepresentation.Files);
        List<FieldArchiveMember> result = new(names.Count);
        for (int number = 0; number < names.Count; number++)
        {
            IFlArchiveEntry entry = archive.Entries[number];
            byte[] data = new byte[(int)entry.Size];
            if (data.Length > 0)
            {
                try
                {
                    using Stream decoded = entry.OpenForRead();
                    decoded.ReadExactly(data);
                    if (decoded.ReadByte() != -1)
                    {
                        throw new InvalidDataException("The decoded member exceeds its declared size.");
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    throw new InvalidDataException($"Field archive member '{names[number]}' could not be decoded completely.", exception);
                }
            }
            result.Add(new(names[number], data));
        }
        return result.AsReadOnly();
    }

    private static bool IsIdentifierCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value is '_' or '-';
}
