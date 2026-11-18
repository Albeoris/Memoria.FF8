using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

/// <summary>Reads the explicit actor identity and side written by diagnostic callback receipts.</summary>
public sealed class DiagnosticBattleActorReferenceJsonConverter : JsonConverter<BattleActorReference>
{
    public override BattleActorReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement value = document.RootElement;
        string actorProperty = options.PropertyNamingPolicy?.ConvertName(nameof(BattleActorReference.ActorId)) ?? nameof(BattleActorReference.ActorId);
        string sideProperty = options.PropertyNamingPolicy?.ConvertName(nameof(BattleActorReference.Side)) ?? nameof(BattleActorReference.Side);
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 2 || !value.TryGetProperty(actorProperty, out JsonElement actor) || actor.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(actor.GetString()) || !value.TryGetProperty(sideProperty, out JsonElement side))
        {
            throw new JsonException("Diagnostic actor reference requires exactly its actor identity and explicit side.");
        }
        return side.Deserialize<BattleSide>(options) switch
        {
            BattleSide.Allies => new BattlePartyActorReference(actor.GetString()!),
            BattleSide.Enemies => new BattleEnemyActorReference(actor.GetString()!),
            _ => throw new JsonException("Diagnostic actor reference has an unsupported side.")
        };
    }

    public override void Write(Utf8JsonWriter writer, BattleActorReference value, JsonSerializerOptions options)
    {
        if (value is not (BattlePartyActorReference or BattleEnemyActorReference))
        {
            throw new JsonException("Diagnostic actor reference requires a supported concrete actor identity.");
        }
        writer.WriteStartObject();
        writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(BattleActorReference.ActorId)) ?? nameof(BattleActorReference.ActorId), value.ActorId);
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(BattleActorReference.Side)) ?? nameof(BattleActorReference.Side));
        JsonSerializer.Serialize(writer, value.Side, options);
        writer.WriteEndObject();
    }
}
