using System.Globalization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

/// <summary>Native image addresses for the Remastered artwork consumers that Memoria exposes.</summary>
public static class RemasteredArtworkPaths
{
    public const string Layout = "demaster-remastered-artwork";

    public static string? MenuAtlasStem(TextureKey key, string? language)
    {
        if (key.Subresource != "atlas" || !key.Owner.Path.StartsWith("menu/", StringComparison.Ordinal))
        {
            return null;
        }
        if (key.Owner.Path == "menu/icon.tex")
        {
            return $"textures/icon.tex/{GameLanguage(language)}/{key.Palette}";
        }
        if (key.Palette != 0)
        {
            return null;
        }
        string name = key.Owner.Path[5..];
        if (NativeResourceNames.RemasteredPortraitTextureStem(key.Owner.Path) is { } portrait)
        {
            return portrait;
        }
        if (name.Length != 9 || !name.StartsWith("mag", StringComparison.Ordinal) || !name.EndsWith(".tex", StringComparison.Ordinal) || !int.TryParse(name.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int atlas))
        {
            return null;
        }
        return atlas switch
        {
            >= 0 and <= 7 => "textures/" + name,
            >= 10 and <= 14 or 17 => $"textures/{name}_{GameLanguage(language)}",
            _ => null
        };
    }

    public static string SplitIconStem(string? language, int palette, int page)
    {
        return $"textures/hires/iconfl{page:D2}.tex/{GameLanguage(language)}/{palette}";
    }

    private static string GameLanguage(string? language) => language switch
    {
        null => "en",
        "ja" => "jp",
        _ => language
    };
}
