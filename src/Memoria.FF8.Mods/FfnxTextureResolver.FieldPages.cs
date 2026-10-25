using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private TextureReplacement? ResolveFieldPage(TextureBuildContext context)
    {
        if (context.NativePage is not { } page || page.Members.Count == 0)
        {
            return null;
        }
        TexturePageMember first = page.Members[0];
        StringBuilder name = new($"field/model/{first.Kind}_chr/");
        foreach (TexturePageMember member in page.Members.Take(7))
        {
            name.Append(CultureInfo.InvariantCulture, $"{member.Name}-{member.Texture}_");
            if (member.Frame >= 0)
            {
                name.Append(CultureInfo.InvariantCulture, $"f{member.Frame}_");
            }
        }
        int pageId = page.Page;
        if (page.Members.Count == 1)
        {
            pageId -= first.X / 64 + (first.Y >= 256 ? 16 : 0);
        }
        name.Append(CultureInfo.InvariantCulture, $"{pageId}_{page.PaletteX}_{page.PaletteY}");
        string[] roots = context.Source.Language is null or "en" ? ["mods/textures/eng/", "mods/textures/"] : ["mods/textures/"];
        if (page.Members.Any(member => member.Frame < -1))
        {
            foreach (ModFile candidate in catalog.Files.Where(candidate => candidate.Format == ModFormat.Junction))
            {
                string extension = Path.GetExtension(candidate.RelativePath);
                if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string unanimated = Regex.Replace(candidate.RelativePath[..^extension.Length], @"_f[0-9]+_", "_", RegexOptions.IgnoreCase);
                if (roots.Any(root => unanimated.Equals(root + name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ModTextureLoadException(candidate.PackageId, candidate.RelativePath, new NotSupportedException("Field page replacement requires retained first-seen texture-copy history from the native resource registration."));
                }
            }
            return null;
        }
        ModFile? file = new[] { ".dds", ".png" }.SelectMany(extension => roots.SelectMany(root => catalog.FindAll(ModFormat.Junction, root + name + extension))).FirstOrDefault();
        if (file is null)
        {
            return null;
        }
        DecodedModImage decoded = ModImageDecoder.Decode(file);
        PixelImage sheet = decoded.Image;
        if (page.X < 0 || page.Y < 0 || page.Width <= 0 || page.Height <= 0 || page.X + page.Width > 256 || page.Y + page.Height > 256 || sheet.Width != sheet.Height || sheet.Width % 256 != 0)
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, new InvalidDataException("Field VRAM page replacement must preserve its native256 by256 page and registered source crop."));
        }
        int scale = sheet.Width / 256;
        PixelImage image = new(page.Width * scale, page.Height * scale);
        for (int row = 0; row < image.Height; row++)
        {
            sheet.Pixels.AsSpan(((page.Y * scale + row) * sheet.Width) + page.X * scale, image.Width).CopyTo(image.Pixels.AsSpan(row * image.Width));
        }
        return new(image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight);
    }

    private static bool IsSupportedFieldPageTarget(string name)
    {
        Match match = Regex.Match(name, @"^field/model/(main|second)_chr/(?:[a-z0-9]{1,4}-[0-9]+_(?:f[0-9]+_)?){1,7}(?<page>[0-9]+)_(?<x>[0-9]+)_(?<y>[0-9]+)$");
        return match.Success && Number(match.Groups["page"].Value, out int page) && page < 32 && Number(match.Groups["x"].Value, out int x) && x < 1024 && x % 16 == 0 && Number(match.Groups["y"].Value, out int y) && y < 512;
    }

    private static bool IsImpossibleFieldModelTarget(string name)
    {
        string stem = name.ToLowerInvariant();
        if (stem.StartsWith("eng/", StringComparison.Ordinal))
        {
            stem = stem[4..];
        }
        return Regex.IsMatch(stem, @"^field/model/(main|second)_chr/") && !Regex.IsMatch(stem, @"^field/model/(main|second)_chr/[a-z0-9]{1,4}-[0-9]+");
    }
}
