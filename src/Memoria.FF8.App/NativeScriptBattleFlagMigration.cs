using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class NativeScriptBattleFlagMigration
{
    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true, Converters = { new JsonStringEnumConverter() } };

    public static NativeWorldControlState? UpgradeRetainedArrival(GameState state, NativeFieldRuntimeProfile? profile)
    {
        NativeWorldControlState? controls = state.WorldControls;
        if (profile is not { IsVerified: true } || controls is null || controls.ScriptBattleDisabled20 is not null || controls.Origin.Kind != NativeWorldControlOriginKind.VerifiedLegacyNewGame || !NativeWorldControlState.IsApprovedLegacyCheckpoint(controls.Origin.LegacyCheckpointSha256))
        {
            return null;
        }
        controls.Validate();
        if (Fingerprint(state) != NativeScriptBattleFlagMigrationProof.RetainedArrivalSemanticSha256)
        {
            return null;
        }
        // TODO(TD-010): remove this exact legacy arrival migration when ordinary saves carry the bit from New Game
        NativeWorldControlState migrated = controls with
        {
            ScriptBattleDisabled20 = false,
            ScriptBattleFlagMigration = new NativeScriptBattleFlagMigrationProof(1, NativeScriptBattleFlagMigrationProof.RetainedArrivalCheckpointSha256, NativeScriptBattleFlagMigrationProof.RetainedArrivalSemanticSha256, NativeScriptBattleFlagMigrationProof.RetainedRouteWriterManifestSha256)
        };
        migrated.Validate();
        return migrated;
    }

    internal static string Fingerprint(GameState state)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            WriteCanonical(writer, JsonSerializer.SerializeToElement(state, Options));
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement element in value.EnumerateArray())
                {
                    WriteCanonical(writer, element);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out long integer))
                {
                    writer.WriteNumberValue(integer);
                }
                else if (value.TryGetUInt64(out ulong unsigned))
                {
                    writer.WriteNumberValue(unsigned);
                }
                else
                {
                    writer.WriteNumberValue(value.GetDouble());
                }
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
