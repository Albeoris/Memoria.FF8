using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private const int FileQueryCapacity = 1024;
    private const int RegionCacheCapacity = 256;
    private const long RegionCacheByteBudget = 16L * 1024 * 1024;
    private static readonly string[] EnglishTextureRoots = ["mods/textures/eng/", "mods/textures/"];
    private static readonly string[] DefaultTextureRoots = ["mods/textures/"];
    private static readonly string[] TextureExtensions = [".dds", ".png"];
    private readonly object textureCacheSync = new();
    private readonly Dictionary<RegionFileQuery, ModFile?> regionFiles = [];
    private readonly Dictionary<(string Name, bool English), ModFile?> fallbackFiles = [];
    private readonly Dictionary<VramFileQuery, ModFile?> vramFiles = [];
    private readonly Dictionary<RegionImageKey, LinkedListNode<RegionImage>> regionImages = [];
    private readonly LinkedList<RegionImage> recentRegionImages = [];
    private long regionImageBytes;

    private readonly record struct RegionFileQuery(string Name, int Palette, bool English, bool Fallback);
    private readonly record struct VramFileQuery(string Name, int Page, int PaletteX, int PaletteY, bool English);
    private readonly record struct RegionImageKey(ModFile File, long Revision, int X, int Y, int Width, int Height, int CanvasWidth, int CanvasHeight, int HorizontalPageWidth, int VerticalPageHeight, int LogicalWidth, int LogicalHeight, string? Language, bool NormalizedMaterial, bool NormalizeEffectAlpha, bool EffectPage);
    private sealed record RegionImage(RegionImageKey Key, TextureReplacement Replacement, long Bytes);

    private static string[] LanguageRoots(string? language) => language is null or "en" ? EnglishTextureRoots : DefaultTextureRoots;

    private ModFile? FindExternalFile(string[] roots, string stem)
    {
        foreach (string extension in TextureExtensions)
        {
            foreach (string root in roots)
            {
                IReadOnlyList<ModFile> files = catalog.FindAll(ModFormat.Junction, $"{root}{stem}{extension}");
                if (files.Count != 0)
                {
                    return files[0];
                }
            }
        }
        return null;
    }

    private ModFile? FindRegionFile(string[] roots, string name, int palette, bool paletteFallback)
    {
        RegionFileQuery key = new(name, palette, roots.Length == 2, paletteFallback);
        lock (textureCacheSync)
        {
            if (regionFiles.TryGetValue(key, out ModFile? cached))
            {
                return cached;
            }
        }
        ModFile? file = FindExternalFile(roots, $"{name}_{palette:00}");
        if (file is null && paletteFallback)
        {
            file = FindFallbackFile(roots, name);
        }
        lock (textureCacheSync)
        {
            if (regionFiles.Count >= FileQueryCapacity)
            {
                regionFiles.Clear();
            }
            regionFiles[key] = file;
        }
        return file;
    }

    private ModFile? FindFallbackFile(string[] roots, string name)
    {
        var key = (name, roots.Length == 2);
        lock (textureCacheSync)
        {
            if (fallbackFiles.TryGetValue(key, out ModFile? cached))
            {
                return cached;
            }
        }
        ModFile? selected = null;
        int selectedPalette = int.MaxValue;
        int selectedFormat = int.MaxValue;
        foreach (ModFile file in catalog.Files)
        {
            if (file.Format != ModFormat.Junction)
            {
                continue;
            }
            int candidatePalette = ExternalPalette(file.RelativePath, roots, name);
            int candidateFormat = file.RelativePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            if (candidatePalette >= 0 && (candidatePalette < selectedPalette || candidatePalette == selectedPalette && candidateFormat < selectedFormat))
            {
                selected = file;
                selectedPalette = candidatePalette;
                selectedFormat = candidateFormat;
            }
        }
        lock (textureCacheSync)
        {
            if (fallbackFiles.Count >= FileQueryCapacity)
            {
                fallbackFiles.Clear();
            }
            fallbackFiles[key] = selected;
        }
        return selected;
    }

    private TextureReplacement ReadSelectedRegion(TextureBuildContext context, ModFile file, int x, int y, int width, int height, int canvasWidth, int canvasHeight, int horizontalPageWidth, int verticalPageHeight, bool normalizedMaterial, bool normalizeEffectAlpha, bool effectPage = false)
    {
        RegionImageKey key = new(file, file.Revision, x, y, width, height, canvasWidth, canvasHeight, horizontalPageWidth, verticalPageHeight, context.LogicalWidth, context.LogicalHeight, context.Source.Language, normalizedMaterial, normalizeEffectAlpha, effectPage);
        lock (textureCacheSync)
        {
            if (regionImages.TryGetValue(key, out LinkedListNode<RegionImage>? cached))
            {
                recentRegionImages.Remove(cached);
                recentRegionImages.AddFirst(cached);
                return cached.Value.Replacement;
            }
        }
        TextureReplacement replacement = BuildSelectedRegion(context, file, x, y, width, height, canvasWidth, canvasHeight, horizontalPageWidth, verticalPageHeight, normalizedMaterial, effectPage);
        if (normalizeEffectAlpha)
        {
            replacement = NormalizeEffectAlpha(replacement);
        }
        long bytes = replacement.Image.Pixels.LongLength * 4;
        if (replacement.MipLevels is { } levels)
        {
            foreach (PixelImage level in levels)
            {
                if (!ReferenceEquals(level, replacement.Image))
                {
                    bytes += level.Pixels.LongLength * 4;
                }
            }
        }
        if (bytes > RegionCacheByteBudget)
        {
            return replacement;
        }
        lock (textureCacheSync)
        {
            if (regionImages.TryGetValue(key, out LinkedListNode<RegionImage>? existing))
            {
                recentRegionImages.Remove(existing);
                recentRegionImages.AddFirst(existing);
                return existing.Value.Replacement;
            }
            while (recentRegionImages.Last is { } oldest && (regionImages.Count >= RegionCacheCapacity || regionImageBytes > RegionCacheByteBudget - bytes))
            {
                regionImageBytes -= oldest.Value.Bytes;
                regionImages.Remove(oldest.Value.Key);
                recentRegionImages.RemoveLast();
            }
            LinkedListNode<RegionImage> added = recentRegionImages.AddFirst(new RegionImage(key, replacement, bytes));
            regionImages.Add(key, added);
            regionImageBytes += bytes;
        }
        return replacement;
    }
}
