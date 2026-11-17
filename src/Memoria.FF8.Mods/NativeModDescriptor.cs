using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods;

public sealed class NativeModDescriptor
{
    public const string FileName = "mod.json";
    private const int MaximumBytes = 64 * 1024;

    private NativeModDescriptor(string id, string name, string group, Dictionary<string, string> descriptions)
    {
        Id = id;
        Name = name;
        Group = group;
        Descriptions = new ReadOnlyDictionary<string, string>(descriptions);
    }

    public string Id { get; }
    public string RuntimePackageId => RuntimeId(Id);
    public string Name { get; }
    public string Group { get; }
    public IReadOnlyDictionary<string, string> Descriptions { get; }

    public string DescriptionForLanguage(string? language)
    {
        string normalized = NormalizeLanguage(language ?? "en");
        return Descriptions.GetValueOrDefault(normalized) ?? Descriptions["en"];
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
            if (property.Name is not ("id" or "name" or "package" or "description") || !properties.TryAdd(property.Name, property.Value))
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
        JsonElement description = properties.GetValueOrDefault("description");
        if (description.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A native mod descriptor requires an object of localized descriptions.");
        }
        Dictionary<string, string> descriptions = new(StringComparer.Ordinal);
        foreach (JsonProperty property in description.EnumerateObject())
        {
            string language = NormalizeLanguage(property.Name);
            if (!Regex.IsMatch(property.Name, @"\A[a-z]{2,3}(-[A-Za-z0-9]{2,8})*\z", RegexOptions.CultureInvariant) || !descriptions.TryAdd(language, ReadText(property.Value, property.Name, multiline: true)))
            {
                throw new InvalidDataException($"Invalid or duplicate native mod descriptor language '{property.Name}'.");
            }
        }
        if (!descriptions.ContainsKey("en"))
        {
            throw new InvalidDataException("A native mod descriptor requires an English description.");
        }
        return new(id, name, group, descriptions);
    }

    private static string ReadText(JsonElement value, string property, bool multiline = false)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text || string.IsNullOrWhiteSpace(text)
            || text.Any(character => char.IsControl(character) && !(multiline && character is '\n' or '\r' or '\t')))
        {
            throw new InvalidDataException($"Invalid or missing native mod descriptor text '{property}'.");
        }
        return text;
    }

    private static string NormalizeLanguage(string language)
    {
        string normalized = language.ToLowerInvariant();
        return normalized is "ja" or "jp" ? LocalizedTextReference.CanonicalLanguage(normalized) : normalized;
    }
}
