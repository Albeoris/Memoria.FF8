using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Mods;

public sealed class TonberryRecognitionCache
{
    private readonly Dictionary<(string Source, TextureKey Key, long Revision, string Layout, TextureSourceRegion Region), TonberrySignatures> entries = [];
    private readonly object sync = new();
    private readonly Dictionary<(string Source, ResourceKey Owner, long Revision), TonberryWorldTextureSource> worlds = [];
    public long CalculationCount { get; private set; }

    internal TonberryWorldTextureSource GetWorld(TextureBuildContext context)
    {
        lock (sync)
        {
            ResourceContent content = context.EncodedSource ?? throw new InvalidOperationException("World page projection requires the original encoded source.");
            var key = (context.Source.Id, context.Key.Owner, content.Revision);
            if (!worlds.TryGetValue(key, out TonberryWorldTextureSource? world))
            {
                world = new TonberryWorldTextureSource(content);
                if (worlds.Count >= 8)
                {
                    worlds.Clear();
                }
                worlds.Add(key, world);
            }
            return world;
        }
    }

    internal TonberrySignatures Get(TextureBuildContext context, Func<PixelImage> projection)
    {
        lock (sync)
        {
            var key = (context.Source.Id, context.Key, context.Revision, context.Layout, context.Region);
            if (!entries.TryGetValue(key, out TonberrySignatures? signatures))
            {
                signatures = TonberrySignatures.Calculate(projection());
                if (entries.Count >= 1024)
                {
                    entries.Clear();
                }
                entries.Add(key, signatures);
                CalculationCount++;
            }
            return signatures;
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            entries.Clear();
            worlds.Clear();
        }
    }
}

public sealed class TonberryTextureResolver : ITextureReplacementResolver
{
    private readonly TonberryTables tables;
    private readonly TonberryRecognitionCache recognition;
    private readonly Dictionary<ModFile, PixelImage> images = [];
    public TonberryRecognitionCache RecognitionCache => recognition;
    public IReadOnlySet<string> UsablePackageIds => tables.UsablePackageIds;

    private TonberryTextureResolver(TonberryTables tables, TonberryRecognitionCache recognition)
    {
        this.tables = tables;
        this.recognition = recognition;
    }

    public static TonberryTextureResolver? Create(ResourceSourceDescriptor source, ModCatalogSnapshot catalog, out IReadOnlyList<ModDiagnostic> diagnostics, TonberryRecognitionCache? cache = null)
    {
        diagnostics = Array.Empty<ModDiagnostic>();
        if (source.Edition != GameDataEdition.Classic || !catalog.Files.Any(file => file.Format == ModFormat.Tonberry))
        {
            cache?.Clear();
            return null;
        }
        TonberryTables tables = new(catalog);
        diagnostics = tables.Diagnostics;
        if (!tables.HasUsableReplacement)
        {
            cache?.Clear();
            return null;
        }
        return new TonberryTextureResolver(tables, cache ?? new TonberryRecognitionCache());
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        if (context.Source.Edition != GameDataEdition.Classic || context.Layout is not ("classic-world-texl" or "tonberry-classic-page" or "classic-world-road-region"))
        {
            return null;
        }
        if (context.Layout == "classic-world-road-region")
        {
            return ResolveRoadRegion(context);
        }
        TonberrySignatures signatures = recognition.Get(context, () => Project(context));
        TonberryRule? rule = tables.Match(signatures);
        if (rule is null)
        {
            return null;
        }
        string name = rule.Name.StartsWith("sysfld", StringComparison.Ordinal) || rule.Name.StartsWith("iconfl", StringComparison.Ordinal) ? SelectInterfaceVariant(rule.Name, context.Original) : rule.Name;
        ModFile? image = tables.FindImage(name);
        if (image is null)
        {
            return null;
        }
        lock (images)
        {
            if (!images.TryGetValue(image, out PixelImage? decoded))
            {
                decoded = ModImageDecoder.Decode(image).Image;
                if (rule.Persistent)
                {
                    images.Add(image, decoded);
                }
            }
            if (context.Layout == "classic-world-texl" && (decoded.Width % context.LogicalWidth != 0 || decoded.Height % context.LogicalHeight != 0 || decoded.Width / context.LogicalWidth != decoded.Height / context.LogicalHeight))
            {
                throw new ModTextureLoadException(image.PackageId, image.RelativePath, new InvalidDataException("The replacement does not preserve the original TEXL canvas."));
            }
            return new TextureReplacement(decoded, $"{image.PackageId}/{image.RelativePath} (rule {rule.Table.PackageId}/{rule.Table.RelativePath}:{rule.Row})", context.LogicalWidth, context.LogicalHeight);
        }
    }

