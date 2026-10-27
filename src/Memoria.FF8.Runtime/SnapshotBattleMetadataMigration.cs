using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Initializes newly retained independent battle metadata without changing saved execution progress.</summary>
internal static class SnapshotBattleMetadataMigration
{
    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        JsonObject rowDefaults = JsonSerializer.SerializeToNode(NativeBattleModelRetainedFields.FromColdProcess(), options)!.AsObject();
        JsonNode actorDefaults = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(NativeBattleGilgameshZantetsukenActors.FromColdProcess()), options)!;
        List<JsonObject?> retainedOwners =
        [
            Find(saved, "NativeBattleMemory"),
            Find(saved, "Battle", "Definition", "NativeRetainedMemory"),
            Find(saved, "EventState", "Scene", "NativeEyeProcess", "Memory"),
            Find(saved, "DepartingFieldState", "Scene", "NativeEyeProcess", "Memory")
        ];
        foreach (JsonObject? retained in retainedOwners)
        {
            if (retained is null)
            {
                continue;
            }
            if (retained["ModelTransformRows"] is JsonArray rows)
            {
                foreach (JsonNode? node in rows)
                {
                    if (node is JsonObject row)
                    {
                        CompleteAbsent(row, "Flags2", rowDefaults["Flags2"]!);
                        CompleteAbsent(row, "GeometryBounds52", rowDefaults["GeometryBounds52"]!);
                    }
                }
            }
            CompleteGilgamesh(retained["EffectMemory"] as JsonObject, actorDefaults);
        }
        CompleteGilgamesh(Find(saved, "Battle", "NativeEffectMemory"), actorDefaults);

        JsonObject canonical = JsonSerializer.SerializeToNode(NativeBattlePrimitiveCapabilities.PrimaryRgba8(), options)!.AsObject();
        JsonNode startupControl = canonical["SpriteFilterStartupControl"]!.DeepClone();
        canonical.Remove("SpriteFilterStartupControl");
        JsonObject? capabilities = Find(saved, "Battle", "Definition", "NativeGraphics", "PrimitiveCapabilities");
        if (capabilities is not null && !capabilities.ContainsKey("SpriteFilterStartupControl") && JsonNode.DeepEquals(capabilities, canonical))
        {
            capabilities["SpriteFilterStartupControl"] = startupControl;
        }
    }

    private static void CompleteGilgamesh(JsonObject? effects, JsonNode defaults)
    {
        if (effects?["Gilgamesh"] is JsonObject gilgamesh)
        {
            CompleteAbsent(gilgamesh, "ZantetsukenActors", defaults);
        }
    }

    private static void CompleteAbsent(JsonObject owner, string name, JsonNode defaults)
    {
        if (!owner.ContainsKey(name))
        {
            owner[name] = defaults.DeepClone();
        }
    }

    private static JsonObject? Find(JsonObject saved, params string[] path)
    {
        JsonObject current = saved;
        foreach (string name in path)
        {
            if (current[name] is not JsonObject child)
            {
                return null;
            }
            current = child;
        }
        return current;
    }
}
