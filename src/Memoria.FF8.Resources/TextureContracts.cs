using Albeoris.Games.FF8.Imaging;
using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

public readonly record struct TextureKey(ResourceKey Owner, string Subresource, int Palette = 0);

/// <summary>Original source metadata; compatibility adapters interpret layouts, never renderers.</summary>
public sealed record TextureSourceRegion(int X = 0, int Y = 0, int BitsPerPixel = 0, int CanvasWidth = 256, int CanvasHeight = 256, int? PaletteX = null, int? PaletteY = null);

/// <summary>An equivalent region of another native source, before internal texture enlargement.</summary>
public sealed record TextureSourceAddress(ResourceKey Owner, string Subresource, int Palette, int X, int Y, int Width, int Height, int CanvasWidth, int CanvasHeight);

public sealed record TexturePageMember(string Kind, string Name, int Texture, int X, int Y, int WidthWords, int Height, int BitsPerPixel, int Frame);
public sealed record TextureSourcePageAddress(int Page, int X, int Y, int Width, int Height, int PaletteX, int PaletteY, IReadOnlyList<TexturePageMember> Members);

public sealed class TextureBuildContext
{
    private readonly Lazy<PixelImage> original;

    public TextureBuildContext(TextureKey key, ResourceSourceDescriptor source, long revision, string layout, int logicalWidth, int logicalHeight, Func<PixelImage> original, TextureSourceRegion? region = null, Func<int, PixelImage>? paletteImage = null, int paletteCount = 1, ResourceContent? encodedSource = null)
    {
        Key = key;
        Source = source;
        Revision = revision;
        Layout = layout;
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        this.original = new Lazy<PixelImage>(original);
        Region = region ?? new TextureSourceRegion(CanvasWidth: logicalWidth, CanvasHeight: logicalHeight);
        PaletteImage = paletteImage;
        PaletteCount = paletteCount;
        EncodedSource = encodedSource;
    }

    public TextureKey Key { get; }
    public ResourceSourceDescriptor Source { get; }
    public long Revision { get; }
    public string Layout { get; }
    public int LogicalWidth { get; }
    public int LogicalHeight { get; }
    public TextureSourceRegion Region { get; }
    public PixelImage Original => original.Value;
    public Func<int, PixelImage>? PaletteImage { get; }
    public int PaletteCount { get; }
    public ResourceContent? EncodedSource { get; }
    public TextureSourceAddress? NativeAlias { get; init; }
    public TextureSourceAddress? NativeSheet { get; init; }
    public TextureSourcePageAddress? NativePage { get; init; }
}

/// <summary>Image and logical canvas are published as a single value. Pixels are already colored.</summary>
public sealed record TextureAnimation(IReadOnlyList<PixelImage> Frames, int FrameDurationMilliseconds)
{
    public PixelImage At(TimeSpan elapsed)
    {
        if (Frames.Count == 0 || FrameDurationMilliseconds <= 0 || elapsed < TimeSpan.Zero)
        {
            throw new InvalidDataException("Texture animation requires frames, a positive duration and a nonnegative clock.");
        }
        return Frames[(int)(elapsed.Ticks / (TimeSpan.TicksPerMillisecond * FrameDurationMilliseconds) % Frames.Count)];
    }
}

public sealed record TextureReplacement(PixelImage Image, string Provenance, int LogicalWidth, int LogicalHeight, IReadOnlyList<PixelImage>? MipLevels = null, IReadOnlyDictionary<string, PixelImage>? Variants = null)
{
    public TextureAnimation? Animation { get; init; }
}

public sealed record TextureValue(PixelImage Image, int LogicalWidth, int LogicalHeight, string Provenance, IReadOnlyList<PixelImage>? MipLevels = null, IReadOnlyDictionary<string, PixelImage>? Variants = null)
{
    public bool IsReplacement { get; init; }
    public TextureAnimation? Animation { get; init; }
}

public interface ITextureReplacementResolver
{
    TextureReplacement? Resolve(TextureBuildContext context);
}

/// <summary>A native preparation step applied to already resolved source textures.</summary>
public sealed record TextureTransformContext(TextureKey Key, ImmutableArray<TextureKey> Dependencies, Func<IReadOnlyList<TextureValue>, TextureValue> Build)
{
    public long Revision { get; init; }
}

public sealed class AssetHandle<T> where T : class
{
    private Func<T> read;
    private Func<long> generation;

    internal AssetHandle(Func<T> read, Func<long> generation)
    {
        this.read = read;
        this.generation = generation;
    }

    public T Value => Volatile.Read(ref read)();
    public long Generation => Volatile.Read(ref generation)();

    internal void Freeze(T value)
    {
        long retiredGeneration = Generation;
        Volatile.Write(ref read, () => value);
        Volatile.Write(ref generation, () => retiredGeneration);
    }
}

public sealed class OrderedTextureResolver(params ITextureReplacementResolver[] resolvers) : ITextureReplacementResolver
{
    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        foreach (ITextureReplacementResolver resolver in resolvers)
        {
            if (resolver.Resolve(context) is { } replacement)
            {
                return replacement;
            }
        }
        return null;
    }
}