    private static PixelImage Project(TextureBuildContext context)
    {
        PixelImage image = context.Original;
        int width = context.Region.CanvasWidth;
        int height = context.Region.CanvasHeight;
        if (context.Layout == "tonberry-classic-page" && image.Width == width && image.Height == height)
        {
            return image;
        }
        if (image.Width > width || image.Height > height)
        {
            throw new NotSupportedException($"Classic texture {context.Key} exceeds its declared compatibility canvas.");
        }
        PixelImage canvas = new(width, height);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Rgba32 color = image[x, y];
                if (context.Layout == "classic-world-texl")
                {
                    // Native 0x41BC76 -> 0x418DEC expands CLUT channels with integer 255*c/31.
                    // Tim.Decode uses bit replication; recover the original five-bit channels first.
                    color = TonberryWorldTextureSource.NativeColor(color);
                }
                canvas[x, y] = color;
            }
        }
        return canvas;
    }

    private TextureReplacement? ResolveRoadRegion(TextureBuildContext context)
    {
        if (context.EncodedSource is null)
        {
            return null;
        }
        TonberryWorldTextureSource world = recognition.GetWorld(context);
        string[] subresource = context.Key.Subresource.Split('/');
        if (subresource.Length != 2 || subresource[0] != "roads" || !int.TryParse(subresource[1], out int index) || (uint)index >= world.Roads.Count)
        {
            throw new InvalidDataException("A world road region requires its exact source TIM ordinal.");
        }
        TimImage tim = world.Roads[index];
        if (context.Region.X != tim.X || context.Region.Y != tim.Y || context.Region.BitsPerPixel != 4 || context.LogicalWidth != tim.PixelWidth || context.LogicalHeight != tim.Height || context.Region.CanvasWidth != 256 || context.Region.CanvasHeight != 256)
        {
            throw new InvalidDataException("World road region metadata differs from its original source TIM.");
        }
        TonberryRule? rule = tables.Match(recognition.Get(context, () => world.Page(tim)));
        ModFile? file = rule is null ? null : tables.FindImage(rule.Name);
        if (file is null)
        {
            return null;
        }
        PixelImage image = ModImageDecoder.Decode(file).Image;
        if (image.Width % 256 != 0 || image.Height != image.Width)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("The replacement must preserve its 256x256 logical world canvas."));
        }
        int scale = image.Width / 256;
        int sourceX = tim.X % 64 * 4 * scale;
        int sourceY = tim.Y % 256 * scale;
        PixelImage region = new(tim.PixelWidth * scale, tim.Height * scale);
        for (int y = 0; y < region.Height; y++)
        {
            image.Pixels.AsSpan((sourceY + y) * image.Width + sourceX, region.Width).CopyTo(region.Pixels.AsSpan(y * region.Width, region.Width));
        }
        return new TextureReplacement(region, $"{file.PackageId}/{file.RelativePath} (original world road region {index}; rule {rule!.Table.PackageId}/{rule.Table.RelativePath}:{rule.Row})", context.LogicalWidth, context.LogicalHeight);
    }

    private static string SelectInterfaceVariant(string name, PixelImage original)
    {
        if (name is "sysfld00_13" or "sysfld01_13" && original.Width > 177 && original.Height > 155)
        {
            Rgba32 color = original[177, 155];
            int page = ((color.R + color.G + color.B) / 3) switch
            {
                153 => 14,
                150 => 15,
                101 => 16,
                85 => 17,
                174 => 18,
                170 => 19,
                255 => 20,
                _ => 13
            };
            return name[..^2] + page;
        }
        (int X, int Y)? point = name switch
        {
            "iconfl00_13" => (82, 150),
            "iconfl01_13" => (175, 208),
            "iconfl02_13" => (216, 108),
            "iconfl03_13" => (58, 76),
            "iconflmaster_13" => (215, 103),
            _ => null
        };
        if (point is { } sample && sample.X < original.Width && sample.Y < original.Height)
        {
            Rgba32 color = original[sample.X, sample.Y];
            int product = Math.Max(1, (int)color.R) * Math.Max(1, (int)color.G) * Math.Max(1, (int)color.B);
            int page = product switch
            {
                605160 => 14,
                1191016 => 15,
                189 => 16,
                473304 => 17,
                20992 => 18,
                859625 => 19,
                551368 => 20,
                1393200 => 21,
                931500 => 22,
                1011240 => 23,
                1395640 => 24,
                1018024 => 25,
                411864 => 26,
                80064 => 27,
                4410944 => 28,
                _ => 13
            };
            return name[..^2] + page;
        }
        return name;
    }
}
