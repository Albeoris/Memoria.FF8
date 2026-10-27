using Albeoris.Games.FF8.TextEncoding.Tags;

namespace Albeoris.Games.FF8.TextEncoding;

/// <summary>Language-qualified native dictionary operands, independent of the glyph codepage.</summary>
public static class TextTagTerms
{
    public static bool TryGetLanguage(string tag, out FF8TextLanguage language)
    {
        language = default;
        return tag.StartsWith("Term", StringComparison.Ordinal) && Enum.TryParse(tag[4..], out language) && Enum.IsDefined(language) && tag == "Term" + language;
    }

    public static Enum Parameter(FF8TextLanguage language, byte value) => language switch
    {
        FF8TextLanguage.EN => (TextTagTermEN)value,
        FF8TextLanguage.FR => (TextTagTermFR)value,
        FF8TextLanguage.DE => (TextTagTermDE)value,
        FF8TextLanguage.ES => (TextTagTermES)value,
        FF8TextLanguage.IT => (TextTagTermIT)value,
        FF8TextLanguage.JP => (TextTagTermJP)value,
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    public static bool TryParse(FF8TextLanguage language, string text, out byte value)
    {
        Type type = Parameter(language, 0).GetType();
        if (!text.Contains(',') && Enum.TryParse(type, text, out object? parameter))
        {
            value = Convert.ToByte(parameter, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        value = 0;
        return false;
    }
}

public static class FF8TextLanguages
{
    public static FF8TextLanguage Parse(string language) => language.ToLowerInvariant() switch
    {
        "en" => FF8TextLanguage.EN,
        "fr" => FF8TextLanguage.FR,
        "de" => FF8TextLanguage.DE,
        "es" => FF8TextLanguage.ES,
        "it" => FF8TextLanguage.IT,
        "jp" or "ja" => FF8TextLanguage.JP,
        _ => throw new ArgumentException("A supported native text language is required.", nameof(language))
    };
}
