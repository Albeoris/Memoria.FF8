using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public sealed record LanguageNativeVariant(string Edition, string Language)
{
    public bool Matches(GameLanguageProfile profile)
    {
        return Edition.Equals(profile.Edition, StringComparison.OrdinalIgnoreCase)
            && LanguagePackages.DisplayTag(Language) == LanguagePackages.DisplayTag(profile.ResourceLanguage);
    }
}

public sealed record LanguageOfferMetadata(string Locale, string Label, string? Option, IReadOnlyList<LanguageNativeVariant> NativeVariants)
{
    public string? Setting { get; init; }
    public int? Value { get; init; }
}

public static class LanguagePackages
{
    private sealed record JunctionProvider(string SourceId, Guid Id, string Version, long? DownloadBytes, IReadOnlyList<LanguageOfferMetadata> Offers);
    private static readonly JunctionProvider[] Reviewed = ReadReviewedProviders();
    private static readonly IReadOnlyDictionary<string, string> LanguageNames = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
        .Where(culture => culture.Name.Length > 0)
        .GroupBy(culture => culture.EnglishName, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() == 1)
        .ToDictionary(group => group.Key, group => group.Single().Name, StringComparer.OrdinalIgnoreCase);

    public static bool IsLanguage(RemoteModEntry entry)
    {
        return entry.LanguageOffers.Count > 0 || IsDependency(entry) || entry.SourceId == RemoteModCatalogAdapter.SourceId
            && (entry.Tags.Contains("language", StringComparer.Ordinal) || FindReviewed(entry.SourceId, entry.Id, entry.Version) is not null);
    }

    public static bool IsLanguage(ModPackage package)
    {
        return package.LocalizationOnly || package.HasLocalization && package.Descriptor?.Group == "Localization"
            || IsDependency(package) || package.Descriptor?.LanguageOffers.Count > 0 || package.Official?.LanguageOffers?.Count > 0
            || package.JunctionMetadata is { Id: Guid id, Version: string version }
            && (package.IsLanguageProvider || FindReviewed(RemoteModCatalogAdapter.SourceId, id, version) is not null);
    }

    public static bool IsDependency(RemoteModEntry entry) => entry.Official?.IsLanguageDependency == true;

    public static bool IsDependency(ModPackage package) => package.Descriptor?.IsLanguageDependency == true || package.Official?.IsLanguageDependency == true;

    internal static IReadOnlyList<ModPackage> ResolveInstalledDependencies(ModPackage root, IEnumerable<ModPackage> packages, GameLanguageProfile profile)
    {
        List<ModPackage> ordered = [];
        HashSet<string> visiting = new(StringComparer.Ordinal);
        void Visit(ModPackage package)
        {
            if (!visiting.Add(package.Id))
            {
                throw new InvalidDataException($"Cyclic language dependency at '{package.Name}'.");
            }
            foreach (string id in package.Descriptor?.Dependencies ?? [])
            {
                ModPackage[] candidates = packages.Where(candidate => candidate.Descriptor?.Id == id && IsDependency(candidate)).ToArray();
                if (candidates.Length != 1 || candidates[0].Official is not null && !candidates[0].OfficialAvailable)
                {
                    throw new InvalidDataException($"Required language package '{id}' must be installed and intact.");
                }
                ModPackage member = candidates[0];
                if (member.Descriptor?.NativeVariants.Any(variant => variant.Matches(profile)) != true)
                {
                    throw new InvalidDataException($"Required language package '{id}' does not support the selected base.");
                }
                if (!ordered.Contains(member))
                {
                    Visit(member);
                    ordered.Add(member);
                }
            }
            visiting.Remove(package.Id);
        }
        Visit(root);
        return ordered.AsReadOnly();
    }

    public static bool IsLanguage(ModPackage package, IEnumerable<RemoteModEntry> entries)
    {
        return IsLanguage(package) || package.JunctionMetadata?.Id is Guid id
            && entries.Any(entry => entry.SourceId == RemoteModCatalogAdapter.SourceId && entry.Id == id && IsLanguage(entry));
    }

