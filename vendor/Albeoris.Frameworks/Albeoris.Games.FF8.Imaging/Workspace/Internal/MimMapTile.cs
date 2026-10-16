using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Map;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class MimMapTile
{
    public const Int32 Size = 16;
    public const Int32 LogoSize = 14;
    public const Int32 PixelSize = 16;

    private MimMapTile(Int32 index, Int16 x, Int16 y, UInt16 z, UInt16 texture, UInt16 palette, UInt16 sourceX, UInt16 sourceY, Byte layer, MapBlendMode blendMode, Byte animationId, Byte animationState)
    {
        Index = index;
        X = x;
        Y = y;
        Z = z;
        Texture = texture;
        Palette = palette;
        SourceX = sourceX;
        SourceY = sourceY;
        Layer = layer;
        BlendMode = blendMode;
        AnimationId = animationId;
        AnimationState = animationState;
    }

    public Int32 Index { get; }
    public Int16 X { get; }
    public Int16 Y { get; }
    public UInt16 Z { get; }
    public UInt16 Texture { get; }
    public UInt16 Palette { get; }
    public UInt16 SourceX { get; }
    public UInt16 SourceY { get; }
    public Byte Layer { get; }
    public MapBlendMode BlendMode { get; }
    public Byte AnimationId { get; }
    public Byte AnimationState { get; }
    public Int32 TextureId => Texture & 0x0F;
    public Boolean Draw => (Texture & 0x10) != 0;
    public Int32 BitsPerPixel => ((Texture >> 7) & 0x03) switch { 0 => 4, 1 => 8, 2 => 16, _ => 24 };
    public Int32 PaletteId => (Palette >> 6) & 0x0F;

    public static MimMapTile Read(ReadOnlySpan<Byte> data, Int32 index, MimMapTileFormat format)
    {
        Int16 x = BinaryPrimitives.ReadInt16LittleEndian(data);
        Int16 y = BinaryPrimitives.ReadInt16LittleEndian(data[2..]);
        return format switch
        {
            MimMapTileFormat.Standard => new MimMapTile(index, x, y, BinaryPrimitives.ReadUInt16LittleEndian(data[4..]), BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..]), data[10], data[11], (Byte)(data[12] >> 1), (MapBlendMode)data[13], data[14], data[15]),
            MimMapTileFormat.Alternate => new MimMapTile(index, x, y, BinaryPrimitives.ReadUInt16LittleEndian(data[8..]), BinaryPrimitives.ReadUInt16LittleEndian(data[10..]), BinaryPrimitives.ReadUInt16LittleEndian(data[12..]), BinaryPrimitives.ReadUInt16LittleEndian(data[4..]), BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), 0, MapBlendMode.None, data[14], data[15]),
            MimMapTileFormat.Logo => new MimMapTile(index, x, y, BinaryPrimitives.ReadUInt16LittleEndian(data[8..]), BinaryPrimitives.ReadUInt16LittleEndian(data[10..]), BinaryPrimitives.ReadUInt16LittleEndian(data[12..]), BinaryPrimitives.ReadUInt16LittleEndian(data[4..]), BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), 0, MapBlendMode.None, Byte.MaxValue, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }
}

internal enum MimMapTileFormat
{
    Standard,
    Alternate,
    Logo,
}
