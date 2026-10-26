using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Runtime.InteropServices;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NaturalWorldContinuationTests
{
    [Fact]
    public void ActualWalkingSaveReplaysTheNaturalWorldActionsAndMatchesLivePixels()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.NaturalWorldContinuationTests.1")));
        var resumedSession = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.NaturalWorldContinuationTests.1")));
        using var world = new GameWorld(session, directory);
        using var resumed = new GameWorld(resumedSession, directory);
        world.EnsureField();
        resumed.EnsureField();
        world.SetRenderSize(1280, 896);
        resumed.SetRenderSize(1280, 896);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(new WorldTransition(0, 0, -1), session.State.WorldArrival);
        Assert.Equal(4, session.State.World!.Frame);
        Assert.Equal((24666, -29382, 1, 32), (session.State.World.X, session.State.World.Z, session.State.World.AnimationId, session.State.World.AnimationCounter));
        JsonElement frozenVm = world.Events.Capture();
        Assert.Equal(634, world.Events.Frame);
        byte[] walkingPixels = world.Render(TimeSpan.Zero).ToArray();
        Assert.Equal(walkingPixels, resumed.Render(TimeSpan.Zero));
        Assert.Equal(ReadPixels(TestResources.FixturePath("App.Tests.NaturalWorldContinuationTests.2")), walkingPixels);
        PlayerAction[] actions = [new(PlayerActionKind.Wait), new(PlayerActionKind.Wait), new(PlayerActionKind.Move, Vertical: -1), new(PlayerActionKind.Move, Vertical: -1)];
        foreach (PlayerAction action in actions)
        {
            Assert.Equal(world.Apply(action), resumed.Apply(action));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(resumedSession.State)));
            Assert.True(JsonElement.DeepEquals(frozenVm, world.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(frozenVm, resumed.Events.Capture()));
        }
        GameState expected = GameSession.Load(TestResources.FixturePath("App.Tests.NaturalWorldContinuationTests.3"));
        Assert.True(JsonElement.DeepEquals(expected.DepartingFieldState!.Value, frozenVm));
        Assert.Null(session.State.DepartingFieldState);
        // Reload consumes this pending field-to-world transfer marker. The
        // complete departing VM remains restored and is checked separately.
        expected = expected with { DepartingFieldState = null };
        JsonElement expectedState = JsonSerializer.SerializeToElement(expected);
        JsonElement actualState = JsonSerializer.SerializeToElement(session.State);
        foreach (JsonProperty property in expectedState.EnumerateObject())
        {
            Assert.True(JsonElement.DeepEquals(property.Value, actualState.GetProperty(property.Name)), $"Restored world differs from live checkpoint property {property.Name}.");
        }
        Assert.Equal((24682, -29442, 8), (session.State.World.X, session.State.World.Z, session.State.World.Frame));
        byte[] finalPixels = world.Render(TimeSpan.Zero);
        Assert.Equal(finalPixels, resumed.Render(TimeSpan.Zero));
        Assert.Equal(ReadPixels(TestResources.FixturePath("App.Tests.NaturalWorldContinuationTests.4")), finalPixels);
        Assert.NotEqual(walkingPixels, finalPixels);
    }

    private static byte[] ReadPixels(string path)
    {
        using Bitmap image = Bitmap.Decode(path);
        Assert.Equal((1280, 896), (image.Width, image.Height));
        return PortableRaster.Rgba(image);
    }
}
