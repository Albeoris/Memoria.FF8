using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveParkedGardenTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DismountedGardenGameplaySaveRestoresCurrentFootPresentationAndContinues(bool alternateView)
    {
        GameSession session = WorldGardenCameraSceneTests.Fixture();
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 31)).Accepted);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        if (alternateView)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait, WorldButtons: 2)).Accepted);
        }
        GameplaySaveComponentDocument document = world.CaptureGameplaySave();
        string path = Path.Combine(TestResources.ArtifactDirectory, "parked-garden-" + Guid.NewGuid().ToString("N") + ".sav");
        GameState restored = world.WriteGameplaySave(path);
        Assert.True(File.Exists(path));
        NativeFootWorldRuntimeState foot = restored.World!.NativeFootRuntime!;
        NativeGardenWorldRuntimeState garden = restored.World.NativeGardenRuntime!;
        Assert.Equal(alternateView ? (byte)1 : (byte)0, foot.AnimationVariant);
        Assert.Equal(foot.AnimationVariant, garden.AnimationVariant);
        Assert.Equal(foot.MapDisplayMode, garden.MapDisplayMode);
        Assert.Equal(foot.GroundYaw, garden.GroundYaw);
        Assert.True(NativeWorldGroundCacheState.ContentEquals(foot.GroundCache, garden.GroundCache));
        Assert.True(NativeWorldForestState.ContentEquals(foot.Forest, garden.Forest));
        Assert.Equal(document.Resume.World!.X, restored.World.X);
        Assert.Equal(document.Resume.World.Z, restored.World.Z);
        Assert.Equal(document.Progression!.World!.GardenPose, GameplaySaveComponentDocument.CaptureProgression(restored, document.Resume).Progression!.World!.GardenPose);
        GameSession continued = new(GameplaySaveSessionLoader.Load(path, TestResources.GameDirectory));
        using GameWorld resumed = new(continued, TestResources.GameDirectory);
        resumed.EnsureField();
        Assert.True(resumed.Apply(new(PlayerActionKind.Wait)).Accepted);
        _ = new GameSession(continued.PrepareSnapshot());
    }
}

