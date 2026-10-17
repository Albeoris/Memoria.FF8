using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFootWorldReturnTests
{
    [Fact]
    public void World_load_uses_departing_VM_and_validates_its_frozen_timer_against_elapsed_world_callbacks()
    {
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600).Display(250, 10).KillDisplay();
        var session = Session();
        session.SetEventState(RuntimeOwnerCheckpointFixture.Capture(session.State with { ExamTimer = timer.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1) }));
        session.SetDepartingFieldState(RuntimeOwnerCheckpointFixture.Capture(session.State with { ExamTimer = timer }));
        session.SetExamTimer(timer.AdvanceCallbacks(NativeTimerCallbackKind.World, 3));
        string path = Path.Combine(Path.GetTempPath(), "ff8-frozen-world-timer-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameState restored = GameSession.Load(path);
            Assert.Equal(JsonSerializer.Serialize(session.ExamTimer), JsonSerializer.Serialize(restored.ExamTimer));
            Assert.True(JsonElement.DeepEquals(session.State.DepartingFieldState!.Value, restored.DepartingFieldState!.Value));
            byte[] original = File.ReadAllBytes(path);
            session.SetNativeEventOwners(session.WorldControls! with { EventFlags104 = session.WorldControls.EventFlags104 | 0x40u }, timer: session.ExamTimer! with { DisplayEnabled = true, Event182Mask4 = true, SaveGlobalMask4 = true });
            Assert.Throws<InvalidDataException>(() => session.Save(path));
            Assert.Equal(original, File.ReadAllBytes(path));
            session.SetExamTimer(timer.AdvanceCallbacks(NativeTimerCallbackKind.Menu, 7));
            Assert.Throws<InvalidDataException>(() => session.Save(path));
            Assert.Equal(original, File.ReadAllBytes(path));
            session.SetExamTimer(timer.AdvanceCallbacks(NativeTimerCallbackKind.World, 101));
            Assert.Throws<InvalidDataException>(() => session.Save(path));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void World_return_commits_timer_callbacks_including_the_final_departure_callback_atomically()
    {
        var session = Session();
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600).Display(250, 10).KillDisplay();
        session.SetExamTimer(timer);
        GameState before = session.State;
        WorldState final = Final(before.World!, 30);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        NativeExamTimerState expected = timer.AdvanceCallbacks(NativeTimerCallbackKind.World, 30);
        FieldTransition transition = new(136, 175, -2269, 12, 128);
        Assert.Throws<NotSupportedException>(() => session.CommitPreparedWorldFieldEntry(before, final, transition, receipt, SalaryBatch(before, final), candidate =>
        {
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(candidate.ExamTimer));
            Assert.Same(before, session.State);
            throw new NotSupportedException("Bounded destination refusal.");
        }));
        Assert.Same(before, session.State);
        session.CommitPreparedWorldFieldEntry(before, final, transition, receipt, SalaryBatch(before, final), Accept);
        Assert.Equal(60, session.ExamTimer!.ClockCalls);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.ExamTimer));
        Assert.False(session.ExamTimer.DisplayEnabled);
    }

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
        Assert.Null(receipt.MapDisplayMode110);
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
        Assert.Equal<byte?>(0, receipt.MapDisplayMode110);
        Assert.Equal(-128, receipt.MapLocationId);
        Assert.Throws<InvalidDataException>(() => (receipt with { MapDisplayMode110 = 3 }).Validate());
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
        session.CommitPreparedWorldFieldEntry(before, final, arrival, receipt, SalaryBatch(before, final), candidate =>
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
    public void Asset_preflight_failure_preserves_live_world_dialogue_and_saved_owners()
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 1);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        Assert.Throws<NotSupportedException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, SalaryBatch(before, final), _ => throw new NotSupportedException("Unsupported destination asset boundary")));
        Assert.Same(before, session.State);
        Assert.Equal("Retained", session.Dialog);
        Assert.Null(session.State.SavedWorld!.FootReturn);
        Assert.Null(session.State.DepartingWorldState);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Altered_receipt_frame_or_source_field_refuses_before_publication(int alteration)
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 30);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(final, 159, 17, 1);
        if (alteration == 4)
        {
            receipt = receipt with { SourceWorldFrame = receipt.SourceWorldFrame - 1 };
        }
        else
        {
            receipt = receipt with { LastFieldId = 160 };
        }
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, SalaryBatch(before, final), _ => Assert.Fail("Altered preparation must refuse before the callback.")));
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
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, SalaryBatch(before, final), _ => Assert.Fail("Stale source must refuse before validation.")));
        session.Restore(before);
        Assert.Throws<InvalidDataException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, float.NaN, 0, 12, 128), receipt, SalaryBatch(before, final), _ => Assert.Fail("Malformed arrival must refuse before validation.")));
        Assert.Same(before, session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), receipt, SalaryBatch(before, final), _ => session.SetMoviePosition(2)));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(session.State.SavedWorld!.FootReturn);
    }

    [Fact]
    public void Foot_return_and_departing_world_snapshot_survive_checkpoint_reload()
    {
        var session = Session();
        GameState before = session.State;
        WorldState final = Final(before.World!, 30);
        session.CommitPreparedWorldFieldEntry(before, final, new FieldTransition(136, 175, -2269, 12, 128), NativeSavedWorldFootReturn.Create(final, 159, 17, 1), SalaryBatch(before, final), Accept);
        string path = Path.Combine(Path.GetTempPath(), "ff8-foot-return-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameState restored = GameSession.Load(path);
            Assert.Equal(session.State.SavedWorld, restored.SavedWorld);
            Assert.Equal(159, restored.PreviousFieldId);
            Assert.Equal(final.Frame, restored.DepartingWorldState!.Frame);
            Assert.Equal(JsonSerializer.Serialize(final), JsonSerializer.Serialize(restored.DepartingWorldState));
            Assert.Equal(JsonSerializer.Serialize(session.State.DepartingFieldState), JsonSerializer.Serialize(restored.DepartingFieldState));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Foot_type1_commit_retains_the_packet_type_and_refuses_a_type0_receipt()
    {
        GameState source = Session().State;
        NativeFootWorldRuntimeState runtime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(source.SavedWorld!, 1);
        var session = new GameSession(source with { WorldArrival = new WorldTransition(0, 1, 0), World = source.World! with { NativeFootRuntime = runtime } });
        GameState before = session.State;
        WorldState final = Final(before.World!, 1);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(final, 159, 17, 1);
        FieldTransition transition = new(136, 175, -2269, 12, 128);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldFieldEntry(before, final, transition, receipt with { EntityType105 = 0 }, SalaryBatch(before, final), Accept));
        Assert.Same(before, session.State);
        session.CommitPreparedWorldFieldEntry(before, final, transition, receipt, SalaryBatch(before, final), Accept);
        Assert.Equal((byte)1, session.State.SavedWorld!.FootReturn!.EntityType105);
        Assert.Equal((byte)1, session.State.DepartingWorldState!.NativeFootRuntime!.EntityType);
        Assert.Equal((byte)1, session.State.WorldConfiguration!.EntityByte);
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
            NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame()),
            GroundType = 6
        };
    }

    private static WorldState Final(WorldState before, int advanced)
    {
        return before with { Frame = before.Frame + advanced, X = before.X + 1 };
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
            // Declared component initialization for this owner-only fixture.
            WorldProcess = NativeWorldProcess.FromColdProcess(),
            WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 17),
            EventMemory = NativeEventMemory.Empty.Write(84, 2, NativeScalarValue.Exact(160)),
            DialogueDraft = new DialogueDraft(0, 1, 0, "Squall")
        });
        session.SetEventState(RuntimeOwnerCheckpointFixture.Capture(session.State));
        session.ShowDialog("Retained");
        return session;
    }

    private static NativeWorldSalaryState SalaryBatch(GameState before, WorldState final)
    {
        // Declared foot-return component has no salary owner or movement producer.
        // These explicit callback receipts test publication, not a fabricated route.
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(before);
        for (long callback = before.World!.Frame; callback < final.Frame; callback++)
        {
            batch = batch.Advance(null, batch.Presentation, batch.SoundProcess, 0, 2);
        }
        return batch;
    }

    private static void Accept(GameState state)
    {
    }
}
