using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private static bool IsSupportedFieldBackgroundTarget(string target)
    {
        return Regex.IsMatch(target, @"^(?:eng/)?field/mapdata/(?<field>[a-z0-9_]+)/\k<field>$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string? InspectFieldBackgroundTarget(IResourceSource source, string target, Dictionary<ResourceKey, string?> cache)
    {
        if (!IsSupportedFieldBackgroundTarget(target))
        {
            return null;
        }
        string field = target.Replace('\\', '/').Split('/')[^1].ToLowerInvariant();
        ResourceKey key = new($"field/{field}/{field}.map");
        if (cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }
        string? reason = null;
        try
        {
            ResourcePrefix? prefix = (source as IResourcePrefixSource)?.ReadPrefix(key, int.MaxValue);
            byte[] bytes;
            if (prefix is not null)
            {
                if (prefix.Length != prefix.Bytes.LongLength)
                {
                    throw new InvalidDataException("Field MAP inspection requires the complete declared resource.");
                }
                bytes = prefix.Bytes;
            }
            else
            {
                ResourceContent? resource = source.Find(key);
                if (resource is null)
                {
                    cache.Add(key, null);
                    return null;
                }
                using Stream input = resource.OpenRead();
                using MemoryStream output = new();
                input.CopyTo(output);
                bytes = output.ToArray();
            }
            MapFile map = MapFile.Read(bytes);
            if (!map.HasTerminator || map.Tiles.Count == 0)
            {
                reason = "Field background atlas requires a complete native MAP tile directory.";
            }
            else if (!FieldBackgroundTileDecoder.SupportsAtlas(map.Tiles))
            {
                reason = "Field background atlas uses unsupported source UV alignment, depth or page bounds; its replacement remains unused and original tiles remain active.";
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException)
        {
            reason = "Field background atlas source cannot be inspected: " + exception.Message;
        }
        cache.Add(key, reason);
        return reason;
    }

    private TextureReplacement? ResolveFieldBackground(TextureBuildContext context)
    {
        if (context.Layout != "classic-field-tile" || context.NativeSheet is not { } atlas || atlas.Subresource is not ("background-atlas" or "background-atlas-unsupported-source") || atlas.Owner != context.Key.Owner)
        {
            return null;
        }
        string[] owner = atlas.Owner.Path.Split('/');
        if (owner.Length != 3 || owner[0] != "field" || owner[2] != owner[1] + ".mim")
        {
            return null;
        }
        string name = $"field/mapdata/{owner[1]}/{owner[1]}";
        string[] roots = context.Source.Language is null or "en" ? ["mods/textures/eng/", "mods/textures/"] : ["mods/textures/"];
        ModFile? file = new[] { ".dds", ".png" }.SelectMany(extension => roots.SelectMany(root => catalog.FindAll(ModFormat.Junction, root + name + extension))).FirstOrDefault();
        if (file is null)
        {
            return null;
        }
        if (atlas.Subresource == "background-atlas-unsupported-source")
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new NotSupportedException("Field background atlas replacement requires aligned 16-pixel source tiles within the native 4-bit, 8-bit or 16-bit page. This MAP uses unsupported source UV alignment, depth or page bounds."));
        }
        PixelImage sheet = ModImageDecoder.Decode(file).Image;
        if (atlas.CanvasWidth <= 0 || atlas.CanvasWidth % 16 != 0 || atlas.CanvasHeight != 256 || atlas.Width != 16 || atlas.Height != 16 || context.LogicalWidth != 16 || context.LogicalHeight != 16 || atlas.X < 0 || atlas.Y < 0 || atlas.X % 16 != 0 || atlas.Y % 16 != 0 || (long)atlas.X + 16 > atlas.CanvasWidth || atlas.Y + 16 > 256 || sheet.Width < atlas.CanvasWidth || sheet.Height < atlas.CanvasHeight || sheet.Width % atlas.CanvasWidth != 0 || sheet.Height % atlas.CanvasHeight != 0 || sheet.Width / atlas.CanvasWidth != sheet.Height / atlas.CanvasHeight)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Field background replacement must preserve the native MAP-record atlas and uniform integer tile scale."));
        }
        int scale = sheet.Width / atlas.CanvasWidth;
        PixelImage image = new(16 * scale, 16 * scale);
        for (int row = 0; row < image.Height; row++)
        {
            for (int column = 0; column < image.Width; column++)
            {
                Rgba32 pixel = sheet[atlas.X * scale + column, atlas.Y * scale + row];
                // Packed external backgrounds use a coverage threshold, then the MAP primitive's blend mode.
                // Publish the engine's canonical semitransparency marker instead of authored opacity.
                image[column, row] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A >= 127 ? (byte)128 : (byte)0);
            }
        }
        return new(image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", 16, 16);
    }
}
