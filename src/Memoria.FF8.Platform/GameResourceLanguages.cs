namespace Memoria.FF8.Platform;

/// <summary>An explicit content-language choice, independent of the executable and storage profiles.</summary>
public static class GameResourceLanguages
{
    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly(new[] { "en", "fr", "de", "es", "it" });

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        string language = value.Trim().ToLowerInvariant();
        if (language == "auto")
        {
            return null;
        }
        if (!Supported.Contains(language, StringComparer.Ordinal))
        {
            throw new ArgumentException("Resource language must be auto, en, fr, de, es or it.", nameof(value));
        }
        return language;
    }
}
