using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeScriptedWorldStateTests
{
    [Theory]
    [InlineData(11, 66, 40, 61)]
    [InlineData(9, 22, 205, 59)]
    public void OriginalScriptedModesSaveAtEndpointAndAtomicallyPublishRowAfterSixteenFadeCallbacks(int track, byte entity, ushort story, int destination)
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeScriptedWorldFootRecord foot = assets.ReadScriptedFootRecord(story, 0);
        Assert.Equal(new NativeSavedWorldPose(0, 0, 0, 0), foot.Pose);
        Assert.Equal((byte)0, assets.ReadScriptedFootRecord(33, 0).ShapeId);
        Assert.Throws<NotSupportedException>(() => assets.ReadScriptedFootRecord(290, 0));
        NativeSavedWorldState initial = NativeSavedWorldState.CreateNewGame();
        WorldState previous;
        NativeSavedWorldState saved;
        if (entity == 66)
        {
            previous = new WorldState { Frame = 500, X = 12390, Z = -26709, NativeHeight = -301, BodyYaw = 2256, CameraYaw = 2256, NativeCarRuntime = NativeCarWorldRuntimeState.Create(initial, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0)), NativeWorldProcess.FromColdProcess()) };
            NativeSavedWorldCarReturn car = NativeSavedWorldCarReturn.Create(previous, 262, story, 1);
            saved = initial.WithOrdinaryCarEntry().WithCarReturn(car);
        }
        else
        {
            previous = new WorldState { Frame = 500, X = 13895, Z = -27012, NativeHeight = -383, BodyYaw = 2256, CameraYaw = 2256, NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(initial) };
            saved = initial.WithFootReturn(NativeSavedWorldFootReturn.RecordFootDeparture(previous, 104, story, 1));
        }
        NativeScriptedWorldLoadReceipt load = NativeScriptedWorldLoadReceipt.Create(saved, previous, foot);
        string missingShape = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(foot))!.ToJsonString();
        System.Text.Json.Nodes.JsonObject malformed = System.Text.Json.Nodes.JsonNode.Parse(missingShape)!.AsObject();
        malformed.Remove(nameof(NativeScriptedWorldFootRecord.ShapeId));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeScriptedWorldFootRecord>(malformed.ToJsonString()));
        Assert.Equal(previous.BodyYaw, load.RetainedBodyYaw);
        Assert.Equal(previous.Frame, load.SourceWorldFrame);
        Assert.Throws<NotSupportedException>(() => NativeScriptedWorldLoadReceipt.Create(saved, previous with { BodyYaw = 1 }, foot));
        Assert.Equal((byte)0, load.AnimationVariant);
        Assert.Equal((byte)0, load.MapDisplayMode);
        var packet = new WorldTransition(checked((short)track), entity, checked((short)destination));
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(track);
        NativeWorldScriptedPathState path = NativeWorldScriptedPathState.Create(definition, packet, null);
        var boat = new NativeScriptedWorldRuntimeState(1, NativeScriptedWorldRuntimeState.RuntimeRule, path, load, null);
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600);
        // Explicit saved-world component, without a historical player journal.
        NativeWorldEntryMemory selectedMemory = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, story);
        var session = new GameSession(new GameState { Scene = SceneKind.WorldMap, FieldId = 123, WorldArrival = packet, World = boat.ToWorldState(), DepartingWorldState = previous, SavedWorld = saved, WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = selectedMemory, WorldProcess = NativeWorldProcess.FromColdProcess(), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int>() { [256] = story }), ExamTimer = timer });
        NativeWorldPathPosition previousBody = path.EntityPosition;
        for (int callback = 0; !path.EndpointRequested && callback < 5000; callback++)
        {
            previousBody = path.EntityPosition;
            path = path.Step(definition, (_, _, _) => null);
        }
        Assert.True(path.EndpointRequested);
        long endpointCallback = path.Callback;
        Assert.Equal((ushort)0, path.CameraCounter);
        Assert.Equal((ushort)endpointCallback, path.CameraReceipt.Counter);
        WorldFieldEntry row = assets.FieldEntries.Entries[destination];
        FieldTransition transition = new(row.FieldId, row.X, row.Y, row.Triangle, row.Direction);
        Assert.Equal(entity == 66 ? new FieldTransition(260, 143, 102, 18, 0) : new FieldTransition(265, 296, -371, 21, 0), transition);
        boat = boat with { Path = path };
        NativeSavedWorldScriptedReturn receipt = NativeSavedWorldScriptedReturn.Create(boat, 123, story, assets.GetNativeMapLocationId(path.EntityPosition.X, -path.EntityPosition.Z), transition);
        boat = boat with { Return = receipt };
        WorldState endpoint = boat.ToWorldState();
        GameState beforeEndpoint = session.State;
        NativeWorldSalaryState endpointSalary = SalaryBatch(session.State, endpoint);
        Assert.Throws<InvalidDataException>(() => session.CommitWorldAction(endpoint with { NativeScriptedRuntime = boat with { Return = receipt with { LastFieldId = 120 } } }, endpointSalary));
        Assert.Same(beforeEndpoint, session.State);
        session.CommitWorldAction(endpoint, endpointSalary);
        Assert.Equal(receipt, session.State.SavedWorld!.ScriptedReturn);
        Assert.Equal(endpointCallback, receipt.SourceWorldFrame);
        Assert.Equal(endpointCallback, session.State.WorldConfiguration!.SourceWorldFrame);
        Assert.Equal(saved.Slot4, session.State.SavedWorld.Slot4);
        Assert.Equal(saved.Slot5, session.State.SavedWorld.Slot5);
        Assert.Equal(entity, receipt.EntityType105);
        NativeSavedWorldPose expectedSavedFoot = path.Mode == 1 ? foot.Pose : new(previousBody.X, -previousBody.Z, checked((short)previousBody.Y), load.RetainedBodyYaw);
        Assert.Equal(expectedSavedFoot, receipt.Pose0);
        Assert.NotEqual(receipt.Pose0, new NativeSavedWorldPose(endpoint.X, endpoint.Z, checked((short)endpoint.NativeHeight), checked((ushort)endpoint.BodyYaw)));
        Assert.Throws<InvalidDataException>(() => (receipt with { StoryWord = 290 }).Validate());
        Assert.True(path.Camera.Eye.HasValue);
        Assert.Equal((ushort)NativeWorldScriptedPathMath.NativeAngle(unchecked((short)(path.Camera.Target.X - path.Camera.Eye.Value.X)), unchecked((short)(path.Camera.Target.Z - path.Camera.Eye.Value.Z))), receipt.CameraYaw96);
        Assert.Throws<InvalidDataException>(() => (receipt with { CameraYaw96 = (ushort)(receipt.CameraYaw96 ^ 1) }).Validate());
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        var restoredSession = new GameSession(restored);
        Assert.Equal(receipt, restoredSession.State.SavedWorld!.ScriptedReturn);
        for (int callback = 0; callback < 16; callback++)
        {
            path = path.Step(definition, (_, _, _) => throw new InvalidOperationException("Fade must freeze projection."));
        }
        boat = boat with { Path = path };
        WorldState final = boat.ToWorldState(endpoint);
        GameState departure = session.State;
        NativeWorldSalaryState fadeSalary = SalaryBatch(departure, final);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedScriptedFieldEntry(departure, final, transition with { FieldId = 123 }, fadeSalary, _ => { }));
        Assert.Same(departure, session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedScriptedFieldEntry(departure, final, transition, fadeSalary, _ => throw new InvalidOperationException("Rejected destination.")));
        Assert.Same(departure, session.State);
        session.CommitPreparedScriptedFieldEntry(departure, final, transition, fadeSalary, _ => { });
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(transition, session.State.FieldArrival);
        Assert.Equal(endpointCallback + 16, session.State.DepartingWorldState!.Frame);
        Assert.Equal(endpointCallback, session.State.SavedWorld!.ScriptedReturn!.SourceWorldFrame);
        Assert.Equal(JsonSerializer.Serialize(timer.AdvanceCallbacks(NativeTimerCallbackKind.World, checked((int)endpointCallback + 16))), JsonSerializer.Serialize(session.State.ExamTimer));
        Assert.Equal(entity, session.State.WorldConfiguration!.EntityByte);
        Assert.Equal(endpointCallback, session.State.WorldConfiguration.SourceWorldFrame);
        session.State.WorldConfiguration.Validate();
    }

    private static NativeWorldSalaryState SalaryBatch(GameState before, WorldState final)
    {
        // Declared saved-scripted component has no SeeD owner; the sixteen endpoint
        // fade callbacks carry unchanged UI owners and zero native movement units.
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(before);
        for (long callback = before.World!.Frame; callback < final.Frame; callback++)
        {
            batch = batch.Advance(null, batch.Presentation, batch.SoundProcess, 0, 2);
        }
        return batch;
    }
}
