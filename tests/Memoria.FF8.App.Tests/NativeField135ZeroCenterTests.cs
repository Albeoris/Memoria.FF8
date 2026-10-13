using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField135ZeroCenterTests
{
    [Fact]
    public void HistoricalRenderBoundaryRestoresZeroCenterAndRefusesMovementWithoutFreshDoorAuthority()
    {
        (string directory, string source) = Corpus();
        byte[] original = File.ReadAllBytes(source);
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        using var resumed = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        resumed.EnsureField();
        Assert.Equal(135, world.Events.FieldId);
        Assert.Equal(2, world.Events.Frame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        AssertZeroCenter(world);
        world.SetRenderSize(320, 240);
        resumed.SetRenderSize(320, 240);
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        FieldFixedPosition start = world.Events.Scene.NativePlayerPosition!;
        JsonElement before = world.Events.Capture();
        var action = new PlayerAction(PlayerActionKind.Move, Vertical: -1);
        NotSupportedException failure = Assert.Throws<NotSupportedException>(() => world.Apply(action));
        NotSupportedException replayFailure = Assert.Throws<NotSupportedException>(() => resumed.Apply(action));
        Assert.Contains("Legacy field135", failure.Message);
        Assert.Equal(failure.Message, replayFailure.Message);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        AssertZeroCenter(world);
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        Assert.Equal(start, world.Events.Scene.NativePlayerPosition);
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void Original135TileOriginsCannotEnterNativeWrappingOrBoundaryCullBranches()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        FieldAssets field = world.Field!;
        Assert.True(NativeField135ZeroCenterCertificate.Supports(field, 0));
        Assert.Equal(2, field.Information!.BackgroundWrapMask);
        Assert.Equal(110, field.Background.Count(tile => tile.Layer == 1));
        foreach (BackgroundTile tile in field.Background)
        {
            Assert.InRange(tile.X, -191, 175);
            Assert.InRange(tile.Y, -143, 127);
        }
        Assert.False(BackgroundScrollCertificate.CanSet(field, 1, new BackgroundScrollSettings(0, 0, 256, 256), 0));
    }

    [Fact]
    public void ChangedOriginalCameraBoundsAssetIdentityAndEnabledWrapTileBranchRemainRefused()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        FieldAssets field = world.Field!;
        byte[] jsm = (byte[])field.Jsm.Clone();
        jsm[^1] ^= 1;
        FieldBounds[] bounds = field.Information!.CameraBounds.ToArray();
        bounds[0] = bounds[0] with { Right = 161 };
        BackgroundTile[] tiles = field.Background.ToArray();
        int wrapped = Array.FindIndex(tiles, tile => tile.Layer == 1);
        tiles[wrapped] = tiles[wrapped] with { X = -193 };
        foreach (FieldAssets altered in new[]
        {
            field with { Jsm = jsm },
            field with { Id = 134 },
            field with { Information = field.Information with { CameraBounds = bounds } },
            field with { Information = field.Information with { BackgroundWrapMask = 3 } },
            field with { Information = field.Information with { CameraHeight = 201 } },
            field with { Background = tiles },
            field with { Camera = field.Camera with { Translation = field.Camera.Translation + Vector3.UnitX } }
        })
        {
            Assert.False(NativeField135ZeroCenterCertificate.Supports(altered, 0));
            Assert.Throws<NotSupportedException>(() => NormalCameraCertificate.Prepare(altered, world.Events.Scene, world.Events.Actors));
        }
        Assert.False(NativeField135ZeroCenterCertificate.Supports(field, 1));
    }

    [Fact]
    public void AnyScriptedScrollStateStillRefusesTheReachedZeroCenterBaseline()
    {
        (string directory, string source) = Corpus();
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        world.Events.Scene.BackgroundScrollLayers[1] = new BackgroundScrollSettings(0, 0, 256, 256);
        Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
        world.Events.Scene.BackgroundScrollLayers.Clear();
        world.Events.Scene.BackgroundScrollPositions[1] = new BackgroundScrollPosition(0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
    }

    private static void AssertZeroCenter(GameWorld world)
    {
        BackgroundScrollFrame frame = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(Vector2.Zero, frame.ActorOffset);
        Assert.Empty(frame.LayerOffsets);
    }

    private static (string Directory, string Source) Corpus()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return (directory, Path.Combine(repository!.FullName, "../fixtures/external-fixture.json"));
    }
}
