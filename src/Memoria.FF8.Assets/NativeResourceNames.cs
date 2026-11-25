using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Canonical native archive and artwork addresses.</summary>
public static class NativeResourceNames
{
    public static string? RemasteredPortraitTextureStem(string owner)
    {
        return owner is "menu/face_b00.tex" or "menu/face_b01.tex" or "menu/gf_big00.tex" or "menu/gf_big01.tex"
            ? "textures/hires/" + owner[5..]
            : null;
    }

    public static ResourceKey WorldSettings(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        string suffix = language.Trim().ToLowerInvariant() switch
        {
            "en" => "us",
            "fr" => "fr",
            "de" => "gr",
            "es" => "sp",
            "it" => "it",
            "jp" => "jp",
            _ => throw new NotSupportedException($"Native world settings language '{language}' is unsupported.")
        };
        return new ResourceKey($"world/wmset{suffix}.obj");
    }
}
