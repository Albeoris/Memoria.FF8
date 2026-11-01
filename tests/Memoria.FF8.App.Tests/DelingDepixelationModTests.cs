using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DelingDepixelationModTests
{
    [Fact]
    public void Uniformed_characters_replace_background_pixels_without_changing_event_continuation()
    {
        string source = TestResources.FixturePath("App.Tests.DelingDepixelation.1");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        string path = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Depixelation", "fields", "gltown1.json");
        ModFile file = new("Memoria/Depixelation", ModFormat.Memoria, "fields/gltown1.json", 1, () => File.ReadAllBytes(path));
        Assert.True(FieldSceneMods.TryPrepare(new(1, [], [file]), out FieldSceneMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        FieldSceneMods active = mods;
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, readSceneMods: () => active);
        using GameWorld baseline = new(new(GameSession.Load(source)), TestResources.GameDirectory);
        Assert.True(world.EnsureField());
        Assert.True(baseline.EnsureField());
        Assert.Equal(761, world.Field!.Id);
        world.SetRenderSize(1280, 960);
        baseline.SetRenderSize(1280, 960);
        byte[] original = baseline.Render(TimeSpan.Zero).ToArray();
        byte[] replaced = world.Render(TimeSpan.Zero).ToArray();
        Assert.False(original.SequenceEqual(replaced));
        Assert.True(JsonElement.DeepEquals(baseline.Events.Capture(), world.Events.Capture()));
        FieldScenePatch patch = Assert.Single(mods.Patches).Patch;
        Assert.Equal(2, patch.Actors.Count);
        foreach (FieldActorPatch binding in patch.Actors)
        {
            ActorState actor = Assert.Single(world.Events.Actors, actor => actor.ObjectId == binding.ObjectId);
            ActorVisual visual = world.CreateVisual(actor);
            Assert.True(visual.Visible);
            Assert.Equal((actor.X, actor.Y, actor.Z), (visual.X, visual.Y, visual.Z));
            Assert.Equal(binding.Direction, visual.Direction);
            Assert.True(visual.ModelId >= world.Field.Models.Count);
        }
        Capture("original.png", original);
        Capture("depixelated.png", replaced);
        active = FieldSceneMods.Empty;
        Assert.Equal(original, world.Render(TimeSpan.Zero));
        active = mods;
        Assert.Equal(replaced, world.Render(TimeSpan.Zero));
        for (int callback = 0; callback < 30; callback++)
        {
            Assert.Equal(baseline.Apply(new(PlayerActionKind.Wait)), world.Apply(new(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(baseline.Events.Capture(), world.Events.Capture()));
        }
        Assert.Null(world.Events.LastFault);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(TestResources.ArtifactDirectory, "deling-depixelated.json");
        session.Save(snapshot);
        using GameWorld restored = new(new(GameSession.Load(snapshot)), TestResources.GameDirectory, readSceneMods: () => mods);
        Assert.True(restored.EnsureField());
        restored.SetRenderSize(1280, 960);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        if (OutputDirectory() is { } output)
        {
            session.SetEventState(world.Events.Capture());
            session.Save(Path.Combine(output, "continued.json"));
            File.WriteAllText(Path.Combine(output, "scene.json"), JsonSerializer.Serialize(world.ReadSceneInspection(), new JsonSerializerOptions { WriteIndented = true }));
        }

        void Capture(string name, byte[] pixels)
        {
            if (OutputDirectory() is { } output)
            {
                Directory.CreateDirectory(output);
                using var image = GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 1280, 960);
                image.Save(Path.Combine(output, name));
            }
        }
    }

    private static string? OutputDirectory() => Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
}
