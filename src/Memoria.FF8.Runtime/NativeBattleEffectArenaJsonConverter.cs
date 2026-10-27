using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed class NativeBattleEffectArenaJsonConverter : JsonConverter<NativeBattleEffectArena>
{
    public override NativeBattleEffectArena Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ReadSnapshot(ref reader, options);
    public override void Write(Utf8JsonWriter writer, NativeBattleEffectArena value, JsonSerializerOptions options) => WriteSnapshot(writer, value, options);

    internal static NativeBattleEffectArenaSnapshot ReadSnapshot(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (!TryProperty(root, "Regions", options, out JsonElement regions))
        {
            throw new JsonException("Native arena snapshot requires its region owner.");
        }
        ImmutableArray<NativeBattleEffectUnknownRegion> unknown = TryProperty(root, "UnknownRegions", options, out JsonElement unknownValue) ? unknownValue.Deserialize<ImmutableArray<NativeBattleEffectUnknownRegion>>(options) : [];
        ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers = TryProperty(root, "ResourcePointers", options, out JsonElement pointerValue) ? pointerValue.Deserialize<ImmutableDictionary<int, NativeBattleResourcePointer?>>(options) : null;
        int logicalLength = TryProperty(root, "LogicalLength", options, out JsonElement logicalLengthValue) ? logicalLengthValue.GetInt32() : NativeBattleEffectArena.Length;
        NativeBattleEffectArenaSnapshot snapshot = NativeBattleEffectArena.CreateSnapshot(regions.Deserialize<ImmutableArray<NativeBattleEffectMemoryRegion>>(options), unknown, pointers, logicalLength);
        NativeBattleStorageReference? reference = TryProperty(root, "StorageReference", options, out JsonElement referenceValue) ? referenceValue.Deserialize<NativeBattleStorageReference>(options) : null;
        return snapshot.WithStorageReference(reference);
    }

    internal static void WriteSnapshot(Utf8JsonWriter writer, NativeBattleEffectArena value, JsonSerializerOptions options)
    {
        if (!value.IsSnapshot)
        {
            throw new InvalidOperationException("Working arena serialization requires an explicit detached capture.");
        }
        value.Validate();
        writer.WriteStartObject();
        if (value.LogicalLength != NativeBattleEffectArena.Length)
        {
            writer.WriteNumber(PropertyName("LogicalLength", options), value.LogicalLength);
        }
        writer.WritePropertyName(PropertyName("Regions", options));
        JsonSerializer.Serialize(writer, value.Regions, options);
        writer.WritePropertyName(PropertyName("UnknownRegions", options));
        JsonSerializer.Serialize(writer, value.UnknownRegions, options);
        if (value.ResourcePointers is { } pointers)
        {
            writer.WritePropertyName(PropertyName("ResourcePointers", options));
            JsonSerializer.Serialize(writer, pointers, options);
        }
        if (value.StorageReference is { } reference)
        {
            writer.WritePropertyName(PropertyName("StorageReference", options));
            JsonSerializer.Serialize(writer, reference, options);
        }
        writer.WriteEndObject();
    }

    private static string PropertyName(string name, JsonSerializerOptions options) => options.PropertyNamingPolicy?.ConvertName(name) ?? name;

    private static bool TryProperty(JsonElement root, string name, JsonSerializerOptions options, out JsonElement value)
    {
        string expected = PropertyName(name, options);
        if (!options.PropertyNameCaseInsensitive)
        {
            return root.TryGetProperty(expected, out value);
        }
        bool found = false;
        value = default;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, expected, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                found = true;
            }
        }
        return found;
    }
}

public sealed class NativeBattleEffectArenaSnapshotJsonConverter : JsonConverter<NativeBattleEffectArenaSnapshot>
{
    public override NativeBattleEffectArenaSnapshot Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => NativeBattleEffectArenaJsonConverter.ReadSnapshot(ref reader, options);
    public override void Write(Utf8JsonWriter writer, NativeBattleEffectArenaSnapshot value, JsonSerializerOptions options) => NativeBattleEffectArenaJsonConverter.WriteSnapshot(writer, value, options);
}
