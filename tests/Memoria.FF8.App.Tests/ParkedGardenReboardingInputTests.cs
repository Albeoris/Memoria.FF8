using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ParkedGardenReboardingInputTests
{
    [Fact]
    public void AlternateFootCameraBoardsAndRestoresTheInProgressGardenCamera()
    {
        string source = TestResources.FixturePath("App.Tests.ParkedGardenReboardingInputTests.2");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.Equal(640, session.State.World!.NativeFootRuntime!.CameraTransition!.ProjectionDistance);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(session.State.World.NativeFootRuntime);
        NativeGardenWorldRuntimeState garden = session.State.World.NativeGardenRuntime!;
        Assert.Equal(17, garden.TransferControls.TransitionPhase);
        Assert.Equal(640, garden.ProjectionCamera!.ProjectionDistance);
        Assert.Equal(648, garden.ReboardCamera!.ProjectionDistance);
        Assert.Equal(garden.ProjectionCamera.Pitch + 16, garden.CameraPitch);
        Assert.NotEmpty(world.WorldScene!.Render(320, 224));

        string snapshot = TestResources.ArtifactFile("garden-camera-transition.ff8save");
        session.Save(snapshot, compact: true);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        for (int focal = 648; focal < 1024; focal += 8)
        {
            PlayerAction action = new(focal == 656 ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            Assert.True(restored.Apply(action).Accepted);
            Assert.Null(session.State.World!.NativeFootRuntime);
            garden = session.State.World.NativeGardenRuntime!;
            Assert.Null(garden.Transfer);
            Assert.Equal(focal, garden.ProjectionCamera!.ProjectionDistance);
            Assert.Equal(focal + 8, garden.ReboardCamera?.ProjectionDistance ?? 1024);
        }
        Assert.Equal(0, garden.TransferControls.TransitionPhase);
        Assert.Null(garden.ReboardCamera);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        garden = session.State.World!.NativeGardenRuntime!;
        Assert.Equal(1024, garden.ProjectionCamera!.ProjectionDistance);
        Assert.Equal(garden.CameraDefaults.Pitch, garden.CameraPitch);
        Assert.NotEmpty(restored.WorldScene!.Render(320, 224));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
    }

    [Fact]
    public void OrdinaryFootCameraBoardsOnceTheFootCameraTransitionFinishes()
    {
        string source = TestResources.FixturePath("App.Tests.ParkedGardenReboardingInputTests.2");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, WorldButtons: 2)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 46)).Accepted);
        Assert.Equal(new NativeFootCameraTransition(0, 1024), session.State.World.NativeFootRuntime.CameraTransition);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(session.State.World.NativeFootRuntime);
        NativeGardenWorldRuntimeState garden = session.State.World.NativeGardenRuntime!;
        Assert.Equal(16, garden.TransferControls.TransitionPhase);
        Assert.Equal(1024, garden.ReboardCamera!.ProjectionDistance);
        Assert.Equal(1024, garden.ProjectionCamera!.ProjectionDistance);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        garden = session.State.World!.NativeGardenRuntime!;
        Assert.Equal(0, garden.TransferControls.TransitionPhase);
        Assert.Null(garden.ReboardCamera);
    }

    [Fact]
    public void ActualCentraFootConfirmPublishesBoardingActivityBeforeWorldEventsAndRestores()
    {
        string source = TestResources.FixturePath("App.Tests.ParkedGardenReboardingInputTests.1");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.NotNull(session.State.World!.NativeFootRuntime);
        Assert.Equal(2, session.State.World.NativeGardenRuntime!.BroadContact);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(session.State.World!.NativeFootRuntime);
        Assert.NotNull(session.State.World.NativeGardenRuntime);
        string snapshot = Path.Combine(Path.GetTempPath(), "Memoria-FF8-Garden-reboard-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static string FindRootFile(string relative)
    {
        return TestResources.FixtureFile(relative);
    }
}
