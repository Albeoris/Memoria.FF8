using System.Runtime.InteropServices;
using SkiaSharp;

namespace Memoria.FF8;

internal static class PortableRaster
{
    internal static SKBitmap FromRgba(byte[] rgba, int width, int height)
    {
        if (rgba.Length != checked(width * height * 4))
        {
            throw new ArgumentException("RGBA buffer dimensions do not match.", nameof(rgba));
        }
        SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        for (int y = 0; y < height; y++)
        {
            Marshal.Copy(rgba, y * width * 4, bitmap.GetPixels() + y * bitmap.RowBytes, width * 4);
        }
        return bitmap;
    }

    internal static byte[] Rgba(SKBitmap bitmap)
    {
        byte[] bytes = new byte[checked(bitmap.Width * bitmap.Height * 4)];
        SKColor[] pixels = bitmap.Pixels;
        for (int index = 0; index < pixels.Length; index++)
        {
            bytes[index * 4] = pixels[index].Red;
            bytes[index * 4 + 1] = pixels[index].Green;
            bytes[index * 4 + 2] = pixels[index].Blue;
            bytes[index * 4 + 3] = pixels[index].Alpha;
        }
        return bytes;
    }

    internal static void SavePng(this SKBitmap bitmap, string path)
    {
        using Stream output = File.Create(path);
        SavePng(bitmap, output);
    }

    internal static void SavePng(this SKBitmap bitmap, Stream output)
    {
        if (!bitmap.Encode(output, SKEncodedImageFormat.Png, 100))
        {
            throw new IOException("Could not encode the PNG preview.");
        }
    }
}
