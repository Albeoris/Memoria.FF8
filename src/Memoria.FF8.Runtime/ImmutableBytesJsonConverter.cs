using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Stores physical byte buffers as base64 while accepting historical numeric arrays.</summary>
internal sealed class ImmutableBytesJsonConverter : JsonConverter<ImmutableArray<byte>>
{
    public override ImmutableArray<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetBytesFromBase64().ToImmutableArray();
        }
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected a byte buffer.");
        }
        ImmutableArray<byte>.Builder bytes = ImmutableArray.CreateBuilder<byte>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return bytes.ToImmutable();
            }
            bytes.Add(reader.GetByte());
        }
        throw new JsonException("Unterminated byte buffer.");
    }

    public override void Write(Utf8JsonWriter writer, ImmutableArray<byte> value, JsonSerializerOptions options)
    {
        if (value.IsDefault)
        {
            throw new JsonException("An uninitialized byte buffer cannot be saved as an empty buffer.");
        }
        writer.WriteBase64StringValue(value.AsSpan());
    }
}
