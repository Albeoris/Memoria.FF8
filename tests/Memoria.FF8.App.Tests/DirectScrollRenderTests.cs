using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DirectScrollRenderTests
{
    [Fact]
    public void ReachedDirectScrollUsesSharedDrawOffsetAndRestoresTheSameFrame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        var original = world.Events.Actors.Select(actor => (actor.X, actor.Y, actor.Z)).ToArray();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(4, world.Events.Frame);
        BackgroundScrollPosition position = world.Events.Scene.BackgroundScrollPositions[0];
        Assert.Equal(new BackgroundScrollPosition(0, 224, 0, 224, 0, 0, 0, 3, 2), position);
        Assert.Equal(original, world.Events.Actors.Select(actor => (actor.X, actor.Y, actor.Z)).ToArray());
        BackgroundScrollFrame scroll = BackgroundScrollCertificate.Prepare(world.Field!, world.Events.Scene);
        Assert.Equal(new Vector2(0, -224), scroll.ActorOffset);
        Assert.Equal(Vector2.Zero, scroll.LayerOffsets[1]);
        ActorState player = world.Events.Actors[0];
        var visual = new ActorVisual(player.ObjectId, player.ModelId, player.X, player.Y, player.Z, player.Direction, player.AnimationId, player.AnimationFrame, true, PolygonColor: player.PolygonColor);
        var isolatedRenderer = new FieldRenderer(world.Field!);
        byte[] unshiftedBody = isolatedRenderer.Render(world.Events.BackgroundStates, [visual], TimeSpan.Zero, backgroundVisible: false).ToArray();
        Assert.Contains(unshiftedBody.Where((_, index) => index % 4 != 3), value => value != 0);
        byte[] shiftedBody = isolatedRenderer.Render(world.Events.BackgroundStates, [visual], TimeSpan.Zero, backgroundVisible: false, actorScrollOffset: scroll.ActorOffset).ToArray();
        Assert.True(shiftedBody.Where((_, index) => index % 4 != 3).All(value => value == 0));
        byte[] pixels = world.Render(TimeSpan.Zero).ToArray();
        GameState captured = session.State with { EventState = world.Events.Capture() };
        using var resumed = new GameWorld(new GameSession(captured), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(pixels, resumed.Render(TimeSpan.Zero));
    }

    [Fact]
    public void LayerTranslationPreservesTexelsClipsAndReusesTheOriginalRasterAtNativeAndTripleSize()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using var assets = new GameAssets(directory);
        FieldAssets original = assets.LoadField(172);
        BackgroundTile tile = original.Background.First(tile => tile.Image.Pixels.Any(pixel => pixel.A != 0)) with { X = -16, Y = -16, Layer = 1, AnimationId = 255, Blend = false, Draw = true };
        FieldAssets field = original with { Background = new[] { tile } };
        foreach (int multiplier in new[] { 1, 3 })
        {
            var renderer = new FieldRenderer(field, 320 * multiplier, 240 * multiplier);
            byte[] baseline = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            byte[] translated = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(32, 24) }).ToArray();
            for (int y = 104 * multiplier; y < 120 * multiplier; y++)
            {
                for (int x = 144 * multiplier; x < 160 * multiplier; x++)
                {
                    int source = (y * 320 * multiplier + x) * 4;
                    int target = ((y + 24 * multiplier) * 320 * multiplier + x + 32 * multiplier) * 4;
                    Assert.Equal(baseline.AsSpan(source, 4).ToArray(), translated.AsSpan(target, 4).ToArray());
                }
            }
            byte[] clipped = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [1] = new(0, -224) }).ToArray();
            Assert.True(clipped.Where((_, index) => index % 4 != 3).All(value => value == 0));
            Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = new(32, 24) }));
            Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero));
        }
    }
}
