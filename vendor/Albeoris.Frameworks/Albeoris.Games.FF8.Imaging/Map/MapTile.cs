using System.Buffers.Binary;

namespace Albeoris.Games.FF8.Imaging.Map;

/// <summary>A lossless 16-byte FF8 field tile record.</summary>
public sealed class MapTile
{
    public const Int32 Size = 16;
    private readonly Byte[] _data;

    public MapTile() => _data = new Byte[Size];

    private MapTile(Byte[] data) => _data = data;

    public Int16 X { get => ReadInt16(0); set => WriteInt16(0, value); }
    public Int16 Y { get => ReadInt16(2); set => WriteInt16(2, value); }
    public Int16 Z { get => ReadInt16(4); set => WriteInt16(4, value); }
    public Byte PackedTexture { get => _data[6]; set => _data[6] = value; }
    public Byte Unknown { get => _data[7]; set => _data[7] = value; }
    public UInt16 PackedPalette { get => ReadUInt16(8); set => WriteUInt16(8, value); }
    public Byte SourceX { get => _data[10]; set => _data[10] = value; }
    public Byte SourceY { get => _data[11]; set => _data[11] = value; }
    public Byte PackedLayer { get => _data[12]; set => _data[12] = value; }
    public MapBlendMode BlendMode { get => (MapBlendMode)_data[13]; set => _data[13] = (Byte)value; }
    public Byte Parameter { get => _data[14]; set => _data[14] = value; }
    public Byte State { get => _data[15]; set => _data[15] = value; }

    public Int32 TextureId
    {
        get => PackedTexture & 0x0F;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 0x0F);
            PackedTexture = (Byte)((PackedTexture & 0xF0) | value);
        }
    }

    public Int32 BlendFlag => (PackedTexture >> 4) & 1;
    public Int32 BitsPerIndexSelector => (PackedTexture >> 5) & 7;
    public Boolean Uses8BitIndices => BitsPerIndexSelector > 3;

    public Int32 PaletteId
    {
        get => (PackedPalette >> 6) & 0x0F;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 0x0F);
            PackedPalette = (UInt16)((PackedPalette & ~(0x0F << 6)) | (value << 6));
        }
    }

    public Int32 LayerId
    {
        get => PackedLayer >> 7;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            PackedLayer = (Byte)((PackedLayer & 0x7F) | (value << 7));
        }
    }

    public Int16 SimpleSourceX { get => ReadInt16(4); set => WriteInt16(4, value); }
    public Int16 SimpleSourceY { get => ReadInt16(6); set => WriteInt16(6, value); }
    public Int16 SimpleZ { get => ReadInt16(8); set => WriteInt16(8, value); }

    public Byte[] ToArray() => (Byte[])_data.Clone();

    internal static MapTile Read(ReadOnlySpan<Byte> content)
    {
        if (content.Length < Size)
            throw new InvalidDataException($"A MAP tile requires {Size} bytes.");
        return new MapTile(content[..Size].ToArray());
    }

    internal void CopyTo(Span<Byte> destination) => _data.CopyTo(destination);

    private Int16 ReadInt16(Int32 offset) => BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(offset));
    private UInt16 ReadUInt16(Int32 offset) => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset));
    private void WriteInt16(Int32 offset, Int16 value) => BinaryPrimitives.WriteInt16LittleEndian(_data.AsSpan(offset), value);
    private void WriteUInt16(Int32 offset, UInt16 value) => BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(offset), value);
}
