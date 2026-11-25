using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private static readonly Regex remasteredIconPath = new(@"\Atextures/(?:icon\.tex|hires/iconfl0[0-3]\.tex)/(?:en|de|fr|es|it|jp)/(?<palette>[0-9]|1[0-5])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex remasteredMagazinePath = new(@"\Atextures/(?<name>mag[0-9]{2}\.tex)(?:_(?<language>en|de|fr|es|it|jp))?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static TextureKey? DescribeRemasteredArtworkTarget(string stem)
    {
        if (DescribePortraitArtworkTarget(stem) is { } portrait)
        {
            return new(new(portrait), "atlas");
        }
        Match icon = remasteredIconPath.Match(stem);
        if (icon.Success)
        {
            return new(new("menu/icon.tex"), "atlas", int.Parse(icon.Groups["palette"].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        Match magazine = remasteredMagazinePath.Match(stem);
        if (!magazine.Success)
        {
            return null;
        }
        TextureKey key = new(new("menu/" + magazine.Groups["name"].Value), "atlas");
        string? expected = RemasteredArtworkPaths.MenuAtlasStem(key, magazine.Groups["language"].Value.ToLowerInvariant());
        return string.Equals(expected, stem, StringComparison.OrdinalIgnoreCase) ? key : null;
    }

    private static bool IsRemasteredArtworkTarget(string stem, string? language)
    {
        if (DescribeRemasteredArtworkTarget(stem) is not { } key)
        {
            return false;
        }
        if (string.Equals(stem, RemasteredArtworkPaths.MenuAtlasStem(key, language), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return key.Owner.Path == "menu/icon.tex" && Enumerable.Range(0, 4).Any(page => stem.Equals(RemasteredArtworkPaths.SplitIconStem(language, key.Palette, page), StringComparison.OrdinalIgnoreCase));
    }

    private TextureReplacement? ResolveRemasteredIconAtlas(TextureBuildContext context)
    {
        string? language = context.Source.Language;
        string stem = RemasteredArtworkPaths.MenuAtlasStem(context.Key, language)!;
        ModFile? whole = FindArtwork(stem);
        ModFile?[] pages = Enumerable.Range(0, 4).Select(page => FindArtwork(RemasteredArtworkPaths.SplitIconStem(language, context.Key.Palette, page))).ToArray();
        if (pages.All(file => file is null))
        {
            return whole is null ? null : ResolveWholeArtwork(context, whole);
        }
        if (context.LogicalWidth != 256 || context.LogicalHeight != 256)
        {
            ModFile first = pages.OfType<ModFile>().First();
            throw new ModTextureLoadException(first.PackageId, first.RelativePath, new InvalidDataException("Split menu icons require a 256 by 256 logical atlas."));
        }

        PixelImage?[] images = pages.Select(file => file is null ? null : DecodeArtwork(file, 128, 128).Image).ToArray();
        PixelImage? fallback = null;
        if (images.Any(image => image is null))
        {
            fallback = whole is not null ? DecodeArtwork(whole, 256, 256).Image : context.ReadInstalledOriginal?.Invoke().Image ?? context.Original;
        }
        int size = Math.Max(images.OfType<PixelImage>().Max(image => image.Width), (fallback?.Width ?? 0) / 2);
        PixelImage atlas = new(size * 2, size * 2);
        for (int page = 0; page < images.Length; page++)
        {
            PixelImage source = images[page] ?? fallback!;
            int sourceSize = images[page] is null ? source.Width / 2 : source.Width;
            int quadrantX = page % 2;
            int quadrantY = page / 2;
            int sourceX = images[page] is null ? quadrantX * sourceSize : 0;
            int sourceY = images[page] is null ? quadrantY * sourceSize : 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    atlas[quadrantX * size + x, quadrantY * size + y] = source[sourceX + x * sourceSize / size, sourceY + y * sourceSize / size];
                }
            }
        }
        IEnumerable<ModFile> used = pages.OfType<ModFile>();
        if (fallback is not null && whole is not null)
        {
            used = used.Append(whole);
        }
        return new(atlas, string.Join(';', used.Select(ArtworkProvenance)), 256, 256);
    }

    private ModFile? FindArtwork(string stem) => WithExtensions(stem).SelectMany(path => catalog.FindAll(ModFormat.Demaster, path)).FirstOrDefault();

    private static TextureReplacement ResolveWholeArtwork(TextureBuildContext context, ModFile file)
    {
        DecodedModImage image = DecodeArtwork(file, context.LogicalWidth, context.LogicalHeight);
        return new(image.Image, ArtworkProvenance(file), context.LogicalWidth, context.LogicalHeight, image.MipLevels);
    }

    private static DecodedModImage DecodeArtwork(ModFile file, int width, int height)
    {
        ValidateExplicitTextureLayout(file);
        DecodedModImage image = ModImageDecoder.Decode(file);
        int scale = image.Image.Width / width;
        if (scale < 1 || image.Image.Width != width * scale || image.Image.Height != height * scale)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException($"Artwork must preserve its {width} by {height} logical canvas at an integer scale."));
        }
        return image;
    }

    private static string ArtworkProvenance(ModFile file) => $"{file.PackageId}/{file.RelativePath}@{file.Revision}";
}
