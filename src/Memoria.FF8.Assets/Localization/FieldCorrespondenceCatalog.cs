using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Approved field source-slot correspondence across game languages and editions.</summary>
public static class FieldCorrespondenceCatalog
{
    public const string Version = "source-ordinal-v1";

    /// <summary>Fingerprint of the multilingual source-ordinal mapping exported by existing projects.</summary>
    public const string Fingerprint = "F03F1D1BA1EEB38D0946C5289BA9D35B7EA91E33AAE8F95CFAA40BF2F2100F50";

    public const string CatalogFingerprint = "2037B5A221F5D766E19F7727CE0E8E6D69369028DB6EA54BE4D5520193A10616";

    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];
    private static readonly Lazy<Catalog> Data = new(Load);

    public static bool Contains(string key) => Data.Value.Groups.ContainsKey(key);

    public static int SourceCount => Data.Value.Groups.Count;

    public static IReadOnlyList<string> GetKeys(string language)
    {
        string prefix = "$" + FieldLocalizationKey.CanonicalLanguage(language) + "_";
        return Data.Value.Groups.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
    }

    public static int SourceCountForLanguage(string language)
    {
        string prefix = "$" + FieldLocalizationKey.CanonicalLanguage(language) + "_";
        return Data.Value.Groups.Keys.Count(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static string GetCanonicalKey(string language, string field, int ordinal)
    {
        return FieldLocalizationKey.Create(language, field, ordinal);
    }

    public static IReadOnlyList<string> GetEquivalents(string sourceKey, string targetLanguage)
    {
        string language = FieldLocalizationKey.CanonicalLanguage(targetLanguage);
        return Data.Value.Groups.TryGetValue(sourceKey, out string[][]? group) ? Array.AsReadOnly(group[Array.IndexOf(Languages, language)]) : [];
    }

    public static bool TryGetEquivalentKey(string sourceKey, string targetLanguage, [NotNullWhen(true)] out string? key)
    {
        IReadOnlyList<string> keys = GetEquivalents(sourceKey, targetLanguage);
        key = keys.Count == 1 ? keys[0] : null;
        return key is not null;
    }

    /// <summary>Resolves an English MSD key using identical banks or an approved pair of different bank fingerprints.</summary>
    public static bool TryGetEnglishEditionKey(string sourceKey, GameDataEdition sourceEdition, string? sourceMsdHash, GameDataEdition targetEdition, string? targetMsdHash, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (sourceEdition is not (GameDataEdition.Classic or GameDataEdition.Remastered) || targetEdition is not (GameDataEdition.Classic or GameDataEdition.Remastered) || !IsSha256(sourceMsdHash) || !IsSha256(targetMsdHash) || !sourceKey.StartsWith("$en_", StringComparison.Ordinal))
        {
            return false;
        }
        if (StringComparer.OrdinalIgnoreCase.Equals(sourceMsdHash, targetMsdHash))
        {
            key = Contains(sourceKey) ? sourceKey : null;
            return key is not null;
        }
        if (sourceEdition == targetEdition)
        {
            return false;
        }
        int separator = sourceKey.LastIndexOf('_');
        if (separator <= 4 || !Data.Value.EnglishEditionBanks.TryGetValue(sourceKey[4..separator], out EnglishEditionBank? bank))
        {
            return false;
        }
        if (sourceEdition == GameDataEdition.Classic && bank.Matches(sourceMsdHash, targetMsdHash))
        {
            return bank.ClassicToRemastered.TryGetValue(sourceKey, out key);
        }
        return sourceEdition == GameDataEdition.Remastered && bank.Matches(targetMsdHash, sourceMsdHash) && bank.RemasteredToClassic.TryGetValue(sourceKey, out key);
    }

    public static bool IsReviewedEditionTextEquivalent(string nativeBaseKey) => Data.Value.ReviewedSharedFields.Contains(nativeBaseKey) || Data.Value.ReviewedSharedNonFields.ContainsKey(nativeBaseKey);

    /// <summary>Confirms that the reviewed non-field wording applies to both source fingerprints.</summary>
    public static bool IsReviewedEditionTextEquivalent(string nativeBaseKey, GameDataEdition sourceEdition, string? sourceFingerprint, GameDataEdition targetEdition, string? targetFingerprint)
    {
        if (sourceEdition == targetEdition || !Data.Value.ReviewedSharedNonFields.TryGetValue(nativeBaseKey, out NonFieldFingerprints? fingerprints))
        {
            return false;
        }
        return (sourceEdition, targetEdition) switch
        {
            (GameDataEdition.Classic, GameDataEdition.Remastered) => fingerprints.Matches(sourceFingerprint, targetFingerprint),
            (GameDataEdition.Remastered, GameDataEdition.Classic) => fingerprints.Matches(targetFingerprint, sourceFingerprint),
            _ => false
        };
    }

    private static Catalog Load()
    {
        using Stream stream = typeof(FieldCorrespondenceCatalog).Assembly.GetManifestResourceStream("Memoria.FF8.Assets.Localization.Data.field-correspondence.json") ?? throw new InvalidDataException("Field correspondence resource is missing.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)) != CatalogFingerprint)
        {
            throw new InvalidDataException("Field correspondence catalog fingerprint mismatch.");
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported field correspondence catalog schema.");
        }
        Dictionary<string, string[][]> groups = new(StringComparer.Ordinal);
        JsonElement groupsJson = root.GetProperty("fieldGroups");
        foreach (JsonElement row in groupsJson.EnumerateArray())
        {
            string field = row[0].GetString()!;
            string[][] keys = Languages.Select((language, index) => row[index + 1].EnumerateArray().Select(value => FieldLocalizationKey.Create(language, field, value.GetInt32())).ToArray()).ToArray();
            foreach (string key in keys.SelectMany(group => group))
            {
                if (!groups.TryAdd(key, keys))
                {
                    throw new InvalidDataException("Duplicate field source slot.");
                }
            }
        }
        if (groupsJson.GetArrayLength() != 22446 || groups.Count != 134519)
        {
            throw new InvalidDataException("Incomplete field correspondence resource.");
        }
        Dictionary<string, EnglishEditionBank> banks = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.GetProperty("englishEditionBanks").EnumerateObject())
        {
            JsonElement bank = property.Value;
            Dictionary<string, string> toRemastered = new(StringComparer.Ordinal);
            Dictionary<string, string> toClassic = new(StringComparer.Ordinal);
            foreach (JsonElement pair in bank.GetProperty("matches").EnumerateArray())
            {
                string classicKey = FieldLocalizationKey.Create("en", property.Name, pair[0].GetInt32());
                string remasteredKey = FieldLocalizationKey.Create("en", property.Name, pair[1].GetInt32());
                if (!groups.ContainsKey(remasteredKey) || !toRemastered.TryAdd(classicKey, remasteredKey) || !toClassic.TryAdd(remasteredKey, classicKey))
                {
                    throw new InvalidDataException("Invalid English edition correspondence slot.");
                }
            }
            banks.Add(property.Name, new EnglishEditionBank(bank.GetProperty("classicMsd").GetString()!, bank.GetProperty("remasteredMsd").GetString()!, toRemastered, toClassic));
        }
        HashSet<string> sharedFields = root.GetProperty("reviewedSharedFields").EnumerateArray().Select(value => value.GetString()!).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, NonFieldFingerprints> sharedNonFields = root.GetProperty("reviewedSharedNonFields").EnumerateObject().ToDictionary(property => property.Name, property => new NonFieldFingerprints(property.Value[0].GetString()!, property.Value[1].GetString()!), StringComparer.Ordinal);
        return new Catalog(groups, banks, sharedFields, sharedNonFields);
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private sealed record Catalog(Dictionary<string, string[][]> Groups, Dictionary<string, EnglishEditionBank> EnglishEditionBanks, HashSet<string> ReviewedSharedFields, Dictionary<string, NonFieldFingerprints> ReviewedSharedNonFields);

    private sealed record EnglishEditionBank(string ClassicMsd, string RemasteredMsd, Dictionary<string, string> ClassicToRemastered, Dictionary<string, string> RemasteredToClassic)
    {
        public bool Matches(string? classicMsd, string? remasteredMsd) => StringComparer.OrdinalIgnoreCase.Equals(ClassicMsd, classicMsd) && StringComparer.OrdinalIgnoreCase.Equals(RemasteredMsd, remasteredMsd);
    }

    private sealed record NonFieldFingerprints(string Classic, string Remastered)
    {
        public bool Matches(string? classic, string? remastered) => StringComparer.OrdinalIgnoreCase.Equals(Classic, classic) && StringComparer.OrdinalIgnoreCase.Equals(Remastered, remastered);
    }
}
