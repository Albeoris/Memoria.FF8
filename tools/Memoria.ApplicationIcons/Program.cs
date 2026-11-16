using System.Buffers.Binary;
using System.Text;
using Memoria.FF8.TitleScene;
using SkiaSharp;

string destination = Path.GetFullPath(args.Length == 1 ? args[0] : "Artwork/Application");
Directory.CreateDirectory(destination);
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512, 1024];
Dictionary<int, byte[]> images = [];
using SKBitmap crest = TitleArtworkRenderer.CreateSilverCrestImage(2048);
using SKImage source = SKImage.FromBitmap(crest);
foreach (int size in sizes)
{
    using SKBitmap image = new(size, size, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    using SKCanvas canvas = new(image);
    canvas.Clear(SKColors.Transparent);
    float height = size * 0.94f;
    float width = height * crest.Width / crest.Height;
    using SKPaint paint = new() { IsAntialias = true };
    canvas.DrawImage(source, new SKRect((size - width) / 2, (size - height) / 2, (size + width) / 2, (size + height) / 2), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
    using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
    images[size] = encoded.ToArray();
    File.WriteAllBytes(Path.Combine(destination, $"application-{size}.png"), images[size]);
}
File.WriteAllBytes(Path.Combine(destination, "application.png"), images[256]);

// MonoGame Native loads Icon.bmp through its own native windowing library.
// A V4 DIB declares alpha explicitly, preserving the transparent silhouette.
using (SKBitmap image = SKBitmap.Decode(images[256], new SKImageInfo(256, 256, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
using (BinaryWriter writer = new(File.Create(Path.Combine(destination, "application.bmp"))))
{
    const int headerSize = 14 + 108;
    const int pixelBytes = 256 * 256 * 4;
    writer.Write((ushort)0x4D42);
    writer.Write(headerSize + pixelBytes);
    writer.Write(0);
    writer.Write(headerSize);
    writer.Write(108);
    writer.Write(256);
    writer.Write(256);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(3);
    writer.Write(pixelBytes);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0x00FF0000u);
    writer.Write(0x0000FF00u);
    writer.Write(0x000000FFu);
    writer.Write(0xFF000000u);
    writer.Write(0x73524742u);
    writer.Write(new byte[48]);
    for (int y = 255; y >= 0; y--)
    {
        for (int x = 0; x < 256; x++)
        {
            SKColor pixel = image.GetPixel(x, y);
            writer.Write(pixel.Blue);
            writer.Write(pixel.Green);
            writer.Write(pixel.Red);
            writer.Write(pixel.Alpha);
        }
    }
}

int[] iconSizes = sizes.Where(size => size <= 256).ToArray();
using (BinaryWriter writer = new(File.Create(Path.Combine(destination, "application.ico"))))
{
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)iconSizes.Length);
    int offset = 6 + 16 * iconSizes.Length;
    foreach (int size in iconSizes)
    {
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((byte)(size == 256 ? 0 : size));
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(images[size].Length);
        writer.Write(offset);
        offset += images[size].Length;
    }
    foreach (int size in iconSizes)
    {
        writer.Write(images[size]);
    }
}

(string Type, int Size)[] macImages = [("icp4", 16), ("icp5", 32), ("icp6", 64), ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024), ("ic11", 32), ("ic12", 64), ("ic13", 256), ("ic14", 512)];
using (Stream stream = File.Create(Path.Combine(destination, "application.icns")))
{
    stream.Write(Encoding.ASCII.GetBytes("icns"));
    WriteBigEndian(stream, 8 + macImages.Sum(entry => 8 + images[entry.Size].Length));
    foreach ((string type, int size) in macImages)
    {
        stream.Write(Encoding.ASCII.GetBytes(type));
        WriteBigEndian(stream, 8 + images[size].Length);
        stream.Write(images[size]);
    }
}
Console.WriteLine($"Generated {sizes.Length} PNG sizes, PNG alias, BMP, ICO and ICNS in {destination}.");

static void WriteBigEndian(Stream stream, int value)
{
    Span<byte> bytes = stackalloc byte[4];
    BinaryPrimitives.WriteInt32BigEndian(bytes, value);
    stream.Write(bytes);
}
