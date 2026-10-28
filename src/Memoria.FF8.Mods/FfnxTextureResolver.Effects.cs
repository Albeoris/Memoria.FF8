using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private const string EffectFilePattern = @"(?:a8def[0-2]\.tim|ma8def_[0-9]+\.tim|mag[0-9]{3}(?:-[0-9]{3})*(?:_[0-9]+)?\.tim|mag[0-9]{3}_[a-z]\.(?:dat|[0-9]+(?:t[0-9]+)?(?:-[0-9]+)?))";
    private const string EffectStemPattern = EffectFilePattern + @"(?:-[0-9]+)?";

    private TextureReplacement? ResolveEffect(TextureBuildContext context)
    {
        if (context.NativeAlias is not { } address || address.Palette is < 0 or > byte.MaxValue || !TryEffectStem(address, out string name))
        {
            return null;
        }
        TextureReplacement? replacement = ReadEffectPage(context, address.Owner.Path[..address.Owner.Path.IndexOf('/')]);
        replacement ??= ReadRegion(context, name, address.Palette, address.X, address.Y, address.Width, address.Height, address.CanvasWidth, address.CanvasHeight, normalizeEffectAlpha: true);
        return replacement;
    }

    private static bool TryEffectStem(TextureSourceAddress address, out string name)
    {
        name = address.Owner.Path;
        int slash = name.IndexOf('/');
        if (slash < 0 || name[..slash] is not ("magic" or "battle") || !IsEffectFile(name[(slash + 1)..]))
        {
            return false;
        }
        if (address.Subresource == "image")
        {
            return true;
        }
        string[] parts = address.Subresource.Split('/');
        if (parts.Length != 2 || parts[0] != "texture" || !Number(parts[1], out int ordinal))
        {
            return false;
        }
        name += "-" + ordinal.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private TextureReplacement? ReadEffectPage(TextureBuildContext context, string domain)
    {
        if (context.NativePage is not { } page || page.Members.Count == 0 || page.Members.Any(member => member.Kind != "effect" || !IsEffectFile(member.Name) || member.Texture < -1 || member.Frame < -1 || member.X is < 0 or >= 1024 || member.Y is < 0 or >= 512))
        {
            return null;
        }
        domain = page.Namespace ?? domain;
        if (domain is not ("magic" or "battle"))
        {
            return null;
        }
        TexturePageMember[] members = page.Members.OrderBy(member => member.Y).ThenBy(member => member.X).ToArray();
        StringBuilder prefix = new(domain + "/");
        foreach (TexturePageMember member in members.Take(7))
        {
            prefix.Append(member.Name);
            if (member.Texture >= 0)
            {
                prefix.Append(CultureInfo.InvariantCulture, $"-{member.Texture}");
            }
            prefix.Append('_');
            if (member.Frame >= 0)
            {
                prefix.Append(CultureInfo.InvariantCulture, $"f{member.Frame}_");
            }
        }
        string joined = prefix.ToString();
        if (joined.Length > 240)
        {
            joined = joined[..240];
            if (!joined.EndsWith('_'))
            {
                joined += "_";
            }
        }
        int pageId = page.Page;
        if (members.Length == 1)
        {
            pageId -= members[0].X / 64 + (members[0].Y >= 256 ? 16 : 0);
        }
        if (pageId is < 0 or >= 32 || page.PaletteX is < 0 or >= 1024 || page.PaletteY is < 0 or >= 512)
        {
            return null;
        }
        string[] roots = LanguageRoots(context.Source.Language);
        string pageStem = joined[..^1];
        ModFile? file = FindVramPage(roots, pageStem, pageId, page.PaletteX, page.PaletteY) ?? FindVramPage(roots, pageStem, pageId, 0, 0);
        if (file is null)
        {
            return null;
        }
        return ReadSelectedRegion(context, file, page.X, page.Y, page.Width, page.Height, 256, 256, 0, 0, false, true, effectPage: true);
    }

    private static TextureReplacement NormalizeEffectAlpha(TextureReplacement replacement)
    {
        PixelImage image = Normalize(replacement.Image);
        IReadOnlyList<PixelImage>? mips = replacement.MipLevels is null ? null : Array.AsReadOnly(replacement.MipLevels.Select(level => ReferenceEquals(level, replacement.Image) ? image : Normalize(level)).ToArray());
        return replacement with { Image = image, MipLevels = mips };

        static PixelImage Normalize(PixelImage source)
        {
            Rgba32[] pixels = new Rgba32[source.Pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Rgba32 color = source.Pixels[i];
                pixels[i] = color with { A = color.A >= 127 ? (byte)127 : (byte)0 };
            }
            return new(source.Width, source.Height, pixels);
        }
    }

    private static bool IsSupportedEffectTarget(string name)
    {
        if (name.Length > 512)
        {
            return false;
        }
        Match direct = Regex.Match(name, "^(?:magic|battle)/" + EffectStemPattern + @"_(?<palette>[0-9]+)$", RegexOptions.CultureInvariant);
        if (direct.Success && EffectTokensBounded(direct.Value) && PaletteNumber(direct.Groups["palette"].Value, out _))
        {
            return true;
        }
        Match page = Regex.Match(name, "^(?:magic|battle)/(?:" + EffectStemPattern + @"_(?:f[0-9]+_)?){1,7}(?<page>[0-9]+)_(?<x>[0-9]+)_(?<y>[0-9]+)$", RegexOptions.CultureInvariant);
        return page.Success && EffectTokensBounded(page.Value) && EffectNumber(page.Groups["page"].Value, 31) && EffectNumber(page.Groups["x"].Value, 1023) && EffectNumber(page.Groups["y"].Value, 511);
    }

    private static bool IsEffectFile(string name) => name.Length <= 240 && Regex.IsMatch(name, "^" + EffectFilePattern + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && EffectTokensBounded(name);

    private static bool EffectTokensBounded(string name) => Regex.Matches(name, "[0-9]+", RegexOptions.CultureInvariant).All(match => Number(match.Value, out _));

    private static bool EffectNumber(string text, int maximum) => Number(text, out int value) && value <= maximum;
}
