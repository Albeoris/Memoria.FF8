using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Preserves an unmaterialized resource's legacy empty-array representation.</summary>
public sealed class NativeBattlePendingNodeSourceConverter : JsonConverter<NativeBattleNodeSource?>
{
    public override bool HandleNull => true;

    public override NativeBattleNodeSource? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException("Pending native resource bytes require their explicit empty array.");
        }
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement value = document.RootElement;
        return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0 ? null : value.Deserialize<NativeBattleNodeSource>(options);
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleNodeSource? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
