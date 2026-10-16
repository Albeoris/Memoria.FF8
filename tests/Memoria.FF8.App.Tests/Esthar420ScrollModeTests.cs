using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Esthar420ScrollModeTests
{
    [Fact]
    public void DetachedProjectionPublishesAllEightPendingDirectRowsWithoutChangingSourceOrOtherPhases()
    {
        SceneEventState scene = new();
        for (int layer = 0; layer < 8; layer++)
        {
            scene.BackgroundScrollLayers[layer] = new(0, 0, 256, 256);
            scene.BackgroundScrollPositions[layer] = new(null, null, (short)(layer * 10), (short)-layer, null, null, null, 3, 0);
        }
        BackgroundScrollSettings changed = new(34, 0, 0, 0);
        string before = JsonSerializer.Serialize(scene);
        SceneEventState projection = scene.CreateBackgroundScrollProjection(2, changed);
        Assert.Equal(before, JsonSerializer.Serialize(scene));
        Assert.Equal(changed, projection.BackgroundScrollLayers[2]);
        Assert.NotSame(scene.BackgroundScrollLayers, projection.BackgroundScrollLayers);
        Assert.NotSame(scene.BackgroundScrollPositions, projection.BackgroundScrollPositions);
        for (int layer = 0; layer < 8; layer++)
        {
            BackgroundScrollPosition row = projection.BackgroundScrollPositions[layer];
            Assert.Equal((short)(layer * 10), row.X);
            Assert.Equal((short)-layer, row.Y);
            Assert.Equal((byte)2, row.Status);
            Assert.Equal((short)0, row.Counter);
            Assert.Null(row.PreviousX);
            Assert.Null(row.PreviousY);
        }
        BackgroundScrollPosition retained = new(12, 34, 56, 78, 1, 2, 3, 4, 1);
        scene.BackgroundScrollPositions[3] = retained;
        Assert.Same(retained, scene.CreateBackgroundScrollProjection(2, changed).BackgroundScrollPositions[3]);
    }

    [Fact]
    public void Actual420ScrollMode2AdmitsPendingNonselectedDirectPhaseAndRestores()
    {
        string root = AppContext.BaseDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.Esthar420ScrollModeTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Func<int, BackgroundScrollSettings, bool> original = world.Events.CanSetBackgroundScroll!;
        bool sawPendingDirect = false;
        world.Events.CanSetBackgroundScroll = (layer, settings) =>
        {
            JsonElement before = world.Events.Capture();
            bool supported = original(layer, settings);
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
            if (layer == 2 && world.Events.Scene.BackgroundScrollPositions.TryGetValue(1, out BackgroundScrollPosition? pending) && pending is { Mode: 3, Status: 0, X: null, Y: null })
            {
                sawPendingDirect = true;
                Assert.Equal(new BackgroundScrollSettings(34, 0, 0, 0), settings);
                Assert.True(supported);
                SceneEventState invalid = world.Events.Scene.CreateBackgroundScrollProjection(layer, settings);
                invalid.BackgroundScrollPositions[1] = pending with { TargetX = short.MaxValue };
                string held = JsonSerializer.Serialize(invalid);
                Assert.False(BackgroundScrollCertificate.CanSetForState(world.Field!, invalid, world.Events.Actors, layer, settings));
                Assert.Equal(held, JsonSerializer.Serialize(invalid));
            }
            return supported;
        };
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.True(sawPendingDirect);
        Assert.Null(world.Events.LastFault);
        Assert.Equal((short)312, world.Events.Scene.BackgroundScrollPositions[1].X);
        Assert.Equal((byte)2, world.Events.Scene.BackgroundScrollPositions[1].Status);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-scroll-mode2-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            world.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }
}
