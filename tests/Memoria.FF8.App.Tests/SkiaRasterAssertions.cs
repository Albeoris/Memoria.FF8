using SkiaSharp;

namespace Memoria.FF8.App.Tests;

// Keep existing raster assertions in their authored ARGB channel convention.
internal static class SkiaRasterAssertions
{
    extension(SKColor color)
    {
        internal byte R => color.Red;
        internal byte G => color.Green;
        internal byte B => color.Blue;
        internal byte A => color.Alpha;
        internal int ToArgb() => unchecked((int)(uint)color);
        internal static SKColor FromArgb(int red, int green, int blue) => new((byte)red, (byte)green, (byte)blue);
        internal static SKColor FromArgb(int alpha, int red, int green, int blue) => new((byte)red, (byte)green, (byte)blue, (byte)alpha);
    }

    extension(SKBitmap bitmap)
    {
        internal void Save(string path) => PortableRaster.SavePng(bitmap, path);
    }
}
