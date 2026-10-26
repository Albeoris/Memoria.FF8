using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotFileStagingMigrationTests
{
    [Fact]
    public void Historical_byte_ranges_keep_their_absolute_addresses_in_every_owner()
    {
        JsonObject saved = new();
        List<JsonObject> stagingOwners = [];
        foreach (string[] path in OwnerPaths())
        {
            JsonObject staging = new()
            {
                ["KnownRanges"] = new JsonArray(new JsonObject { ["Offset"] = 12, ["Data"] = new JsonArray(7, 8, 9) })
            };
            Set(saved, path, staging);
            stagingOwners.Add(staging);
        }

        SnapshotFileStagingMigration.Apply(saved);

        foreach (JsonObject staging in stagingOwners)
        {
            NativeBattleFileStaging restored = staging.Deserialize<NativeBattleFileStaging>()!;
            restored.Validate();
            Assert.Equal(NativeBattleFileStaging.FromColdProcess().BaseAddress, restored.BaseAddress);
            Assert.Equal(new byte[] { 7, 8, 9 }, restored.Read(NativeBattleFileStaging.Address + 12, 3));
            Assert.Equal(NativeBattleFileStaging.FromColdProcess().LoaderZeroRanges.ToArray(), restored.LoaderZeroRanges.ToArray());
            Assert.Equal(new byte[] { 0 }, restored.Read(0x188C000, 1));
            Assert.Equal(12 + (int)(NativeBattleFileStaging.Address - restored.BaseAddress), staging["KnownRanges"]![0]!["Offset"]!.GetValue<int>());
        }
        JsonNode beforeRepeat = saved.DeepClone();
        SnapshotFileStagingMigration.Apply(saved);
        Assert.True(JsonNode.DeepEquals(beforeRepeat, saved));
    }

    [Fact]
    public void Explicit_coordinates_zero_ranges_and_nulls_are_preserved()
    {
        JsonObject saved = new();
        Set(saved, OwnerPaths()[0], new JsonObject
        {
            ["BaseAddress"] = 0x188C000u,
            ["LoaderZeroRanges"] = new JsonArray(new JsonObject { ["Address"] = 0x188C000u, ["Length"] = 32 }),
            ["KnownRanges"] = new JsonArray()
        });
        Set(saved, OwnerPaths()[1], new JsonObject { ["BaseAddress"] = null, ["LoaderZeroRanges"] = null });
        JsonNode before = saved.DeepClone();

        SnapshotFileStagingMigration.Apply(saved);

        Assert.True(JsonNode.DeepEquals(before, saved));
    }

    [Fact]
    public void Absent_staging_is_left_for_whole_component_initialization()
    {
        JsonObject saved = new() { ["NativeBattleMemory"] = new JsonObject(), ["Battle"] = null };
        JsonNode before = saved.DeepClone();

        SnapshotFileStagingMigration.Apply(saved);

        Assert.True(JsonNode.DeepEquals(before, saved));
    }

    [Fact]
    public void Existing_empty_loader_regions_keep_the_original_missing_origin()
    {
        JsonObject staging = new() { ["KnownRanges"] = new JsonArray(), ["LoaderZeroRanges"] = new JsonArray() };
        JsonObject saved = new();
        Set(saved, OwnerPaths()[0], staging);

        SnapshotFileStagingMigration.Apply(saved);

        Assert.Equal(NativeBattleFileStaging.Address, staging["BaseAddress"]!.GetValue<uint>());
        Assert.Empty(staging["LoaderZeroRanges"]!.AsArray());
    }

    private static string[][] OwnerPaths() =>
    [
        ["NativeBattleMemory", "FileStaging"],
        ["Battle", "NativeFileStaging"],
        ["Battle", "Definition", "NativeRetainedMemory", "FileStaging"],
        ["EventState", "Scene", "NativeEyeProcess", "Memory", "FileStaging"],
        ["DepartingFieldState", "Scene", "NativeEyeProcess", "Memory", "FileStaging"]
    ];

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
