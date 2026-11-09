using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public sealed record PcFieldPalettes(short X, short Y, ushort ColorsPerPalette, IReadOnlyList<IReadOnlyList<ushort>> Palettes)
{
    public ushort[][] CreateCachedBackgroundRows(PcFieldBackground background)
    {
        ArgumentNullException.ThrowIfNull(background);
        ushort[][] rows = Palettes.Select(row => row.ToArray()).ToArray();
        for (int row = 0; row < rows.Length; row++)
        {
            if (row >= background.PaletteHasTransparentColor.Count)
            {
                throw new InvalidDataException("FFVII background palette flags do not cover its palette rows.");
            }
            if (background.PaletteHasTransparentColor[row] == 0)
            {
                continue;
            }
            ushort first = rows[row][0];
            for (int index = 0; index < rows[row].Length; index++)
            {
                if (rows[row][index] == 0)
                {
                    rows[row][index] = first;
                }
            }
            rows[row][0] = 0;
        }
        return rows;
    }

    public static PcFieldPalettes Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        uint declared = cursor.U32();
        short x = cursor.I16();
        short y = cursor.I16();
        ushort colors = cursor.U16();
        ushort count = cursor.U16();
        if (declared != source.Length || colors == 0 || (long)colors * count * 2 != cursor.Remaining)
        {
            throw new InvalidDataException("FF7 field palette dimensions or size are invalid.");
        }
        IReadOnlyList<ushort>[] palettes = new IReadOnlyList<ushort>[count];
        for (int i = 0; i < count; i++)
        {
            ushort[] entries = new ushort[colors];
            for (int color = 0; color < entries.Length; color++)
            {
                entries[color] = cursor.U16();
            }
            palettes[i] = Array.AsReadOnly(entries);
        }
        return new PcFieldPalettes(x, y, colors, Array.AsReadOnly(palettes));
    }
}

public sealed record PcFieldTile(int Layer, int Index, short DestinationX, short DestinationY, short SourceX, short SourceY, short SourceX2, short SourceY2, short Width, short Height, short PaletteId, ushort Id, byte Parameter, byte State, byte Blending, byte BlendMode, short TextureId, short TextureId2, short Depth, int Z, int U, int V, ReadOnlyMemory<byte> Raw)
{
    public int PixelSize => Layer > 1 ? 32 : 16;
    public int EffectiveTextureId => Layer > 0 && Blending != 0 ? TextureId2 : TextureId;
    public int EffectiveSourceX => Layer > 0 && Blending != 0 ? SourceX2 : SourceX;
    public int EffectiveSourceY => Layer > 0 && Blending != 0 ? SourceY2 : SourceY;
}
public sealed record PcFieldTexturePage(int Id, ushort BigTile, ushort Depth, ReadOnlyMemory<byte> Pixels)
{
    public int GetIndex(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, 256);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, 256);
        int pixel = y * 256 + x;
        return Depth switch
        {
            0 => (Pixels.Span[pixel / 2] >> ((pixel & 1) * 4)) & 15,
            1 => Pixels.Span[pixel],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(Pixels.Span[(pixel * 2)..]),
            _ => throw new InvalidDataException("Unsupported FF7 background texture depth.")
        };
    }
}
public sealed record PcFieldBackground(short Width, short Height, IReadOnlyList<byte> PaletteHasTransparentColor, IReadOnlyList<IReadOnlyList<PcFieldTile>> Layers, IReadOnlyList<PcFieldTexturePage> Textures)
{
    public ReadOnlyMemory<byte> Footer { get; init; }
    public static PcFieldBackground Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        cursor.Seek(12);
        byte[] paletteFlags = cursor.Take(16).ToArray();
        cursor.Seek(40);
        short width = cursor.I16();
        short height = cursor.I16();
        ushort firstCount = cursor.U16();
        cursor.Seek(52);
        IReadOnlyList<PcFieldTile>[] layers = new IReadOnlyList<PcFieldTile>[4];
        layers[0] = ReadTiles(ref cursor, firstCount, 0);
        for (int layer = 1; layer < 4; layer++)
        {
            int count = 0;
            if (cursor.U8() != 0)
            {
                cursor.Skip(4);
                count = cursor.U16();
                cursor.Skip(layer == 1 ? 20 : 14);
            }
            layers[layer] = ReadTiles(ref cursor, count, layer);
        }
        cursor.Skip(7);
        List<PcFieldTexturePage> pages = new();
        for (int page = 0; page < 42; page++)
        {
            if (cursor.U16() == 0)
            {
                continue;
            }
            ushort bigTile = cursor.U16();
            ushort depth = cursor.U16();
            if (depth > 2)
            {
                throw new InvalidDataException($"FF7 background texture {page} has unsupported depth {depth}.");
            }
            int length = depth == 0 ? 32768 : depth * 65536;
            pages.Add(new PcFieldTexturePage(page, bigTile, depth, cursor.Take(length).ToArray()));
        }
        ReadOnlySpan<byte> trailing = cursor.Take(cursor.Remaining);
        if (trailing.Length < 3 || !trailing[..3].SequenceEqual("END"u8))
        {
            throw new InvalidDataException("FF7 background END marker is absent.");
        }
        return new PcFieldBackground(width, height, Array.AsReadOnly(paletteFlags), Array.AsReadOnly(layers), pages.AsReadOnly()) { Footer = trailing[3..].ToArray() };
    }

    private static IReadOnlyList<PcFieldTile> ReadTiles(ref BinaryCursor cursor, int count, int layer)
    {
        if (count > cursor.Remaining / 52)
        {
            throw new InvalidDataException("FF7 background tile count exceeds its section.");
        }
        PcFieldTile[] tiles = new PcFieldTile[count];
        for (int i = 0; i < count; i++)
        {
            byte[] raw = cursor.Take(52).ToArray();
            BinaryCursor tile = new(raw);
            short dx = tile.I16();
            short dy = tile.I16();
            tile.Skip(4);
            short sx = tile.I16();
            short sy = tile.I16();
            short sx2 = tile.I16();
            short sy2 = tile.I16();
            short width = tile.I16();
            short height = tile.I16();
            short palette = tile.I16();
            ushort id = tile.U16();
            byte parameter = tile.U8();
            byte state = tile.U8();
            byte blending = tile.U8();
            tile.Skip(1);
            byte blendMode = tile.U8();
            tile.Skip(1);
            tiles[i] = new PcFieldTile(layer, i, dx, dy, sx, sy, sx2, sy2, width, height, palette, id, parameter, state, blending, blendMode, tile.I16(), tile.I16(), tile.I16(), tile.I32(), tile.I32(), tile.I32(), raw);
        }
        return Array.AsReadOnly(tiles);
    }
}
