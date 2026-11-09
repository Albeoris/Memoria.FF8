using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public enum PcFieldSectionKind
{
    ScriptAndText,
    Camera,
    Models,
    Palettes,
    Walkmesh,
    TileMap,
    Encounters,
    TriggersAndGateways,
    Background
}

public sealed record PcFieldSection(PcFieldSectionKind Kind, int HeaderOffset, int DataOffset, int Length);

public sealed class PcFieldFile
{
    private readonly byte[] data;

    private PcFieldFile(byte[] data, ushort reserved, PcFieldSection[] sections)
    {
        this.data = data;
        ReservedHeader = reserved;
        Sections = Array.AsReadOnly(sections);
    }

    public ushort ReservedHeader { get; }
    public int DecodedLength => data.Length;
    public IReadOnlyList<PcFieldSection> Sections { get; }

    public ReadOnlyMemory<byte> GetSection(PcFieldSectionKind kind)
    {
        int index = (int)kind;
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Sections.Count);
        PcFieldSection section = Sections[index];
        return data.AsMemory(section.DataOffset, section.Length);
    }

    public static PcFieldFile ReadCompressed(ReadOnlySpan<byte> source, int maximumOutputBytes = Lzss.DefaultMaximumOutputBytes)
    {
        return ReadDecoded(Lzss.DecodeLengthFramed(source, maximumOutputBytes));
    }

    public static PcFieldFile ReadDecoded(ReadOnlySpan<byte> source)
    {
        const int headerLength = 42;
        if (source.Length < headerLength || BinaryPrimitives.ReadUInt32LittleEndian(source[2..]) != 9)
        {
            throw new InvalidDataException("FF7 PC field requires a two-byte header and nine section offsets.");
        }
        PcFieldSection[] sections = new PcFieldSection[9];
        long previousEnd = headerLength;
        for (int i = 0; i < sections.Length; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(source[(6 + i * 4)..]);
            if (offset < previousEnd || (long)offset + 4 > source.Length)
            {
                throw new InvalidDataException($"FF7 PC field section {i + 1} offset is invalid or overlaps a prior section.");
            }
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(source[(int)offset..]);
            previousEnd = (long)offset + 4 + length;
            if (previousEnd > source.Length)
            {
                throw new InvalidDataException($"FF7 PC field section {i + 1} extends outside the decoded member.");
            }
            sections[i] = new PcFieldSection((PcFieldSectionKind)i, (int)offset, (int)offset + 4, (int)length);
        }
        return new PcFieldFile(source.ToArray(), BinaryPrimitives.ReadUInt16LittleEndian(source), sections);
    }
}
