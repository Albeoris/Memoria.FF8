using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods;

public sealed class NativeModDescriptor
{
    public const string FileName = "mod.json";
    private const int MaximumBytes = 64 * 1024;

    private NativeModDescriptor(string id, string name, string group, IReadOnlyDictionary<string, string> descriptions, IReadOnlyList<LanguageOfferMetadata> languageOffers, IReadOnlyList<LanguageNativeVariant> nativeVariants, IReadOnlyList<string> dependencies)
    {
        Id = id;
        Name = name;
        Group = group;
        Descriptions = descriptions;
        LanguageOffers = languageOffers;
        NativeVariants = nativeVariants;
        Dependencies = dependencies;
    }

    public string Id { get; }
    public string RuntimePackageId => RuntimeId(Id);
    public string Name { get; }
    public string Group { get; }
    public IReadOnlyDictionary<string, string> Descriptions { get; }
    public IReadOnlyList<LanguageOfferMetadata> LanguageOffers { get; }
    public IReadOnlyList<LanguageNativeVariant> NativeVariants { get; }
    public IReadOnlyList<string> Dependencies { get; }
    public bool IsLanguageDependency => NativeVariants.Count > 0;

    public string DescriptionForLanguage(string? language)
    {
        return TextForLanguage(Descriptions, language);
    }

    internal static string RuntimeId(string id)
    {
        return "Memoria/" + id;
    }

    internal static bool IsId(string? id)
    {
        return id is { Length: > 0 and <= 80 } && Regex.IsMatch(id, @"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    }

    public static NativeModDescriptor Read(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Native mod descriptors must be regular files.");
        }
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (input.Length > MaximumBytes)
        {
            throw new InvalidDataException("Native mod descriptor exceeds its size limit.");
        }
        byte[] bytes = new byte[input.Length];
        input.ReadExactly(bytes);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            return Parse(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new InvalidDataException("Invalid native mod descriptor JSON.", exception);
        }
    }

    private static NativeModDescriptor Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A native mod descriptor must be an object.");
        }
        Dictionary<string, JsonElement> properties = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is not ("id" or "name" or "package" or "description" or "kind" or "languageOffers" or "nativeVariants" or "dependencies") || !properties.TryAdd(property.Name, property.Value))
            {
                throw new InvalidDataException($"Unknown or duplicate native mod descriptor property '{property.Name}'.");
            }
        }
        string id = ReadText(properties.GetValueOrDefault("id"), "id");
        if (!IsId(id))
        {
            throw new InvalidDataException("A native mod descriptor id must be a lowercase slug.");
        }
        string name = ReadText(properties.GetValueOrDefault("name"), "name");
        string group = ReadText(properties.GetValueOrDefault("package"), "package");
        IReadOnlyList<LanguageOfferMetadata> languageOffers = [];
        IReadOnlyList<LanguageNativeVariant> nativeVariants = [];
        string[] dependencies = [];
        if (properties.ContainsKey("kind") || properties.ContainsKey("languageOffers") || properties.ContainsKey("nativeVariants") || properties.ContainsKey("dependencies"))
        {
            string kind = ReadText(properties.GetValueOrDefault("kind"), "kind");
            if (kind == "language" && properties.TryGetValue("languageOffers", out JsonElement declarations) && !properties.ContainsKey("nativeVariants"))
            {
                languageOffers = declarations.Deserialize<LanguageOfferMetadata[]>(OfficialModCatalog.Json) ?? [];
                LanguagePackages.ValidateOffers(languageOffers);
            }
            else if (kind == "dependency" && properties.TryGetValue("nativeVariants", out JsonElement variants) && !properties.ContainsKey("languageOffers"))
            {
                nativeVariants = variants.Deserialize<LanguageNativeVariant[]>(OfficialModCatalog.Json) ?? [];
                if (!LanguagePackages.ValidVariants(nativeVariants))
                {
                    throw new InvalidDataException("A language dependency requires valid native variants.");
                }
            }
            else
            {
                throw new InvalidDataException("The descriptor's language or dependency kind does not match its metadata.");
            }
            if (properties.TryGetValue("dependencies", out JsonElement requirements))
            {
                dependencies = requirements.Deserialize<string[]>() ?? throw new InvalidDataException("Language dependencies cannot be null.");
                if (dependencies.Length > 32 || dependencies.Any(dependency => !IsId(dependency) || dependency == id)
                    || dependencies.Distinct(StringComparer.Ordinal).Count() != dependencies.Length)
                {
                    throw new InvalidDataException("A native descriptor contains invalid or duplicate language dependencies.");
                }
            }
        }
        return new(id, name, group, ReadTranslations(properties.GetValueOrDefault("description"), "description", multiline: true), languageOffers, nativeVariants, Array.AsReadOnly(dependencies));
    }

    internal static string TextForLanguage(IReadOnlyDictionary<string, string> translations, string? language)
    {
        return translations.GetValueOrDefault(NormalizeLanguage(language ?? "en")) ?? translations["en"];
    }

    internal static IReadOnlyDictionary<string, string> ReadTranslations(JsonElement translations, string field, bool multiline = false)
    {
        if (translations.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The '{field}' field requires an object of localized text.");
        }
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in translations.EnumerateObject())
        {
            string language = NormalizeLanguage(property.Name);
            if (!Regex.IsMatch(property.Name, @"\A[a-z]{2,3}(-[A-Za-z0-9]{2,8})*\z", RegexOptions.CultureInvariant) || !result.TryAdd(language, ReadText(property.Value, property.Name, multiline)))
            {
                throw new InvalidDataException($"Invalid or duplicate language '{property.Name}' in '{field}'.");
            }
        }
        if (!result.ContainsKey("en"))
        {
            throw new InvalidDataException($"The '{field}' field requires English text.");
        }
        return new ReadOnlyDictionary<string, string>(result);
    }

    internal static string ReadText(JsonElement value, string property, bool multiline = false)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text || string.IsNullOrWhiteSpace(text)
            || text.Any(character => char.IsControl(character) && !(multiline && character is '\n' or '\r' or '\t')))
        {
            throw new InvalidDataException($"Invalid or missing native mod text '{property}'.");
        }
        return text;
    }

    private static string NormalizeLanguage(string language)
    {
        string normalized = language.ToLowerInvariant();
        return normalized is "ja" or "jp" ? LocalizedTextReference.CanonicalLanguage(normalized) : normalized;
    }
}
