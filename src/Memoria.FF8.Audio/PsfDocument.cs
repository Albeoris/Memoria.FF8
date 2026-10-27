using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Memoria.FF8.Audio;

public sealed record PsfMetadata(TimeSpan Length, TimeSpan Fade, double Volume, IReadOnlyDictionary<string, string> Tags);

/// <summary>Validated PSF1 container, independent of any installed resource filesystem.</summary>
public sealed class PsfDocument
{
    public byte[] Program { get; }
    public PsfMetadata Metadata { get; }
    public IReadOnlyList<string> Libraries { get; }

    private PsfDocument(byte[] program, Dictionary<string, string> tags, List<string> libraries)
    {
        Program = program;
        Libraries = libraries.AsReadOnly();
        TimeSpan length = tags.TryGetValue("length", out string? l) ? ParseTime(l) : TimeSpan.FromSeconds(170);
        TimeSpan fade = tags.TryGetValue("fade", out string? f) ? ParseTime(f) : TimeSpan.FromSeconds(10);
        double volume = 1;
        if (tags.TryGetValue("volume", out string? v) && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out volume))
        {
            throw new InvalidDataException("Invalid PSF volume tag.");
        }
        if (!double.IsFinite(volume) || volume < 0 || volume > 64 || length + fade > TimeSpan.FromHours(24))
        {
            throw new InvalidDataException("PSF playback metadata exceeds supported limits.");
        }
        Metadata = new(length, fade, volume, new ReadOnlyDictionary<string, string>(tags));
    }

    public static PsfDocument Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16 || !data[..3].SequenceEqual("PSF"u8))
        {
            throw new InvalidDataException("Invalid PSF header.");
        }
        if (data[3] != 1)
        {
            throw new NotSupportedException("Only PSF1 and miniPSF1 are supported.");
        }
        long offset = 16L + BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        long size = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        if (data.Length > 16 * 1024 * 1024 || offset + size > data.Length)
        {
            throw new InvalidDataException("Invalid PSF section bounds.");
        }
        ReadOnlySpan<byte> compressed = data.Slice((int)offset, (int)size);
        uint crc = uint.MaxValue;
        foreach (byte b in compressed)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
            }
        }
        if (~crc != BinaryPrimitives.ReadUInt32LittleEndian(data[12..]))
        {
            throw new InvalidDataException("PSF compressed program CRC does not match.");
        }
        byte[] program = [];
        if (size != 0)
        {
            using var inflater = new ZLibStream(new MemoryStream(compressed.ToArray()), CompressionMode.Decompress);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = inflater.Read(buffer)) != 0)
            {
                if (output.Length + read > 2048 + 0x1f0000)
                {
                    throw new InvalidDataException("PSF decompressed program exceeds PS1 RAM.");
                }
                output.Write(buffer, 0, read);
            }
            program = output.ToArray();
            if (program.Length < 2048 || !program.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8))
            {
                throw new InvalidDataException("PSF program has no PS-X EXE header.");
            }
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(program.AsSpan(0x18)) & 0x1fffff;
            if (address < 0x10000 || address + program.Length - 2048 > 0x200000)
            {
                throw new InvalidDataException("PSF program lies outside supported PS1 RAM.");
            }
        }
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ReadOnlySpan<byte> tail = data[(int)(offset + size)..];
        if (tail.StartsWith("[TAG]"u8))
        {
            string text = Encoding.UTF8.GetString(tail[5..]);
            foreach (string line in text.Split('\n'))
            {
                int equal = line.IndexOf('=');
                if (equal < 1)
                {
                    continue;
                }
                string key = line[..equal].Trim();
                string value = line[(equal + 1)..].Trim();
                if (!tags.TryAdd(key, value))
                {
                    throw new InvalidDataException($"Duplicate PSF tag: {key}.");
                }
                if (key.StartsWith('_') && !key.Equals("_refresh", StringComparison.OrdinalIgnoreCase) && !key.Equals("_utf8", StringComparison.OrdinalIgnoreCase) && !IsLibraryTag(key))
                {
                    throw new NotSupportedException($"Unsupported required PSF tag: {key}.");
                }
            }
        }
        var libraries = new List<string>();
        if (tags.TryGetValue("_lib", out string? primary))
        {
            libraries.Add(primary);
        }
        for (int n = 2; tags.TryGetValue("_lib" + n, out string? library); n++)
        {
            libraries.Add(library);
        }
        return new PsfDocument(program, tags, libraries);
    }

    private static bool IsLibraryTag(string key)
    {
        return key.Equals("_lib", StringComparison.OrdinalIgnoreCase) || (key.StartsWith("_lib", StringComparison.OrdinalIgnoreCase) && int.TryParse(key.AsSpan(4), out int n) && n >= 2 && n <= 128);
    }

    public static TimeSpan ParseTime(string value)
    {
        string[] parts = value.Replace(',', '.').Split(':');
        if (parts.Length is < 1 or > 3)
        {
            throw new InvalidDataException("Invalid PSF time tag.");
        }
        double seconds = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double part) || !double.IsFinite(part) || part < 0 || (i != 0 && part >= 60) || (i < parts.Length - 1 && part != Math.Truncate(part)))
            {
                throw new InvalidDataException("Invalid PSF time tag.");
            }
            seconds = seconds * 60 + part;
        }
        if (seconds > 86400)
        {
            throw new InvalidDataException("PSF time exceeds supported duration.");
        }
        return TimeSpan.FromSeconds(seconds);
    }
}
