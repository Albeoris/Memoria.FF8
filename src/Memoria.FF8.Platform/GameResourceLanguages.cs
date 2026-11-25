namespace Memoria.FF8.Platform;

/// <summary>An explicit content-language choice, independent of the executable and storage profiles.</summary>
public static class GameResourceLanguages
{
    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly(new[] { "en", "fr", "de", "es", "it", "ja" });

    public static IReadOnlyList<string> DiscoverClassicArchives(string gameDirectory)
    {
        string[] requiredFamilies = ["main", "menu", "field", "battle", "world", "magic"];
        string[] indexParts = [".fs", ".fi", ".fl"];
        return Supported.Where(language =>
        {
            string directory = Path.Combine(gameDirectory, "Data", "lang-" + (language == "ja" ? "jp" : language));
            return requiredFamilies.All(family => indexParts.All(part => File.Exists(Path.Combine(directory, family + part))));
        }).ToArray();
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        string language = value.Trim().ToLowerInvariant();
        if (language == "jp")
        {
            return "ja";
        }
        if (language == "auto")
        {
            return null;
        }
        if (!Supported.Contains(language, StringComparer.Ordinal))
        {
            throw new ArgumentException("Resource language must be auto, en, fr, de, es, it or ja.", nameof(value));
        }
        return language;
    }
}
