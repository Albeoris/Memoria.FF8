namespace Albeoris.Games.FF8.Imaging;

/// <summary>A small, fully managed RGBA image.</summary>
public sealed class PixelImage
{
    public const Int32 MaximumDimension = 16 * 1024;
    public const Int32 MaximumPixelCount = 64 * 1024 * 1024;

    public PixelImage(Int32 width, Int32 height)
        : this(width, height, new Rgba32[GetPixelCount(width, height)])
    {
    }

    public PixelImage(Int32 width, Int32 height, Rgba32[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        Int32 pixelCount = GetPixelCount(width, height);
        if (pixels.Length != pixelCount)
        {
            throw new ArgumentException(
                $"A {width}x{height} image requires {pixelCount} pixels, but {pixels.Length} were supplied.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public Int32 Width { get; }
    public Int32 Height { get; }
    public Rgba32[] Pixels { get; }

    public Rgba32 this[Int32 x, Int32 y]
    {
        get => Pixels[GetIndex(x, y)];
        set => Pixels[GetIndex(x, y)] = value;
    }

    private Int32 GetIndex(Int32 x, Int32 y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return y * Width + x;
    }

    private static Int32 GetPixelCount(Int32 width, Int32 height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaximumDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaximumDimension);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((Int64)width * height, MaximumPixelCount);
        return checked(width * height);
    }
}
