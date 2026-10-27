using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Canonical native archive names for an explicitly selected resource language.</summary>
public static class NativeResourceNames
{
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
