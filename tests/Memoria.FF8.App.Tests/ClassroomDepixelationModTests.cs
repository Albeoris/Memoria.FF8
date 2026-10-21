using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ClassroomDepixelationModTests
{
    [Fact]
    public void Classroom_patch_removes_only_the_seated_student_and_preserves_saved_event_state()
    {
        string source = TestResources.FixturePath("App.Tests.ClassroomDepixelation.1");
        string game = TestResources.GameDirectory;
        string path = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Depixelation", "fields", "bgroom_1.json");
        ModFile file = new("Memoria/Depixelation", ModFormat.Memoria, "fields/bgroom_1.json", 1, () => File.ReadAllBytes(path));
        Assert.True(FieldSceneMods.TryPrepare(new(1, [], [file]), out FieldSceneMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        FieldBackgroundPatch patch = Assert.Single(Assert.Single(mods.Patches).Patch.Backgrounds);
        FieldBackgroundRegion region = Assert.IsType<FieldBackgroundRegion>(patch.Region);
        FieldSceneMods active = mods;
        using GameWorld world = new(new(GameSession.Load(source)), game, readSceneMods: () => active);
        using GameWorld baseline = new(new(GameSession.Load(source)), game);
        world.EnsureField();
        baseline.EnsureField();
        FieldAssets field = world.Field!;
        Assert.Equal(232, field.Id);
        Assert.Equal("bgroom_1", field.Name);
        BackgroundTile[] group = field.Background.Where(tile => tile.Layer == patch.Layer && tile.AnimationId == patch.ModuleId && tile.AnimationState == patch.State).ToArray();
        BackgroundTile[] selected = group.Where(tile => region.Contains(tile.X, tile.Y)).ToArray();
        Assert.Equal(8, selected.Length);
        Assert.Equal(17, group.Length - selected.Length);
        Assert.False(patch.Visible);
        Assert.Empty(Assert.Single(mods.Patches).Patch.Actors);
        Assert.True(JsonElement.DeepEquals(baseline.Events.Capture(), world.Events.Capture()));
        var inspection = world.ReadSceneInspection()!;
        Assert.Contains(inspection.Entries, entry => entry.Id == "layer:0:5:0" && entry.Details.Any(detail => detail.Contains("8 of 25 tiles", StringComparison.Ordinal)));

        // Exercise the background while its original animation group is selected and hidden.
        // The renderer-only selection does not change the saved scene or its scripts.
        foreach (int state in new[] { 0, -1 })
        {
            Dictionary<int, int> states = new(world.Events.BackgroundStates) { [patch.ModuleId] = state };
            FieldRenderer renderer = new(field);
            Dictionary<BackgroundTile, bool> visibility = selected.ToDictionary(tile => tile, _ => false);
            byte[] actual = renderer.Render(states, [], TimeSpan.Zero, backgroundTileVisibility: visibility).ToArray();
            FieldAssets expected = field with { Background = field.Background.Where(tile => !selected.Contains(tile)).ToArray() };
            Assert.Equal(new FieldRenderer(expected).Render(states, [], TimeSpan.Zero), actual);
            byte[] original = renderer.Render(states, [], TimeSpan.Zero).ToArray();
            Assert.Equal(state != 0, original.SequenceEqual(actual));
            Dictionary<FieldBackgroundKey, bool> inspect = new() { [new(patch.Layer, patch.ModuleId, patch.State)] = state == 0 };
            Assert.Equal(original, renderer.Render(states, [], TimeSpan.Zero, backgroundVisibility: inspect, backgroundTileVisibility: visibility));
        }

        active = FieldSceneMods.Empty;
        Assert.Equal(baseline.Render(TimeSpan.Zero), world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(baseline.Events.Capture(), world.Events.Capture()));
        Assert.Null(world.Events.LastFault);
    }
}
