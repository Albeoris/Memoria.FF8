using System.Text.RegularExpressions;

namespace Memoria.FF8.Resources.Localization;

/// <summary>A complete localization identity, optionally bound to the selected original-resource language.</summary>
public sealed record LocalizedTextReference
{
    private const string SourceLanguagePrefix = "${LANG}_";

    public string Key { get; }
    public bool UsesSourceLanguage => Key.StartsWith(SourceLanguagePrefix, StringComparison.Ordinal);

    public LocalizedTextReference(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidDataException("A localization reference requires a complete canonical key.");
        }
        string identity;
        if (key.StartsWith(SourceLanguagePrefix, StringComparison.Ordinal))
        {
            identity = key[SourceLanguagePrefix.Length..];
            Key = key;
        }
        else
        {
            Match match = Regex.Match(key, @"\A\$(?<language>[A-Za-z]+)_(?<identity>[A-Za-z0-9_.-]+)\z", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                throw new InvalidDataException("A localization reference requires a complete canonical key or a ${LANG}_ template.");
            }
            string language;
            try
            {
                language = CanonicalLanguage(match.Groups["language"].Value);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("A localization reference requires a supported original-resource language.", exception);
            }
            identity = match.Groups["identity"].Value;
            Key = "$" + language + "_" + identity;
        }
        string systemPrefix = SystemTextCatalog.File + "_";
        bool authoredSystem = identity.StartsWith(systemPrefix, StringComparison.Ordinal) && SystemTextCatalog.Defaults.ContainsKey(identity[systemPrefix.Length..]);
        if (!authoredSystem && !Regex.IsMatch(identity, @"\A[A-Za-z0-9_][A-Za-z0-9_.-]*_[0-9]{4,}(?:_ctx_[a-fA-F0-9]{8,64})?\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException("A localization reference requires a complete canonical ordinal key or a known system text identity.");
        }
    }

    public static LocalizedTextReference Parse(string key)
    {
        return new(key);
    }

    /// <summary>Uses the original-resource language, independently of the catalog's translated output language.</summary>
    public string ResolveKey(string sourceLanguage)
    {
        string language = CanonicalLanguage(sourceLanguage);
        return UsesSourceLanguage ? "$" + language + "_" + Key[SourceLanguagePrefix.Length..] : Key;
    }

    public static string CanonicalLanguage(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return language.ToLowerInvariant() switch
        {
            "en" => "en",
            "fr" => "fr",
            "de" => "de",
            "es" => "es",
            "it" => "it",
            "ja" or "jp" => "jp",
            _ => throw new ArgumentException("Localization reference language must be en, fr, de, es, it, jp or ja.", nameof(language))
        };
    }
}
