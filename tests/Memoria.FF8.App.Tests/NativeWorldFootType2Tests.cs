using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldFootType2Tests
{
    [Fact]
    public void ActualType2EstharReturnPublishesFootSaveAndFreshFieldByte86()
    {
        string path = TestResources.FixturePath("App.Tests.NativeWorldFootType2Tests.2");
        GameSession session = new(GameSession.Load(path));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.0873837553920483f, Vertical: -0.9961747232757828f, Frames: 2)).Accepted);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Null(session.State.World);
        Assert.Equal((byte)2, session.State.WorldConfiguration!.EntityByte);
        Assert.Equal((byte)2, session.State.SavedWorld!.FootReturn!.EntityType105);
        Assert.Equal((byte)2, session.State.DepartingWorldState!.NativeFootRuntime!.EntityType);
        session.SetDepartingFieldState(world.Events.Capture());
        GameSession coldSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld cold = new(coldSession, TestResources.GameDirectory);
        world.EnsureField();
        cold.EnsureField();
        Assert.Equal((byte)2, world.Events.Scene.PreviousWorldEntityByte86);
        Assert.Equal((byte)2, cold.Events.Scene.PreviousWorldEntityByte86);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 2)).Accepted);
        Assert.True(cold.Apply(new(PlayerActionKind.Wait, Frames: 2)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
        session.SetEventState(world.Events.Capture());
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    [Fact]
    public void OriginalEntity2SelectsShape5AndItsActualFiveAnimationBank()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        Assert.Equal((byte)5, WorldAssets.NativeGardenEntityShape(2));
        Assert.Equal(2, NativeTrainWorldObject.ClassifyShape(5));
        Assert.Equal(new[] { 1, 20, 30, 15, 14 }, assets.GetFootModel(2).Animations.Select(animation => animation.Frames.Count));
        Assert.Equal((byte)0x80, NativeWorldTerrain.PermissionMask(2));
        Assert.Equal(32, NativeWorldTerrain.ProbeRadius(2));
    }

    [Fact]
    public void Original545Type2InitializesItsActualShape5ClauseAndRefusesAnUnknownRetainedDrawOwner()
    {
        GameSession session = new(GameSession.Load(Source()));
        Assert.Null(session.State.WorldProcess!.ForcedAnimationC76628);
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.15382f, Vertical: 0.9881f)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        NativeFootWorldRuntimeState foot = session.State.World!.NativeFootRuntime!;
        Assert.Equal((byte)2, foot.EntityType);
        Assert.Null(session.State.World.NativeGardenRuntime);
        Assert.Equal(new byte[] { 5, 89 }, foot.NormalObjects!.Allocation.Objects.Select(item => item.ShapeId));
        Assert.Equal((12, 2140, 2500), (foot.NormalObjects.Allocation.ClauseIndex, (int)foot.NormalObjects.Allocation.MinimumStory, (int)foot.NormalObjects.Allocation.MaximumStory));
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => world.Apply(new(PlayerActionKind.Wait)));
        Assert.Contains("forced-animation owner", error.Message);
    }

    [Fact]
    public void ExplicitRetained1886OwnerSupportsActual545EntryMovementIdleAndColdRestore()
    {
        GameState source = GameSession.Load(Source());
        //Component copy of the one-property migration authorized separately by root.
        //The original file is untouched; these are the actual retained proof owners.
        WorldState prior = source.DepartingWorldState!;
        Assert.Equal(1886, prior.Frame);
        Assert.Equal((byte)0, prior.NativeFootRuntime!.EntityType);
        Assert.Equal(0u, prior.NativeGardenRuntime!.Buttons.LastObservedButtons);
        Assert.Equal(0, prior.NativeGardenRuntime.TransferControls.TransitionPhase);
        Assert.Equal(-1, prior.NativeGardenRuntime.TransferControls.ActiveWorldScript);
        GameSession session = new(source with { WorldProcess = source.WorldProcess! with { ForcedAnimationC76628 = -1 } });
        using GameWorld world = new(session, TestResources.GameDirectory);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -0.15382f, Vertical: 0.9881f)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldState before = session.State.World!;
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        Assert.NotEqual((before.X, before.Z), (session.State.World!.X, session.State.World.Z));
        Assert.Equal((byte)2, session.State.World.NativeFootRuntime!.EntityType);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Equal(-1, session.State.WorldProcess!.ForcedAnimationC76628);
        session.SetDepartingFieldState(world.Events.Capture());
        GameSession coldSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld cold = new(coldSession, TestResources.GameDirectory);
        cold.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(cold.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(coldSession.State)));
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(session.State.World, 545, 2145, 0);
        Assert.Equal((byte)2, receipt.EntityType105);
        Assert.Equal((byte)2, NativeFootWorldRuntimeState.CreateSavedReload(receipt).EntityType);
        GameState returnedField = session.State with { Scene = SceneKind.Field, FieldId = 545, World = null, WorldArrival = null, SavedWorld = session.State.SavedWorld!.WithFootReturn(receipt), DepartingWorldState = session.State.World };
        using PreparedWorldEntry reload = WorldEntryPreflight.Prepare(returnedField, TestResources.GameDirectory, new(62, 2, -1));
        Assert.Equal((byte)2, reload.InitialState.NativeFootRuntime!.EntityType);
        Assert.Equal((receipt.Pose0.X, receipt.Pose0.RawZ, (int)receipt.Pose0.Height, (int)receipt.Pose0.Yaw), (reload.InitialState.X, reload.InitialState.Z, reload.InitialState.NativeHeight, reload.InitialState.BodyYaw));
    }

    private static string Source()
    {
        string root = AppContext.BaseDirectory;
        return TestResources.FixturePath("App.Tests.NativeWorldFootType2Tests.1");
    }
}
