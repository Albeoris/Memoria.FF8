using System.Globalization;
using System.Security.Cryptography;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Approved numeric source-slot correspondence. No source game text is embedded.</summary>
public static class FieldCorrespondenceCatalog
{
    public const string Version = "source-ordinal-v1";
    public const string Fingerprint = "F03F1D1BA1EEB38D0946C5289BA9D35B7EA91E33AAE8F95CFAA40BF2F2100F50";
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];
    private static readonly Lazy<IReadOnlyDictionary<string, string[][]>> Groups = new(Load);

    public static bool Contains(string key) => Groups.Value.ContainsKey(key);

    public static int SourceCount => Groups.Value.Count;
    public static IReadOnlyList<string> GetKeys(string language) => Groups.Value.Keys.Where(key => key.StartsWith("$" + FieldLocalizationKey.CanonicalLanguage(language) + "_", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
    public static int SourceCountForLanguage(string language) => Groups.Value.Keys.Count(key => key.StartsWith("$" + FieldLocalizationKey.CanonicalLanguage(language) + "_", StringComparison.Ordinal));
    public static string GetCanonicalKey(string language, string field, int ordinal)
    {
        return FieldLocalizationKey.Create(language, field, ordinal);
    }

    public static IReadOnlyList<string> GetEquivalents(string sourceKey, string targetLanguage)
    {
        string language = FieldLocalizationKey.CanonicalLanguage(targetLanguage);
        return Groups.Value.TryGetValue(sourceKey, out string[][]? group) ? Array.AsReadOnly(group[Array.IndexOf(Languages, language)]) : [];
    }

    public static bool TryGetEquivalentKey(string sourceKey, string targetLanguage, out string? key)
    {
        IReadOnlyList<string> keys = GetEquivalents(sourceKey, targetLanguage);
        key = keys.Count == 1 ? keys[0] : null;
        return key is not null;
    }

    private static IReadOnlyDictionary<string, string[][]> Load()
    {
        using Stream stream = typeof(FieldCorrespondenceCatalog).Assembly.GetManifestResourceStream("Memoria.FF8.Assets.Localization.Data.field-correspondence.csv") ?? throw new InvalidDataException("Field correspondence resource is missing.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)) != Fingerprint)
        {
            throw new InvalidDataException("Field correspondence fingerprint mismatch.");
        }
        using StreamReader reader = new(new MemoryStream(bytes));
        if (reader.ReadLine() != "field,en,fr,de,es,it,jp")
        {
            throw new InvalidDataException("Field correspondence header mismatch.");
        }
        Dictionary<string, string[][]> result = new(StringComparer.Ordinal);
        int groups = 0;
        while (reader.ReadLine() is string line)
        {
            string[] cells = line.Split(',');
            if (cells.Length != 7)
            {
                throw new InvalidDataException("Invalid field correspondence row.");
            }
            string[][] keys = Languages.Select((language, index) => cells[index + 1].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(value => FieldLocalizationKey.Create(language, cells[0], int.Parse(value, CultureInfo.InvariantCulture))).ToArray()).ToArray();
            foreach (string key in keys.SelectMany(group => group))
            {
                if (!result.TryAdd(key, keys))
                {
                    throw new InvalidDataException("Duplicate field source slot.");
                }
            }
            groups++;
        }
        if (groups != 22446 || result.Count != 134519)
        {
            throw new InvalidDataException("Incomplete field correspondence resource.");
        }
        return result;
    }
}