    public static string? DisplayTag(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return null;
        }
        string tag = identity.Trim().Split(' ', 2)[0];
        if (tag.Equals("jp", StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }
        if (!Regex.IsMatch(tag, @"\A[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8})*\z", RegexOptions.CultureInvariant))
        {
            return null;
        }
        try
        {
            return CultureInfo.GetCultureInfo(tag).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    internal static void ValidateOffers(IReadOnlyList<LanguageOfferMetadata>? offers)
    {
        if (offers is not { Count: > 0 and <= 64 } || offers.Any(offer => offer is null || offer.Locale is null || DisplayTag(offer.Locale) is null
            || offer.Locale.Contains(' ') || !ValidText(offer.Label) || !ValidText(offer.Option)
            || !ValidVariants(offer.NativeVariants))
            || offers.Select(offer => (DisplayTag(offer.Locale), offer.Option)).Distinct().Count() != offers.Count)
        {
            throw new InvalidDataException("Invalid or duplicate language offer metadata.");
        }
    }

    internal static bool ValidVariants(IReadOnlyList<LanguageNativeVariant>? variants)
    {
        return variants is { Count: > 0 and <= 16 } && variants.All(variant => variant is not null && variant.Edition is "Classic" or "Remastered"
            && DisplayTag(variant.Language) is not null && !variant.Language.Contains(' ')) && variants.Distinct().Count() == variants.Count;
    }

    private static bool ValidText(string? value)
    {
        return value is { Length: > 0 and <= 160 } && value == value.Trim() && !value.Any(char.IsControl);
    }

    internal static IReadOnlyList<LanguageOfferMetadata> JunctionOffers(RemoteModEntry? entry, JunctionManifestMetadata? installation)
    {
        Guid? id = entry?.Id ?? installation?.Id;
        string? version = entry?.Version ?? installation?.Version;
        JunctionProvider? reviewed = id is Guid key && version is not null ? FindReviewed(RemoteModCatalogAdapter.SourceId, key, version) : null;
        if (entry is null || !entry.Tags.Contains("language", StringComparer.Ordinal))
        {
            return reviewed?.Offers ?? [];
        }
        LanguageNativeVariant[] variants = JunctionVariants(entry, installation);
        return entry.Tags.Select(tag => LanguageNames.GetValueOrDefault(tag)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tag =>
            {
                LanguageOfferMetadata? binding = reviewed?.Offers.FirstOrDefault(offer => offer.Locale == tag);
                return binding is null ? new LanguageOfferMetadata(tag, entry.Name, tag, variants) : binding with { NativeVariants = variants };
            }).ToArray();
    }

    internal static bool IsReviewedJunctionOffer(RemoteModEntry entry, LanguageOfferMetadata offer)
    {
        return entry.SourceId == RemoteModCatalogAdapter.SourceId && FindReviewed(entry.SourceId, entry.Id, entry.Version)?.Offers.Any(candidate => candidate.Locale == offer.Locale) == true;
    }

    internal static long? JunctionDownloadBytes(RemoteModEntry entry)
    {
        return FindReviewed(entry.SourceId, entry.Id, entry.Version)?.DownloadBytes;
    }

    internal static JunctionOptionValue? JunctionOption(JunctionManifestMetadata manifest, LanguageOfferMetadata offer)
    {
        if (offer.Setting is string setting && offer.Value is int value)
        {
            return manifest.OptionValues.SingleOrDefault(option => option.Setting.Equals(setting, StringComparison.OrdinalIgnoreCase) && option.Value == value);
        }
        CultureInfo culture = CultureInfo.GetCultureInfo(offer.Locale);
        JunctionOptionValue[] matches = manifest.OptionValues.Where(option => option.Name.Equals(culture.EnglishName, StringComparison.OrdinalIgnoreCase)
            || option.Name.Equals(culture.NativeName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static LanguageNativeVariant[] JunctionVariants(RemoteModEntry entry, JunctionManifestMetadata? installed)
    {
        IReadOnlyList<string> declared = installed?.GameLanguages is { Count: > 0 } local ? local : entry.GameLanguages;
        bool supportsEnglish = declared.Count == 0 || declared.Contains("ANY", StringComparer.OrdinalIgnoreCase) || declared.Contains("EN", StringComparer.OrdinalIgnoreCase);
        return supportsEnglish ? [new("Classic", "en")] : declared.Select(DisplayTag).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Select(tag => new LanguageNativeVariant("Classic", tag)).ToArray();
    }

    internal static IReadOnlyList<LanguageOfferMetadata> NativeOffers(ModPackage package)
    {
        if (package.Descriptor?.LanguageOffers is { Count: > 0 } declarations)
        {
            return declarations;
        }
        if (!package.HasLocalization)
        {
            return [];
        }
        try
        {
            string path = LocalizationProjectIO.Resolve(package.RootDirectory, LocalizationProjectIO.ManifestRelativePath);
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > 16 * 1024 * 1024)
            {
                return [];
            }
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;
            string? target = root.GetProperty("targetLanguage").GetString();
            if (DisplayTag(target) is not string tag)
            {
                return [];
            }
            string nativeLanguage = GetText(root, "fieldScriptBaseLanguage") ?? GetText(root, "sourceLanguage") ?? "en";
            string[] editions = GetText(root, "sourceEdition") is string edition ? [edition] : ["Classic", "Remastered"];
            LanguageNativeVariant[] variants = root.TryGetProperty("compatibleVariants", out JsonElement declared) && declared.GetArrayLength() > 0
                ? declared.EnumerateArray().SelectMany(value =>
                {
                    string[] parts = value.GetString()!.Split(':', 2);
                    return parts.Length == 2 ? [new LanguageNativeVariant(parts[0], parts[1])] : editions.Select(name => new LanguageNativeVariant(name, parts[0]));
                }).ToArray()
                : editions.Select(name => new LanguageNativeVariant(name, nativeLanguage)).ToArray();
            if (root.GetProperty("schemaVersion").GetInt32() >= 2)
            {
                variants = variants.Where(variant => DisplayTag(variant.Language) == DisplayTag(nativeLanguage)).ToArray();
            }
            return [new(tag, package.Name, target, variants)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return [];
        }
    }

    internal static string ReadNativeTarget(ModPackage package)
    {
        string path = LocalizationProjectIO.Resolve(package.RootDirectory, LocalizationProjectIO.ManifestRelativePath);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > 16 * 1024 * 1024)
        {
            throw new InvalidDataException("The localization manifest exceeds its size limit.");
        }
        using JsonDocument document = JsonDocument.Parse(stream);
        return GetText(document.RootElement, "targetLanguage") ?? throw new InvalidDataException("The localization manifest has no target language.");
    }

    private static string? GetText(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement text) ? text.GetString() : null;
    }

    private static JunctionProvider[] ReadReviewedProviders()
    {
        using Stream stream = typeof(LanguagePackages).Assembly.GetManifestResourceStream("Memoria.LanguageProviders")!;
        return JsonSerializer.Deserialize<JunctionProvider[]>(stream, OfficialModCatalog.Json)!;
    }

    private static JunctionProvider? FindReviewed(string source, Guid id, string version)
    {
        return Reviewed.FirstOrDefault(provider => provider.SourceId == source && provider.Id == id && provider.Version == version);
    }
}
