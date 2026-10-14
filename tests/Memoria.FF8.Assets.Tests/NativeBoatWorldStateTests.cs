using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBoatWorldStateTests
{
    [Fact]
    public void OriginalBoatSavesAtEndpointAndAtomicallyPublishesRowAfterSixteenFadeCallbacks()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeBoatWorldFootRecord foot = assets.ReadScriptedBoatFootRecord(40);
        Assert.Equal(new NativeSavedWorldPose(0, 0, 0, 0), foot.Pose);
        Assert.Throws<NotSupportedException>(() => assets.ReadScriptedBoatFootRecord(33));
        NativeSavedWorldState initial = NativeSavedWorldState.CreateNewGame();
        var previous = new WorldState { Frame = 500, X = 12390, Z = -26709, NativeHeight = -301, BodyYaw = 2256, CameraYaw = 2256, NativeCarRuntime = NativeCarWorldRuntimeState.Create(initial, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0))) };
        NativeSavedWorldCarReturn car = NativeSavedWorldCarReturn.Create(previous, 262, 40, 1);
        NativeSavedWorldState saved = initial.WithOrdinaryCarEntry().WithCarReturn(car);
        NativeBoatWorldLoadReceipt load = NativeBoatWorldLoadReceipt.Create(saved, previous, foot);
        Assert.Equal((byte)0, load.AnimationVariant);
        Assert.Equal((byte)0, load.MapDisplayMode);
        var packet = new WorldTransition(11, 66, 61);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedBoatPath(11);
        NativeWorldScriptedPathState path = NativeWorldScriptedPathState.Create(definition, packet, null);
        var boat = new NativeBoatWorldRuntimeState(1, NativeBoatWorldRuntimeState.RuntimeRule, path, load, null);
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600);
        var session = new GameSession(new GameState { Scene = SceneKind.WorldMap, FieldId = 123, WorldArrival = packet, World = boat.ToWorldState(), DepartingWorldState = previous, SavedWorld = saved, WorldControls = NativeWorldControlState.CreateNewGame(), Variables = new() { [256] = 40 }, ExamTimer = timer });
        for (int callback = 0; callback < 230; callback++)
        {
            path = path.Step(definition, (_, _, _) => null);
        }
        Assert.Equal((ushort)0, path.CameraCounter);
        Assert.Equal((ushort)230, path.CameraReceipt.Counter);
        WorldFieldEntry row = assets.FieldEntries.Entries[61];
        FieldTransition transition = new(row.FieldId, row.X, row.Y, row.Triangle, row.Direction);
        Assert.Equal(new FieldTransition(260, 143, 102, 18, 0), transition);
        boat = boat with { Path = path };
        NativeSavedWorldBoatReturn receipt = NativeSavedWorldBoatReturn.Create(boat, 123, 40, assets.GetNativeMapLocationId(path.EntityPosition.X, -path.EntityPosition.Z), transition);
        boat = boat with { Return = receipt };
        WorldState endpoint = boat.ToWorldState() with { Actions = [new(1, 230, "Wait", 0, 0, 230, 230, path.EntityPosition.X, -path.EntityPosition.Z, path.EntityPosition.Y)] };
        GameState beforeEndpoint = session.State;
        Assert.Throws<InvalidDataException>(() => session.CommitWorldAction(endpoint with { NativeBoatRuntime = boat with { Return = receipt with { LastFieldId = 120 } } }, 0));
        Assert.Same(beforeEndpoint, session.State);
        session.CommitWorldAction(endpoint, 0);
        Assert.Equal(receipt, session.State.SavedWorld!.BoatReturn);
        Assert.Equal(230, receipt.SourceWorldFrame);
        Assert.Equal(230, session.State.WorldConfiguration!.SourceWorldFrame);
        Assert.Equal(saved.Slot4, session.State.SavedWorld.Slot4);
        Assert.Equal(saved.Slot5, session.State.SavedWorld.Slot5);
        Assert.Equal((ushort)NativeWorldScriptedPathMath.NativeAngle(unchecked((short)(path.Camera.Target.X - path.Camera.Eye.X)), unchecked((short)(path.Camera.Target.Z - path.Camera.Eye.Z))), receipt.CameraYaw96);
        Assert.Throws<InvalidDataException>(() => (receipt with { CameraYaw96 = (ushort)(receipt.CameraYaw96 ^ 1) }).Validate());
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        var restoredSession = new GameSession(restored);
        Assert.Equal(receipt, restoredSession.State.SavedWorld!.BoatReturn);
        for (int callback = 0; callback < 16; callback++)
        {
            path = path.Step(definition, (_, _, _) => throw new InvalidOperationException("Fade must freeze projection."));
        }
        boat = boat with { Path = path };
        WorldState final = boat.ToWorldState(endpoint) with { Actions = [.. endpoint.Actions, new(2, 246, "Wait", 0, 0, 16, 16, path.EntityPosition.X, -path.EntityPosition.Z, path.EntityPosition.Y)] };
        GameState departure = session.State;
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedBoatFieldEntry(departure, final, transition with { FieldId = 123 }, _ => { }));
        Assert.Same(departure, session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedBoatFieldEntry(departure, final, transition, _ => throw new InvalidOperationException("Rejected destination.")));
        Assert.Same(departure, session.State);
        session.CommitPreparedBoatFieldEntry(departure, final, transition, _ => { });
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(transition, session.State.FieldArrival);
        Assert.Equal(246, session.State.DepartingWorldState!.Frame);
        Assert.Equal(230, session.State.SavedWorld!.BoatReturn!.SourceWorldFrame);
        Assert.Equal(JsonSerializer.Serialize(timer.AdvanceCallbacks(NativeTimerCallbackKind.World, 246)), JsonSerializer.Serialize(session.State.ExamTimer));
        Assert.Equal((byte)66, session.State.WorldConfiguration!.EntityByte);
        Assert.Equal(230, session.State.WorldConfiguration.SourceWorldFrame);
        session.State.WorldConfiguration.Validate();
    }
}
