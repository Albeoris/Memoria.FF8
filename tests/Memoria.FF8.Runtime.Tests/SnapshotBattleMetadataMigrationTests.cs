using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotBattleMetadataMigrationTests
{
    [Fact]
    public void Missing_new_metadata_is_initialized_in_retained_copies_without_changing_active_progress()
    {
        JsonObject saved = new();
        foreach (string[] path in RetainedPaths())
        {
            Set(saved, path, new JsonObject
            {
                ["ModelTransformRows"] = new JsonArray(new JsonObject { ["Rotation12"] = 77 }, null, new JsonObject { ["Flags2"] = null, ["GeometryBounds52"] = null }),
                ["EffectMemory"] = Effects()
            });
        }
        Set(saved, ["Battle", "NativeEffectMemory"], Effects());
        Set(saved, ["Battle", "NativeGraphics"], new JsonObject { ["Models"] = new JsonArray(new JsonObject { ["Flags2"] = 4, ["SequenceFrame"] = 23 }) });
        JsonNode graphics = saved["Battle"]!["NativeGraphics"]!.DeepClone();

        SnapshotBattleMetadataMigration.Apply(saved, new JsonSerializerOptions());

        foreach (string[] path in RetainedPaths())
        {
            JsonObject retained = Get(saved, path);
            JsonArray rows = retained["ModelTransformRows"]!.AsArray();
            Assert.Equal(3, rows.Count);
            Assert.Equal(77, rows[0]!["Rotation12"]!.GetValue<int>());
            Assert.Equal((ushort)0, rows[0]!["Flags2"]!.GetValue<ushort>());
            Assert.NotNull(rows[0]!["GeometryBounds52"]);
            Assert.Null(rows[1]);
            Assert.Null(rows[2]!["Flags2"]);
            Assert.Null(rows[2]!["GeometryBounds52"]);
            Assert.Equal(29, retained["EffectMemory"]!["Gilgamesh"]!["Entry"]!["Frame"]!.GetValue<int>());
            Assert.NotNull(retained["EffectMemory"]!["Gilgamesh"]!["ZantetsukenActors"]);
        }
        Assert.NotNull(saved["Battle"]!["NativeEffectMemory"]!["Gilgamesh"]!["ZantetsukenActors"]);
        Assert.True(JsonNode.DeepEquals(graphics, saved["Battle"]!["NativeGraphics"]));
        JsonNode beforeRepeat = saved.DeepClone();
        SnapshotBattleMetadataMigration.Apply(saved, new JsonSerializerOptions());
        Assert.True(JsonNode.DeepEquals(beforeRepeat, saved));
    }

    [Fact]
    public void Only_the_matching_primary_renderer_profile_receives_its_new_startup_value()
    {
        JsonObject canonical = JsonSerializer.SerializeToNode(NativeBattlePrimitiveCapabilities.PrimaryRgba8())!.AsObject();
        canonical.Remove("SpriteFilterStartupControl");
        JsonObject saved = new();
        Set(saved, ["Battle", "Definition", "NativeGraphics", "PrimitiveCapabilities"], canonical);

        SnapshotBattleMetadataMigration.Apply(saved, new JsonSerializerOptions());

        Assert.Equal(2u, canonical["SpriteFilterStartupControl"]!.GetValue<uint>());
        canonical["SpriteFilterStartupControl"] = null;
        SnapshotBattleMetadataMigration.Apply(saved, new JsonSerializerOptions());
        Assert.Null(canonical["SpriteFilterStartupControl"]);
        canonical.Remove("SpriteFilterStartupControl");
        canonical["HardwareEnabled"] = false;
        SnapshotBattleMetadataMigration.Apply(saved, new JsonSerializerOptions());
        Assert.False(canonical.ContainsKey("SpriteFilterStartupControl"));
    }

    [Fact]
    public void Existing_null_independent_owner_is_preserved()
    {
        JsonObject saved = new();
        JsonObject effects = Effects();
        effects["Gilgamesh"]!["ZantetsukenActors"] = null;
        Set(saved, ["Battle", "NativeEffectMemory"], effects);
        JsonNode original = saved.DeepClone();

        SnapshotBattleMetadataMigration.Apply(saved, new JsonSerializerOptions());

        Assert.True(JsonNode.DeepEquals(original, saved));
    }

    private static JsonObject Effects() => new() { ["Gilgamesh"] = new JsonObject { ["Entry"] = new JsonObject { ["Frame"] = 29 } } };

    private static string[][] RetainedPaths() =>
    [
        ["NativeBattleMemory"],
        ["Battle", "Definition", "NativeRetainedMemory"],
        ["EventState", "Scene", "NativeEyeProcess", "Memory"],
        ["DepartingFieldState", "Scene", "NativeEyeProcess", "Memory"]
    ];

    private static JsonObject Get(JsonObject saved, string[] path)
    {
        JsonObject current = saved;
        foreach (string name in path)
        {
            current = current[name]!.AsObject();
        }
        return current;
    }

    private static void Set(JsonObject saved, string[] path, JsonObject value)
    {
        JsonObject current = saved;
        foreach (string name in path[..^1])
        {
            if (current[name] is not JsonObject child)
            {
                child = new JsonObject();
                current[name] = child;
            }
            current = child;
        }
        current[path[^1]] = value;
    }
}
