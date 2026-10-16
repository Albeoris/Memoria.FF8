using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albeoris.Games.Core.NsResources;

namespace Albeoris.Games.FF8.TextEncoding;

/// <summary>
/// The built-in per-field Japanese character sets shipped with the game, loaded from the
/// embedded default resource.
/// </summary>
public sealed class DefaultJapaneseFieldCharacters : IFieldCharacterProvider
{
    private static readonly Lazy<Registry> Defaults = new(Load);

    public FieldCharacterSet Get(String fieldName)
    {
        FieldCharacterSet? result = TryGet(fieldName);
        return result ?? throw new KeyNotFoundException($"No default field characters are registered for field '{fieldName}'.");
    }

    public FieldCharacterSet Get(Int32 fieldId)
    {
        FieldCharacterSet? result = TryGet(fieldId);
        return result ?? throw new KeyNotFoundException($"No default field characters are registered for field id {fieldId}.");
    }

    public FieldCharacterSet? TryGet(String fieldName)
    {
        Defaults.Value.ByName.TryGetValue(fieldName, out var result);
        return result;
    }

    public FieldCharacterSet? TryGet(Int32 fieldId)
    {
        Defaults.Value.ById.TryGetValue(fieldId, out var result);
        return result;
    }

    private static Registry Load()
    {
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        Assembly assembly = typeof(DefaultJapaneseFieldCharacters).Assembly;
        using (Stream stream = EmbeddedResources.Open(assembly, "/Resources/japanese.field-characters.json"))
        {
            Document? document = JsonSerializer.Deserialize<Document>(stream, options);
            if (document is null)
            {
                throw new InvalidOperationException("Embedded resource 'japanese.field-characters.json' could not be parsed.");
            }

            Char placeholder = document.Placeholder.Length == 0 ? ' ' : document.Placeholder[0];

            Dictionary<String, FieldCharacterSet> byName = new Dictionary<String, FieldCharacterSet>(StringComparer.OrdinalIgnoreCase);
            Dictionary<Int32, FieldCharacterSet> byId = new Dictionary<Int32, FieldCharacterSet>();

            foreach (FieldEntry entry in document.Fields)
            {
                FieldCharacterSet set = new FieldCharacterSet(entry.Id, entry.Name, entry.Characters, placeholder);
                byName[entry.Name] = set;
                byId[entry.Id] = set;
            }

            return new Registry(byName, byId);
        }
    }

    private sealed class Registry
    {
        public Registry(Dictionary<String, FieldCharacterSet> byName, Dictionary<Int32, FieldCharacterSet> byId)
        {
            ByName = byName;
            ById = byId;
        }

        public Dictionary<String, FieldCharacterSet> ByName { get; }

        public Dictionary<Int32, FieldCharacterSet> ById { get; }
    }

    private sealed class Document
    {
        [JsonPropertyName("placeholder")]
        public String Placeholder { get; set; } = String.Empty;

        [JsonPropertyName("fields")]
        public FieldEntry[] Fields { get; set; } = Array.Empty<FieldEntry>();
    }

    private sealed class FieldEntry
    {
        [JsonPropertyName("id")]
        public Int32 Id { get; set; }

        [JsonPropertyName("name")]
        public String Name { get; set; } = String.Empty;

        [JsonPropertyName("characters")]
        public String Characters { get; set; } = String.Empty;
    }
}
