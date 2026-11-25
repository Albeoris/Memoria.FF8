using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

/// <summary>Actual original-resource callbacks from a declared component foundation, not a Ragnarok acquisition playthrough.</summary>
public sealed class NativeRagnarokFootTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void LandedFootAlternateViewTransitionsAndRestoresBesideTheActualParkedShip()
    {
        GameSession session = Fixture(3600);
        using WorldMapScene scene = new(session, GameDirectory);
        Land(scene);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 2)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeFootRuntime!.AnimationVariant);
        Assert.True(scene.State.NativeFootRuntime.CameraTransition!.ProjectionDistance < 1024);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, CameraHorizontal: 1, Frames: 4)).Accepted);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, GameDirectory);
        PlayerAction next = new(PlayerActionKind.Wait, CameraHorizontal: -1);
        Assert.True(scene.Apply(next).Accepted);
        Assert.True(replay.Apply(next).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.True(replay.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.Equal(0, scene.State.NativeFootRuntime.CameraTransition.Flags);
        Assert.Equal(640, scene.State.NativeFootRuntime.CameraTransition.ProjectionDistance);
        ApproachShip(scene);
        ApproachShip(replay);
        Assert.True(scene.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.True(replay.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((byte)0, scene.State.NativeRagnarokRuntime!.AnimationVariant);
        Assert.Equal(1, scene.State.NativeRagnarokRuntime.Transfer!.Counter);
        Assert.Equal(640, scene.State.NativeRagnarokRuntime.Transfer.InitialCamera.ProjectionDistance);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.True(replay.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Null(scene.State.NativeRagnarokRuntime.Transfer);
        Assert.Equal((byte)0, scene.State.NativeRagnarokRuntime.AnimationVariant);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        GameSession activeRestored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene activeReplay = new(activeRestored, GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(activeReplay.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal((byte)0, activeReplay.State.NativeRagnarokRuntime!.AnimationVariant);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(activeRestored.State)));
    }

    [Fact]
    public void OriginalLandingHandsOffToCurrentFootControllerAndRestoresIndependently()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, GameDirectory);
        Land(scene);
        NativeRagnarokWorldRuntimeState landed = scene.State.NativeRagnarokRuntime!;
        NativeRagnarokWorldObject ship = landed.Objects[landed.Allocation.RagnarokIndex];
        Assert.Equal((byte)0, scene.State.NativeFootRuntime!.EntityType);
        Assert.Equal(61, scene.State.NativeFootRuntime.RagnarokDismount!.Landing.Counter);
        Assert.Equal((short)-7077, scene.State.NativeFootRuntime.CameraOffsetZ);
        Assert.Equal(5229, landed.TransferCamera!.CurveThreshold);
        Assert.NotEqual(ship.Position, new NativeWorldPathPosition(scene.State.X, scene.State.NativeHeight, -scene.State.Z));
        NativeWorldRagnarokDismountReceipt receipt = scene.State.NativeFootRuntime.RagnarokDismount!;
        Assert.Throws<InvalidDataException>(() => (receipt with { Camera = receipt.Camera with { CurveThreshold = 5216 } }).Validate(landed));
        Assert.Throws<InvalidDataException>(() => receipt.Validate(landed with { Objects = landed.Objects.SetItem(landed.Allocation.RagnarokIndex, ship with { Position = ship.Position with { X = ship.Position.X + 1 } }) }));
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-ragnarok-foot-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using WorldMapScene restored = new(restoredSession, GameDirectory);
            scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1, Frames: 4));
            restored.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1, Frames: 4));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            Assert.Equal(ship, scene.State.NativeRagnarokRuntime!.Objects[landed.Allocation.RagnarokIndex]);
            NativeSavedWorldFootReturn footReturn = NativeSavedWorldFootReturn.Create(scene.State, 257, 3500, 0, 0, 0);
            NativeSavedWorldState saved = session.State.SavedWorld!.WithRagnarokParking(scene.State.NativeRagnarokRuntime).WithFootReturn(footReturn);
            Assert.Equal(footReturn.Pose0, saved.FootReturn!.Pose0);
            Assert.Equal(new NativeSavedWorldPose(ship.Position.X, -ship.Position.Z, checked((short)ship.Position.Y), checked((ushort)ship.Yaw)), saved.RagnarokPose2);
            Assert.NotEqual(saved.RagnarokPose2, saved.FootReturn.Pose0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RealBroadContactAndBoardingGateReboardTheParkedShipThroughSixtyOneTakeoffCallbacks()
    {
        GameSession session = Fixture(3600);
        using WorldMapScene scene = new(session, GameDirectory);
        Land(scene);
        scene.Apply(new(PlayerActionKind.Wait));
        for (int callback = 0; callback < 48 && scene.State.NativeRagnarokRuntime!.BroadContact != scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex; callback++)
        {
            scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: 1));
        }
        Assert.Equal(scene.State.NativeRagnarokRuntime!.Allocation.RagnarokIndex, scene.State.NativeRagnarokRuntime.BroadContact);
        scene.Apply(new(PlayerActionKind.Wait));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeFootRuntime!.MapDisplayMode);
        Assert.Equal((byte)1, scene.State.NativeRagnarokRuntime.MapDisplayMode);
        Assert.Equal((byte)0, scene.State.NativeRagnarokRuntime.Allocation.MapDisplayMode);
        scene.Apply(new(PlayerActionKind.Wait));
        NativeRagnarokWorldRuntimeState parked = scene.State.NativeRagnarokRuntime;
        NativeRagnarokWorldObject boarded = parked.Objects[parked.Allocation.RagnarokIndex];
        scene.Apply(new(PlayerActionKind.Confirm));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(boarded, scene.State.NativeRagnarokRuntime!.DrawBody);
        Assert.Equal((byte)1, scene.State.NativeRagnarokRuntime!.MapDisplayMode);
        Assert.Equal(NativeRagnarokTransferKind.Takeoff, scene.State.NativeRagnarokRuntime!.Transfer!.Kind);
        Assert.Equal(1, scene.State.NativeRagnarokRuntime.Transfer.Counter);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((short)-14336, scene.State.NativeRagnarokRuntime!.TransferCamera!.OffsetZ);
        Assert.Equal(6144, scene.State.NativeRagnarokRuntime.TransferCamera.CurveThreshold);
        Assert.Equal(new NativeWorldPathPosition(scene.State.X, scene.State.NativeHeight, -scene.State.Z), scene.State.NativeRagnarokRuntime.Objects[scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex].Position);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 2));
        Assert.Equal((short)-14336, scene.State.NativeRagnarokRuntime!.TransferCamera!.OffsetZ);
    }

    [Theory]
    [InlineData(0x40)]
    [InlineData(0x02)]
    [InlineData(0x42)]
    public void FirstBoardingCabinDeparturePreservesFoot0AndParkedShip2AndTheirIndependentReload(ushort firstButtons)
    {
        GameSession session = Fixture();
        using (WorldMapScene scene = new(session, GameDirectory))
        {
            Land(scene);
            ApproachShip(scene);
            scene.PrepareFieldEntry = (selection, finalWorld, location, salary) =>
            {
                Assert.Equal(44, selection.Entry.Index);
                Assert.Equal(827, selection.Entry.FieldId);
                NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(finalWorld, checked((ushort)session.State.FieldId), session.State.WorldEntryMemory!.StoryWord, location);
                FieldTransition transition = new(selection.Entry.FieldId, selection.Entry.X, selection.Entry.Y, selection.Entry.Triangle, selection.Entry.Direction);
                GameState departure = session.State;
                return () => session.CommitPreparedWorldFieldEntry(departure, finalWorld, transition, receipt, salary, _ => { });
            };
            scene.Apply(new(PlayerActionKind.Confirm));
        }
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(827, session.State.FieldId);
        Assert.NotNull(session.State.SavedWorld!.FootReturn);
        Assert.Null(session.State.SavedWorld.RagnarokReturn);
        NativeSavedWorldPose parked = session.State.SavedWorld.RagnarokPose2!;
        NativeSavedWorldPose footPose = session.State.SavedWorld.FootReturn!.Pose0;
        Assert.NotEqual(parked, footPose);
        Assert.Equal((byte)0, session.State.WorldConfiguration!.EntityByte);
        //Declare the original general saved-foot packet for an isolated reload
        //component. This does not execute or replace field827's exit scripts.
        session.RequestWorld(new(62, 0, -1));
        using WorldMapScene reload = new(session, GameDirectory);
        Assert.Equal(NativeFootWorldRuntimeState.SavedReloadRule, reload.State.NativeFootRuntime!.Rule);
        Assert.Equal(new NativeWorldPathPosition(footPose.X, footPose.Height, -footPose.RawZ), reload.State.NativeRagnarokRuntime!.Objects[0].Position);
        Assert.Equal(new NativeWorldPathPosition(parked.X, parked.Height, -parked.RawZ), reload.State.NativeRagnarokRuntime.Objects[reload.State.NativeRagnarokRuntime.Allocation.RagnarokIndex].Position);
        Assert.Equal(5216, reload.State.NativeRagnarokRuntime.TransferCamera!.CurveThreshold);
        Assert.Equal((short)-7168, reload.State.NativeFootRuntime.CameraOffsetZ);
        ushort? previous = session.WorldProcess!.InputHistory!.PreviousButtonsAfterWorldLoad;
        byte view = reload.State.NativeFootRuntime.AnimationVariant;
        Assert.Equal(1, reload.Apply(new(PlayerActionKind.Wait, WorldButtons: firstButtons)).FramesAdvanced);
        Assert.Equal(firstButtons, session.WorldProcess.InputHistory!.CurrentButtons);
        Assert.Equal(previous, session.WorldProcess.InputHistory.PreviousButtons);
        Assert.Equal(view, reload.State.NativeFootRuntime!.AnimationVariant);
        Assert.Null(reload.State.NativeRagnarokRuntime!.Transfer);
        Assert.Equal(23, reload.Apply(new(PlayerActionKind.Wait, Frames: 23)).FramesAdvanced);
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-ragnarok-foot-reload-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using WorldMapScene restored = new(restoredSession, GameDirectory);
            reload.Apply(new(PlayerActionKind.Wait));
            restored.Apply(new(PlayerActionKind.Wait));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HistoricalSnapshotWithUnknownSharedHistoryNeedsAnObservedReleaseForViewToggle()
    {
        GameSession session = Fixture(3600);
        using (WorldMapScene scene = new(session, GameDirectory))
        {
            Land(scene);
        }
        byte view = session.State.World!.NativeFootRuntime!.AnimationVariant;
        JsonObject old = JsonSerializer.SerializeToNode(session.State)!.AsObject();
        old["WorldProcess"]!["InputHistory"] = null;
        old["World"]!["NativeRagnarokRuntime"]!["PreviousButtons"] = 0;
        string path = TestResources.ArtifactFile("parked-ragnarok-old-input.json");
        File.WriteAllText(path, old.ToJsonString());
        GameSession restored = new(GameSession.Load(path));
        using WorldMapScene reload = new(restored, GameDirectory);
        Assert.Null(restored.WorldProcess!.InputHistory);
        PlayerAction viewButton = new(PlayerActionKind.Wait, WorldButtons: 2, Frames: 2);
        Assert.Equal(2, reload.Apply(viewButton).FramesAdvanced);
        Assert.Equal(view, reload.State.NativeFootRuntime!.AnimationVariant);
        Assert.True(reload.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(2, reload.Apply(viewButton).FramesAdvanced);
        Assert.Equal(1 - view, reload.State.NativeFootRuntime!.AnimationVariant);
    }

    [Fact]
    public void StandaloneParkedRagnarokSnapshotContinuesWithConfirm()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeRagnarokFootTests.1")));
        using GameWorld world = new(session, GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        WorldState before = session.State.World!;
        Assert.NotNull(before.NativeFootRuntime);
        Assert.NotNull(before.NativeRagnarokRuntime);
        Assert.Equal(1, world.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x40)).FramesAdvanced);
        Assert.Equal(before.Frame + 1, session.State.World!.Frame);
        Assert.NotNull(session.State.World.NativeFootRuntime);
        Assert.Null(session.State.World.NativeRagnarokRuntime!.Transfer);
        Assert.Equal(24, world.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        Assert.Equal(before.Frame + 25, session.State.World!.Frame);
    }

    private static void ApproachShip(WorldMapScene scene)
    {
        scene.Apply(new(PlayerActionKind.Wait));
        for (int callback = 0; callback < 48 && scene.State.NativeRagnarokRuntime!.BroadContact != scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex; callback++)
        {
            scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: 1));
        }
        Assert.Equal(scene.State.NativeRagnarokRuntime!.Allocation.RagnarokIndex, scene.State.NativeRagnarokRuntime.BroadContact);
    }

    internal static void Land(WorldMapScene scene)
    {
        scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
        KeyboardInput keyboard = new();
        InputFrame input = keyboard.Read(new KeyboardState(Keys.Enter), true);
        Assert.True(input.Confirm);
        PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, new GameState { Scene = SceneKind.WorldMap, World = scene.State }, keyboard.HeldControlButtons);
        Assert.Equal(PlayerActionKind.WorldButtons, action.Kind);
        Assert.Equal(0x40, action.Value);
        Assert.True(scene.Apply(action).Accepted);
        Assert.True(scene.State.NativeRagnarokRuntime!.Transfer is not null, $"Landing not admitted at({scene.State.X},{scene.State.Z},{scene.State.NativeHeight}) ground={scene.State.GroundType}, frame={scene.State.Frame}, groundHeight={scene.State.NativeRagnarokRuntime.CurrentGroundHeight}, contacts={scene.State.NativeRagnarokRuntime.PrimaryContact}/{scene.State.NativeRagnarokRuntime.BroadContact}.");
        Assert.Equal(NativeRagnarokTransferKind.Landing, scene.State.NativeRagnarokRuntime.Transfer.Kind);
        Assert.Equal(1, scene.State.NativeRagnarokRuntime.Transfer.Counter);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        Assert.NotNull(scene.State.NativeFootRuntime);
    }

    internal static GameSession Fixture(ushort story = 3500)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, GameDirectory);
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory!.WriteBytes(256, 2, story);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new(32, 50, -1), WorldEntryMemory = entry, SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(260) } });
        return session;
    }
}
