using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFootWorldReturnTests
{
    [Fact]
    public void Actual_foot_save_preserves_raw_pose_yaws_frame_and_unknown_auxiliary_bytes()
    {
        WorldState world = World();
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(world, 159, 17, 1);
        Assert.Equal(new NativeSavedWorldPose(30240, -29393, -691, 3840), receipt.Pose0);
        Assert.Equal(2048, receipt.CameraYaw96);
        Assert.Equal(world.Frame, receipt.SourceWorldFrame);
        Assert.Equal(159, receipt.LastFieldId);
        Assert.Equal(17, receipt.StoryWord);
        Assert.Equal(0, receipt.EntityType105);
        Assert.Equal(255, receipt.Channel106);
        Assert.Equal(255, receipt.Channel107);
        Assert.Null(receipt.AnimationVariant108);
        Assert.Null(receipt.MovementMode110);
        Assert.Throws<NotSupportedException>(receipt.RequireCompleteFootReload);
        NativeSavedWorldState prior = NativeSavedWorldState.CreateNewGame() with { Slot4 = new NativeSavedWorldVehicleSlot(73, new NativeSavedWorldPose(-123, 456, -789, 65000)) };
        NativeSavedWorldState saved = prior.WithFootReturn(receipt);
        NativeSavedWorldState restored = JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(saved))!;
        restored.Validate();
        Assert.Equal(saved, restored);
        Assert.Equal(prior.Slot4, saved.Slot4);
        Assert.Equal(prior.Slot5, saved.Slot5);
        Assert.Equal(prior.Origin, saved.Origin);
        Assert.Null(NativeSavedWorldState.CreateNewGame().FootReturn);
    }

    [Fact]
    public void Native_saved_mode_three_normalizes_to_zero_and_explicit_complete_bytes_can_reload()
    {
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(World(), 159, 17, -128, 201, 3);
        receipt.RequireCompleteFootReload();
        Assert.Equal<byte?>(201, receipt.AnimationVariant108);
        Assert.Equal<byte?>(0, receipt.MovementMode110);
        Assert.Equal(-128, receipt.MapLocationId);
        Assert.Throws<InvalidDataException>(() => (receipt with { MovementMode110 = 3 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { MapLocationId = 128 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { EntityType105 = 6 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { Channel106 = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { SourceWorldFrame = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (receipt with { Rule = "unproved" }).Validate());
        Assert.Throws<InvalidDataException>(() => NativeSavedWorldFootReturn.Create(World() with { NativeHeight = 32768 }, 159, 17, 1));
    }

    [Fact]
    public void Missing_foot_json_metadata_does_not_infer_a_pose_or_implicit_auxiliary_zero()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldFootReturn>("{}"));
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(World(), 159, 17, 1);
        string json = JsonSerializer.Serialize(receipt);
        Assert.Contains("\"AnimationVariant108\":null", json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldFootReturn>(json.Replace("\"AnimationVariant108\":null,", "")));
        NativeSavedWorldState old = JsonSerializer.Deserialize<NativeSavedWorldState>(JsonSerializer.Serialize(NativeSavedWorldState.CreateNewGame()))!;
        Assert.Null(old.FootReturn);
        old.Validate();
    }

    [Fact]
    public void Prepared_world_field_commit_is_atomic_and_retains_native_last_field_and_frozen_VM()
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 30);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        FieldTransition arrival = new(136, 175, -2269, 12, 128);
        int published = 0;
        session.StateChanged += _ => published++;
        session.CommitPreparedWorldFieldEntry(before, final, arrival, receipt, candidate =>
        {
            Assert.Same(before, session.State);
            Assert.Equal(0, published);
            Assert.Equal(159, candidate.PreviousFieldId);
            Assert.Equal(before.EventState!.Value.GetRawText(), candidate.DepartingFieldState!.Value.GetRawText());
            Assert.Same(final, candidate.DepartingWorldState);
            Assert.Equal(receipt, candidate.SavedWorld!.FootReturn);
        });
        Assert.Equal(1, published);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(136, session.State.FieldId);
        Assert.Equal("bdview1", session.State.FieldName);
        Assert.Equal(159, session.State.PreviousFieldId);
        Assert.Equal(arrival, session.State.FieldArrival);
        Assert.Equal(175, session.State.X);
        Assert.Equal(-2269, session.State.Y);
        Assert.Null(session.State.World);
        Assert.Null(session.State.WorldArrival);
        Assert.Null(session.State.EventState);
        Assert.Null(session.State.DialogueDraft);
        Assert.Null(session.Dialog);
        Assert.Equal(0xA4, session.WorldControls!.Menu209);
        Assert.Equal(before.WorldControls!.SourceDisabled, session.WorldControls.SourceDisabled);
        Assert.Equal(before.SavedWorld!.Slot4, session.State.SavedWorld!.Slot4);
        Assert.Equal(17, session.State.Variables[256]);
        // WORD84 is installed by the existing field-loader authority, after it consumes the frozen VM.
        Assert.Equal(160, session.State.Variables[84]);
    }

    [Fact]
    public void Asset_preflight_failure_preserves_live_world_dialogue_and_save_history()
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 1);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        Assert.Throws<NotSupportedException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, _ => throw new NotSupportedException("Unsupported destination asset boundary")));
        Assert.Same(before, session.State);
        Assert.Equal("Retained", session.Dialog);
        Assert.Null(session.State.SavedWorld!.FootReturn);
        Assert.Null(session.State.DepartingWorldState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Altered_action_prefix_tail_pose_frame_or_receipt_refuses_before_publication(int alteration)
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 30);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        List<WorldActionRecord> actions = [.. final.Actions];
        if (alteration == 0)
        {
            actions[0] = actions[0] with { X = actions[0].X + 1 };
        }
        else if (alteration == 1)
        {
            actions[^1] = actions[^1] with { AdvancedFrames = 29 };
        }
        else if (alteration == 2)
        {
            actions[^1] = actions[^1] with { X = final.X + 1 };
        }
        else if (alteration == 3)
        {
            actions[^1] = actions[^1] with { Index = 17 };
        }
        else if (alteration == 4)
        {
            receipt = receipt with { SourceWorldFrame = receipt.SourceWorldFrame - 1 };
        }
        else
        {
            receipt = receipt with { LastFieldId = 160 };
        }
        final = final with { Actions = actions };
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, _ => Assert.Fail("Altered preparation must refuse before the callback.")));
        Assert.Same(before, session.State);
    }

    [Fact]
    public void Stale_session_and_changed_validation_session_refuse_and_invalid_field_packet_never_publishes()
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 1);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        session.SetMoviePosition(1);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, _ => Assert.Fail("Stale source must refuse before validation.")));
        session.Restore(before);
        Assert.Throws<InvalidDataException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, float.NaN, 0, 12, 128), receipt, _ => Assert.Fail("Malformed arrival must refuse before validation.")));
        Assert.Same(before, session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, _ => session.SetMoviePosition(2)));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(session.State.SavedWorld!.FootReturn);
    }

    [Fact]
    public void Foot_return_and_complete_departing_world_actions_survive_checkpoint_reload()
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 30);
        session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), NativeSavedWorldFootReturn.Create(final, 159, 17, 1), Accept);
        string path = Path.Combine(Path.GetTempPath(), "ff8-foot-return-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameState restored = GameSession.Load(path);
            Assert.Equal(session.State.SavedWorld, restored.SavedWorld);
            Assert.Equal(159, restored.PreviousFieldId);
            Assert.Equal(final.Frame, restored.DepartingWorldState!.Frame);
            Assert.Equal(final.Actions, restored.DepartingWorldState.Actions);
            Assert.Equal(JsonSerializer.Serialize(session.State.DepartingFieldState), JsonSerializer.Serialize(restored.DepartingFieldState));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static WorldState World()
    {
        return new WorldState
        {
            Frame = 100,
            X = 30240,
            Z = -29393,
            NativeHeight = -691,
            BodyYaw = 3840,
            CameraYaw = 2048,
            GroundType = 6,
            Actions = [new WorldActionRecord(1, 100, "Move", 1, 0, 100, 100, 30240, -29393, -691)]
        };
    }

    private static WorldState Final(WorldState before, int advanced)
    {
        WorldState next = before with { Frame = before.Frame + advanced, X = before.X + 1 };
        return next with { Actions = [.. before.Actions, new WorldActionRecord(before.Actions.Count + 1, next.Frame, "Move", 1, 0, 60, advanced, next.X, next.Z, next.NativeHeight)] };
    }

    private static GameSession Session()
    {
        var session = new GameSession(new GameState
        {
            Scene = SceneKind.WorldMap,
            FieldId = 159,
            PreviousFieldId = 160,
            FieldName = "bggate_1",
            WorldArrival = new WorldTransition(0, 0, 0),
            World = World(),
            SavedWorld = NativeSavedWorldState.CreateNewGame(),
            WorldControls = NativeWorldControlState.CreateNewGame() with { Menu209 = 0xA7 },
            EventState = JsonSerializer.SerializeToElement(new { FieldId = 159, Party = new[] { 0, 4 }, Name = "Squall" }),
            Variables = new Dictionary<int, int> { [256] = 17, [84] = 160 },
            DialogueDraft = new DialogueDraft(0, 1, 0, "Squall")
        });
        session.ShowDialog("Retained");
        return session;
    }

    private static void Accept(GameState state)
    {
    }
}
