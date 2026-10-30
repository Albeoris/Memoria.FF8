using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalSaveExportPlaceTests
{
    [Fact]
    public void Field_preview_keeps_its_declared_place_without_loading_world_resources()
    {
        GameState state = new() { Scene = SceneKind.Field };
        Assert.Null(OriginalSaveExportPlace.Read(state, "missing-game-resources"));
    }

    [Fact]
    public void World_preview_requires_the_exported_snapshot_coordinates()
    {
        Assert.Throws<InvalidDataException>(() => OriginalSaveExportPlace.Read(new() { Scene = SceneKind.WorldMap }, "missing-game-resources"));
    }

    [Fact]
    public void Installed_world_preview_uses_candidate_coordinates_and_preserves_scene_snapshot()
    {
        string gameDirectory = LaunchOptions.ConfiguredGameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed world resources are required.");
        GameState candidate = new()
        {
            Scene = SceneKind.WorldMap,
            World = new() { X = 12390, Z = -26709 },
            EventState = JsonSerializer.SerializeToElement(new { Scene = new { PlaceId = 999 } })
        };
        string scene = candidate.EventState!.Value.GetRawText();
        using WorldAssets assets = new(gameDirectory);
        short expected = assets.GetNativeMapLocationId(candidate.World.X, candidate.World.Z);
        Assert.Equal(expected, OriginalSaveExportPlace.Read(candidate, gameDirectory));
        Assert.Equal(scene, candidate.EventState.Value.GetRawText());
        Assert.Equal(999, candidate.EventState.Value.GetProperty("Scene").GetProperty("PlaceId").GetInt32());
        Assert.Equal(12390, candidate.World.X);
        Assert.Equal(-26709, candidate.World.Z);
    }
}
