using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Reconstructs source VRAM uploads, before any replacement or optimized runtime atlas.</summary>
internal sealed class TonberryWorldTextureSource
{
    private readonly byte[] vram = new byte[1024 * 512 * 2];
    public IReadOnlyList<TimImage> Roads { get; }

    public TonberryWorldTextureSource(ResourceContent content)
    {
        using Stream source = content.OpenRead();
        using MemoryStream data = new();
        source.CopyTo(data);
        byte[] bytes = data.ToArray();
        List<TimImage> roads = [];
        foreach (int section in new[] { 37, 38 })
        {
            int start = Read(bytes, section * 4);
            for (int i = 0; i < 256; i++)
            {
                int pointer = Read(bytes, start + i * 4);
                if (pointer == 0)
                {
                    break;
                }
                int offset = checked(start + pointer);
                int position = offset + 8;
                if ((Read(bytes, offset + 4) & 8) != 0)
                {
                    position = checked(position + Read(bytes, position));
                }
                int end = checked(position + Read(bytes, position));
                TimImage tim = TimFile.Read(bytes.AsSpan(offset, end - offset)).Images.Single();
                Upload(tim);
                if (section == 38)
                {
                    roads.Add(tim);
                }
            }
        }
        Roads = roads.AsReadOnly();
    }

    public PixelImage Page(TimImage tim)
    {
        if (tim.PixelMode != TimPixelMode.Indexed4)
        {
            throw new NotSupportedException("World road pages require indexed4 source images.");
        }
        int baseX = tim.X / 64 * 64;
        int baseY = tim.Y / 256 * 256;
        TimPalette palette = tim.Palettes[0];
        PixelImage result = new(256, 256);
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                int packed = vram[(baseY + y) * 2048 + baseX * 2 + x / 2];
                int index = packed >> (4 * (x & 1)) & 15;
                Rgba32 color = palette[index];
                // Native VRAM palette converter 0x467370 differs from direct TIM 0x41BC76 for 0x8000 black.
                result[x, y] = color.A == 128 && color.R == 0 && color.G == 0 && color.B == 0 ? new Rgba32(0, 0, 8, 127) : NativeColor(color);
            }
        }
        return result;
    }

    internal static Rgba32 NativeColor(Rgba32 color)
    {
        return color.A == 128 && color.R == 0 && color.G == 0 && color.B == 0
            ? new Rgba32(1, 1, 1)
            : new Rgba32((byte)((color.R >> 3) * 255 / 31), (byte)((color.G >> 3) * 255 / 31), (byte)((color.B >> 3) * 255 / 31), color.A);
    }

    private void Upload(TimImage tim)
    {
        if (tim.X < 0 || tim.Y < 0 || tim.X + tim.WidthInWords > 1024 || tim.Y + tim.Height > 512)
        {
            throw new InvalidDataException("World source TIM upload lies outside VRAM.");
        }
        for (int y = 0; y < tim.Height; y++)
        {
            Span<byte> row = vram.AsSpan((tim.Y + y) * 2048 + tim.X * 2, tim.WidthInWords * 2);
            if (tim.PixelMode == TimPixelMode.Indexed4)
            {
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = (byte)(tim.Pixels[y * tim.PixelWidth + x * 2] | tim.Pixels[y * tim.PixelWidth + x * 2 + 1] << 4);
                }
            }
            else
            {
                tim.Pixels.AsSpan(y * row.Length, row.Length).CopyTo(row);
            }
        }
    }

    private static int Read(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}
