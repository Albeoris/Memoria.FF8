using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ReplayJournalTests
{
    [Fact]
    public void Only_actual_new_game_roots_independent_save_bound_history()
    {
        GameSession session = new(new GameState());
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 0));
        Assert.Null(session.State.ReplayJournal);
        session.StartNewGame();
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 1, Value = 2, MessageId = 3, ObjectId = 0, ScriptId = 1, Operation = 8 });
        GameState saved = session.State;
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 1));
        Assert.Single(saved.ReplayJournal!.Entries);
        session.Restore(saved);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 2) { Code = 1, Value = 1 });
        Assert.Equal(2, session.State.ReplayJournal!.EntryCount);
        Assert.Equal(1, session.State.ReplayJournal.Entries[^1].Value);
        Assert.Single(saved.ReplayJournal.Entries);
        Assert.Equal(saved.ReplayJournal.SessionId, session.State.ReplayJournal.SessionId);
        session.StartNewGame();
        Assert.NotEqual(saved.ReplayJournal.SessionId, session.State.ReplayJournal!.SessionId);
    }

    [Fact]
    public void Portable_capsule_preserves_every_input_causal_receipt_and_route_stimulus()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        ReplayJournalEntry entry = new(ReplayEntryKind.Action, SceneKind.Field, 74, 3)
        {
            Code = 4,
            Advanced = 1,
            ResultFrame = 4,
            CommandCount = 10,
            CommandDigest = 987654321UL,
            ResultMessageId = 7,
            ResultObjectId = 0,
            ResultScriptId = 1,
            ResultOperation = 8,
            Navigation = [new ReplayNavigationStimulus(new ReplayPlayerPose(74, 0, 0, 0, 0, 0, 0, 0, 0), new ReplayPlayerPose(74, 1, 2, 0, 3, 128, 4096, 8192, 0), true, 4200, 8200, 0, [7])],
            Receipts = [new ReplayReceipt(ReplayReceiptKind.Branch, 74, 4, 0, 1, 8, 12, 1, "choice", [1, 2])]
        };
        session.AppendReplayEntry(entry);
        string capsule = JsonSerializer.Serialize(session.State.ReplayJournal);
        ReplayJournal decoded = JsonSerializer.Deserialize<ReplayJournal>(capsule)!;
        Assert.Equal(session.State.ReplayJournal!.SessionId, decoded.SessionId);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(entry), JsonSerializer.SerializeToElement(decoded.Entries[0])));
        entry.Navigation[0].PushContacts![0] = 99;
        ReplayJournalEntry exposed = session.State.ReplayJournal.Entries[0];
        exposed.Receipts[0].Arguments![0] = 100;
        Assert.Equal(7, session.State.ReplayJournal.Entries[0].Navigation[0].PushContacts![0]);
        Assert.Equal(1, session.State.ReplayJournal.Entries[0].Receipts[0].Arguments![0]);
    }

    [Fact]
    public void Idle_scheduler_runs_remain_compact_without_losing_time_or_prior_snapshots()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 0) { Advanced = 1, ResultFrame = 1 });
        GameState earlier = session.State;
        for (int frame = 1; frame < 60001; frame++)
        {
            session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, frame) { Advanced = 1, ResultFrame = frame + 1 });
        }
        ReplayJournal journal = session.State.ReplayJournal!;
        Assert.Equal(new[] { 30000, 30000, 1 }, journal.Entries.Select(entry => entry.Repeat));
        Assert.Equal(60001, journal.Endpoint!.ResultFrame);
        Assert.Equal(1, earlier.ReplayJournal!.Endpoint!.Repeat);
        string capsule = JsonSerializer.Serialize(journal);
        Assert.True(capsule.Length < 2048, $"Expected compact idle time, got {capsule.Length} characters.");
        ReplayJournal decoded = JsonSerializer.Deserialize<ReplayJournal>(capsule)!;
        Assert.Equal(new[] { 30000, 30000, 1 }, decoded.Entries.Select(entry => entry.Repeat));
    }

    [Fact]
    public void Journal_extraction_does_not_deserialize_obsolete_derived_save_shapes()
    {
        string path = Path.Combine(Path.GetTempPath(), "FF8-causal-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GameSession session = new(new GameState());
            session.StartNewGame();
            JsonObject save = JsonNode.Parse(JsonSerializer.Serialize(session.State))!.AsObject();
            save["SavedWorld"] = "obsolete native snapshot shape";
            save["MoviePlayback"] = new JsonObject { ["FrameIndex"] = "obsolete shape" };
            save["EventState"] = new JsonObject { ["Version"] = 999 };
            File.WriteAllText(path, save.ToJsonString());
            Assert.Throws<JsonException>(() => GameSession.Load(path));
            GameState replaySource = GameSession.LoadReplaySource(path);
            Assert.True(replaySource.ReplayJournal!.IsComplete);
            Assert.Equal(session.State.ReplayJournal!.SessionId, replaySource.ReplayJournal.SessionId);
            Assert.Null(replaySource.EventState);
            Assert.Null(replaySource.SavedWorld);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Repeating_vm_cadence_compacts_commands_and_keeps_every_branch_frame_and_decision_boundary()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        for (int frame = 0; frame < 1000; frame++)
        {
            session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, frame)
            {
                Advanced = 1,
                ResultFrame = frame + 1,
                CommandCount = 6,
                CommandDigest = 1234UL,
                Receipts = [new ReplayReceipt(ReplayReceiptKind.Branch, 74, frame + 1, 0, 1, 7, 8, 1, "polling", [3])]
            });
        }
        ReplayJournal compact = session.State.ReplayJournal!;
        Assert.Single(compact.Entries);
        Assert.Equal(1000, compact.Endpoint!.Repeat);
        Assert.Equal(Enumerable.Range(1, 1000).Select(frame => (long)frame), compact.EnumerateReceipts().Select(receipt => receipt.Frame));
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 1000)
        {
            Advanced = 1,
            ResultFrame = 1001,
            CommandCount = 6,
            CommandDigest = 1234UL,
            Receipts = [new ReplayReceipt(ReplayReceiptKind.Branch, 74, 1001, 0, 1, 7, 8, 0, "polling", [3])]
        });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Dialogue, SceneKind.Field, 74, 1001) { Code = 1, Value = 2, MessageId = 9 });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Dialogue, SceneKind.Field, 74, 1001) { Code = 1, Value = 2, MessageId = 9 });
        Assert.Equal(4, session.State.ReplayJournal!.EntryCount);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(session.State.ReplayJournal))!;
        Assert.Equal(1001, restored.EnumerateReceipts().Count());
        Assert.Equal(0, restored.EnumerateReceipts().Last().Value);
        Assert.Equal(4, restored.EntryCount);
    }

    [Fact]
    public void Movie_clocks_are_exogenous_transactions_and_nested_movie_completion_is_not_duplicated()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        session.RequestMovie(30);
        session.SetMoviePlayback(new MoviePlaybackState(2, 100, true, false, false));
        session.SetMoviePosition(0.067);
        Assert.Equal(2, session.State.ReplayJournal!.EntryCount);
        using (session.BeginReplayCapture())
        {
            session.CompleteMovie(true);
        }
        Assert.Equal(2, session.State.ReplayJournal.EntryCount);
        session.RequestMovie(30);
        session.CompleteMovie();
        Assert.Equal(ReplayEntryKind.MovieComplete, session.State.ReplayJournal.Entries[^1].Kind);
    }

    [Fact]
    public void Explicit_committed_prefix_omits_the_failed_suffix_without_rewriting_source_history()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 130, 375)
        {
            Advanced = 1,
            ResultFrame = 376,
            ResultScene = SceneKind.Field,
            ResultFieldId = 130,
            Receipts = [new ReplayReceipt(ReplayReceiptKind.Command, 130, 376, 9, 43, 300, 42, 512, Arguments: [30])]
        });
        ReplayJournal committed = session.State.ReplayJournal!;
        session.MarkReplayUnavailable("Unsupported transaction changed partial VM state after the committed boundary.");
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 130, 376));
        ReplayJournal source = session.State.ReplayJournal!;
        ReplayJournal prefix = source.GetCommittedPrefix();
        Assert.False(source.IsComplete);
        Assert.Equal("Unsupported transaction changed partial VM state after the committed boundary.", source.UnavailableReason);
        Assert.True(prefix.IsComplete);
        Assert.Null(prefix.UnavailableReason);
        Assert.Equal(source.SessionId, prefix.SessionId);
        Assert.Equal(committed.EntryCount, prefix.EntryCount);
        Assert.Equal(committed.EncodedByteCount, prefix.EncodedByteCount);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(committed.Entries), JsonSerializer.SerializeToElement(prefix.Entries)));
        Assert.Equal(376, prefix.Endpoint!.ResultFrame);
        Assert.Equal(SceneKind.Field, prefix.Endpoint.ResultScene);
        Assert.Equal(130, prefix.Endpoint.ResultFieldId);
        prefix.Endpoint.Receipts[0].Arguments![0] = 999;
        Assert.Equal(30, source.Endpoint!.Receipts[0].Arguments![0]);
        ReplayJournal continued = prefix.Append(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 130, 376) { Advanced = 1, ResultFrame = 377 });
        Assert.Equal(2, continued.EntryCount);
        Assert.Single(source.Entries);
        ReplayJournal transferred = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(source))!;
        Assert.False(transferred.IsComplete);
        Assert.Equal(376, transferred.GetCommittedPrefix().Endpoint!.ResultFrame);
    }

    [Fact]
    public void Explicit_empty_committed_prefix_preserves_the_actual_new_game_root_only()
    {
        GameSession legacy = new(new GameState());
        Assert.Null(legacy.State.ReplayJournal);
        GameSession session = new(new GameState());
        session.StartNewGame();
        ReplayJournal root = session.State.ReplayJournal!;
        Assert.Same(root, root.GetCommittedPrefix());
        session.MarkReplayUnavailable("The first initialization transaction failed before committing.");
        ReplayJournal prefix = session.State.ReplayJournal!.GetCommittedPrefix();
        Assert.True(prefix.IsComplete);
        Assert.Empty(prefix.Entries);
        Assert.Null(prefix.Endpoint);
        Assert.Equal(root.SessionId, prefix.SessionId);
        Assert.False(session.State.ReplayJournal.IsComplete);
    }

    [Fact]
    public void Result_dialogue_sites_split_scheduler_cadence_and_invalid_sites_are_refused()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        ReplayJournalEntry first = new(ReplayEntryKind.Step, SceneKind.Field, 74, 0) { Advanced = 1, ResultFrame = 1, ResultTrigger = 2, ResultMessageId = 1, ResultObjectId = 0, ResultScriptId = 1, ResultOperation = 5 };
        session.AppendReplayEntry(first);
        session.AppendReplayEntry(first with { Frame = 1, ResultFrame = 2, ResultOperation = 9 });
        Assert.Equal(2, session.ReplayJournal!.EntryCount);
        Assert.Throws<InvalidDataException>(() => session.AppendReplayEntry(first with { ResultMessageId = -2 }));
        Assert.Throws<InvalidDataException>(() => session.AppendReplayEntry(first with { Operation = -2 }));
        Assert.Throws<InvalidDataException>(() => session.AppendReplayEntry(first with { ResultTrigger = 99 }));
        session.AppendReplayEntry(first with { Frame = 2, ResultFrame = 3, ResultMessageId = -1 });
        Assert.Equal(-1, session.ReplayJournal.Endpoint!.ResultMessageId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":99}")]
    [InlineData("{\"Version\":1,\"Root\":\"Memoria.FF8.NewGame.causal.v1\",\"RandomSeed\":0,\"EntryCount\":0,\"Data\":\"invalid_base64\"}")]
    public void Malformed_capsules_use_the_normal_unavailable_save_error(string json)
    {
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<ReplayJournal>(json));
    }

    [Fact]
    public void Bounded_envelope_rejects_count_mismatch_and_overflow_without_changing_the_prefix()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        using JsonDocument capsule = JsonDocument.Parse(JsonSerializer.Serialize(session.ReplayJournal));
        Dictionary<string, JsonElement> members = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(capsule.RootElement.GetRawText())!;
        members["EntryCount"] = JsonSerializer.SerializeToElement(262144);
        InvalidDataException mismatched = Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(members)));
        Assert.Contains("count differs from its payload", mismatched.Message);
        members["EntryCount"] = JsonSerializer.SerializeToElement(262145);
        InvalidDataException oversized = Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(members)));
        Assert.Contains("exceeds its declared capacity", oversized.Message);
        Assert.Empty(session.ReplayJournal!.Entries);
        Assert.True(session.ReplayJournal.IsComplete);
    }

    [Fact]
    public void Completed_visits_keep_inputs_decisions_and_timing_runs_without_mutating_earlier_fine_history()
    {
        // Storage-only transactions exercise representation; they are not published as a game recreation fixture.
        GameSession session = new(new GameState());
        session.StartNewGame();
        for (int frame = 0; frame < 10; frame++)
        {
            ReplayPlayerPose pose = new(74, frame, 0, 0, 0, 0);
            session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, frame)
            {
                Code = 5,
                Advanced = 1,
                ResultScene = SceneKind.Field,
                ResultFieldId = 74,
                ResultFrame = frame + 1,
                StartPose = pose,
                EndPose = pose,
                CommandCount = frame + 1,
                CommandDigest = (ulong)frame + 1,
                Receipts = [new ReplayReceipt(ReplayReceiptKind.Branch, 74, frame, 0, 1, 2, Value: frame)]
            });
        }
        ReplayReceipt decision = new(ReplayReceiptKind.Dialogue, 74, 10, 0, 1, 3, 4, 2, Arguments: [2, 7]);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Dialogue, SceneKind.Field, 74, 10) { Code = 1, Value = 2, MessageId = 4, ObjectId = 0, ScriptId = 1, Operation = 3, Receipts = [decision] });
        ReplayJournal fine = session.ReplayJournal!;
        string original = JsonSerializer.Serialize(fine);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.FieldLoad, SceneKind.Field, 179, 0));
        ReplayJournal compact = session.ReplayJournal!;
        Assert.Equal(3, compact.EntryCount);
        ReplayJournalEntry run = compact.Entries[0];
        Assert.Equal(ReplayEntryKind.Action, run.Kind);
        Assert.Equal(5, run.Code);
        Assert.Equal(10, run.Repeat);
        Assert.Equal(10, run.ResultFrame);
        Assert.Empty(run.Receipts);
        Assert.Null(run.StartPose);
        Assert.Null(run.EndPose);
        ReplayJournalEntry retainedDecision = compact.Entries[1];
        Assert.Equal(2, retainedDecision.Value);
        Assert.Equal(3, retainedDecision.Operation);
        Assert.Equal(decision with { Arguments = null }, retainedDecision.Receipts[0] with { Arguments = null });
        Assert.Equal(decision.Arguments, retainedDecision.Receipts[0].Arguments);
        Assert.Equal(11, fine.EntryCount);
        Assert.Equal(original, JsonSerializer.Serialize(fine));
        Assert.NotNull(fine.Entries[0].StartPose);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(compact))!;
        Assert.Equal(compact.EncodedByteCount, restored.EncodedByteCount);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(compact.Entries), JsonSerializer.SerializeToElement(restored.Entries)));
        ReplayPlayerPose currentPose = new(179, 1, 2, 3, 4, 5);
        ReplayJournal continued = restored.Append(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 179, 0) { Advanced = 1, ResultFrame = 1, StartPose = currentPose, EndPose = currentPose, Receipts = [new ReplayReceipt(ReplayReceiptKind.Branch, 179, 1, 0, 1, 2)] });
        Assert.Equal(currentPose, continued.Endpoint!.StartPose);
        Assert.Single(continued.Endpoint.Receipts);
        Assert.Equal(3, compact.EntryCount);
    }

    [Fact]
    public void Sparse_storage_preserves_nondefault_values_and_requires_critical_input_identity()
    {
        GameSession session = new(new GameState());
        session.StartNewGame();
        ReplayJournalEntry entry = new(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 8, Value = 0x40, Horizontal = -0.46f, Vertical = 0.88f, Frames = 7, Advanced = 0, Accepted = false, Repeat = 2, Text = "input", Skipped = true, MovieSeconds = 1.25, ElapsedTicks = 1234 };
        session.AppendReplayEntry(entry);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(session.ReplayJournal))!;
        Assert.Equal(entry, restored.Endpoint);
        ReplayJournal signedZero = restored.Append(entry with { Frame = 1, Horizontal = -0f, MovieSeconds = -0d });
        ReplayJournal decodedZero = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(signedZero))!;
        Assert.Equal(BitConverter.SingleToInt32Bits(-0f), BitConverter.SingleToInt32Bits(decodedZero.Endpoint!.Horizontal));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(decodedZero.Endpoint.MovieSeconds));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplayJournalEntry>("{\"Kind\":1,\"Scene\":1,\"FieldId\":74}"));
    }
}
