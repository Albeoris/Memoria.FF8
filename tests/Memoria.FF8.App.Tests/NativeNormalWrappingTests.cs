using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeNormalWrappingTests
{
    [Fact]
    public void FreshIfritArrivalUsesVerifiedNormalCenterAndTwoWrappingLayersWithIndependentSaveAndPixels()
    {
        (string directory, string evidence) = Corpus();
        string source = Path.Combine(evidence, "player-ifrit-arrival-checkpoint.json");
        byte[] original = File.ReadAllBytes(source);
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Equal(130, world.Events.FieldId);
        Assert.Equal(3, world.Events.Frame);
        Assert.NotNull(world.Events.Scene.NativeFieldInitialization);
        Assert.Equal("Completed", world.Events.Scene.NativeFieldInitialization.Stage.ToString());
        Assert.True(NormalBackgroundWrapCertificate.Supports(world.Field!, world.Events.Scene));
        BackgroundScrollFrame frame = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(new Vector2(-6, -8), frame.ActorOffset);
        Assert.Equal(new[] { 1, 2 }, frame.NativeWrap!.Layers.Select(layer => (int)layer.Layer));
        Assert.Equal(1, frame.NativeWrap.NativeNormalVersion);
        Assert.Equal((-186, 182, -136, 136), ((int)frame.NativeWrap.Left, (int)frame.NativeWrap.Right, (int)frame.NativeWrap.Top, (int)frame.NativeWrap.Bottom));
        Assert.All(frame.NativeWrap.Layers, layer => Assert.Equal((0, 0, 368, 144), ((int)layer.OffsetX, (int)layer.OffsetY, (int)layer.PeriodX, (int)layer.PeriodY)));
        Assert.All(frame.LayerOffsets.Values, offset => Assert.Equal(frame.ActorOffset, offset));
        world.SetRenderSize(320, 240);
        session.SetEventState(world.Events.Capture());
        string save = Path.Combine(Path.GetTempPath(), "Memoria.FF8-normal-wrap-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(save);
            using var resumed = new GameWorld(new GameSession(GameSession.Load(save)), directory);
            resumed.EnsureField();
            resumed.SetRenderSize(320, 240);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            FieldFixedPosition before = world.Events.Scene.NativePlayerPosition!;
            for (int tick = 0; tick < 3; tick++)
            {
                var action = new PlayerAction(PlayerActionKind.Move, Vertical: -1);
                PlayerActionResult? result = null;
                PlayerActionResult? repeat = null;
                Exception? failure = Record.Exception(() => result = world.Apply(action));
                Exception? repeatedFailure = Record.Exception(() => repeat = resumed.Apply(action));
                Assert.Equal(result, repeat);
                Assert.Equal(failure?.GetType(), repeatedFailure?.GetType());
                Assert.Equal(failure?.Message, repeatedFailure?.Message);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
                Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
                if (failure is not null)
                {
                    Assert.IsType<EventScriptException>(failure);
                    break;
                }
                Assert.True(result!.Accepted);
            }
            Assert.NotEqual(before, world.Events.Scene.NativePlayerPosition);
        }
        finally
        {
            File.Delete(save);
        }
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void HistoricalIfritInitializationWithoutRuntimeReceiptRefusesRatherThanAcquiringFreshAuthority()
    {
        (string directory, string evidence) = Corpus();
        string source = Path.Combine(evidence, "player-ifrit-render-boundary-checkpoint.json");
        byte[] original = File.ReadAllBytes(source);
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        Assert.Equal(3, world.Events.Frame);
        Assert.Null(world.Events.Scene.NativeFieldInitialization);
        JsonElement before = world.Events.Capture();
        Assert.False(NormalBackgroundWrapCertificate.Supports(world.Field!, world.Events.Scene));
        Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void GenericNormalWrappingRejectsUnknownRuntimeAndScriptedStateWithoutFieldMatrixWhitelists()
    {
        (string directory, string evidence) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(Path.Combine(evidence, "player-ifrit-arrival-checkpoint.json"))), directory);
        world.EnsureField();
        while (world.Events.Scene.NativeFieldLoadVersion == 0)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        FieldAssets field = world.Field!;
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field with { NativeExecutableHash = null }, 0));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field with { NativeExecutableHash = "unverified" }, 0));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field, 1));
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field with { Camera = field.Camera with { Pan = Vector2.One } }, 0));
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[1] = bounds[1] with { Right = 0 };
        Assert.False(FieldNativeNormalBackgroundWrap.SupportsMetadata(field with { Information = field.Information with { CameraBounds = bounds } }, 0));
        world.Events.Scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(0, 0, 256, 256);
        Assert.False(NormalBackgroundWrapCertificate.Supports(field, world.Events.Scene));
        Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
        world.Events.Scene.BackgroundScrollLayers.Clear();
        world.Events.Scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 3, 2);
        Assert.False(NormalBackgroundWrapCertificate.Supports(field, world.Events.Scene));
        Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
    }

    private static (string Directory, string Evidence) Corpus()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        return (directory, Path.Combine(repository!.FullName, "../fixtures"));
    }
}
