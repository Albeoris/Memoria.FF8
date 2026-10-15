using System.IO.Compression;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ReplaySemanticJournalTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Completed_visit_retains_admissions_decisions_poll_and_media_without_physical_cadence()
    {
        GameSession session = Start();
        session.AppendReplayEntry(Load(74));
        ReplaySemanticTrigger admission = new(1, ReplaySemanticTriggerKind.PushContact, 74, Hash, 4, 4, 8, 6, 2, Sequence: 3);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 4, Horizontal = 1, Advanced = 1, SemanticAdmissions = [admission], Receipts = [new ReplayReceipt(ReplayReceiptKind.InputSample, 74, 2, 4, 8, 20, 333, 128, Arguments: [128, 128, 128, 128, 0], Sequence: 1), new ReplayReceipt(ReplayReceiptKind.PersistentEffect, 74, 2, -1, -1, -1, (int)ReplayPersistentEffectKind.SalaryProgress, Arguments: [25, 1], Sequence: 2)] });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 2) { Code = 1, Value = 2, MessageId = 8, ObjectId = 4, ScriptId = 8, Operation = 21 });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.MovieSample, SceneKind.Field, 74, 3) { MovieSeconds = 2, MovieSourceHash = Hash });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 4) { Advanced = 1 });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.SemanticTrigger, SceneKind.Field, 74, 5) { SemanticTrigger = admission, SemanticAdmissions = [admission with { Sequence = 0 }] });
        ReplayJournal before = session.ReplayJournal!;
        session.AppendReplayEntry(Load(74));
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 4, Horizontal = -1 });
        ReplayJournal journal = session.ReplayJournal!;
        ReplayCompletedVisit visit = Assert.Single(journal.CompletedVisits);
        Assert.Equal(0, visit.Ordinal);
        Assert.Equal(74, visit.Exit.FieldId);
        Assert.Equal(Hash, visit.EntryFieldLoad!.NativeSourceHash);
        Assert.Equal(new[] { ReplayEntryKind.InputSample, ReplayEntryKind.PersistentEffect, ReplayEntryKind.SemanticTrigger, ReplayEntryKind.Action, ReplayEntryKind.MovieSample, ReplayEntryKind.SemanticTrigger }, visit.Intents.Select(entry => entry.Kind));
        Assert.Equal(admission, visit.Intents[2].SemanticTrigger);
        Assert.Equal(2, visit.Intents.Count(entry => entry.Kind == ReplayEntryKind.SemanticTrigger));
        Assert.Equal(128, visit.Intents[0].Value);
        Assert.Equal(20, visit.Intents[0].ScriptGoal!.Operation);
        Assert.Equal(2, visit.Intents[3].Value);
        Assert.All(visit.Intents, entry =>
        {
            Assert.Null(entry.StartPose);
            Assert.Empty(entry.Navigation);
            Assert.Equal(0, entry.Horizontal);
        });
        Assert.Equal(-1, journal.ActiveEntries[^1].Horizontal);
        Assert.Empty(before.CompletedVisits);
        GameSession regenerated = Start();
        regenerated.AppendReplayEntry(Load(74));
        foreach (ReplayJournalEntry intent in visit.Intents)
        {
            regenerated.AppendReplayEntry(intent);
        }
        regenerated.AppendReplayEntry(Load(74));
        ReplayCompletedVisit secondGeneration = Assert.Single(regenerated.ReplayJournal!.CompletedVisits);
        Assert.Equal(visit.Intents.Select(entry => entry.Kind), secondGeneration.Intents.Select(entry => entry.Kind));
        Assert.Equal(new[] { 25, 1 }, Assert.Single(secondGeneration.Intents.Single(entry => entry.Kind == ReplayEntryKind.PersistentEffect).Receipts).Arguments);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(journal))!;
        Assert.Equal(2, restored.Version);
        Assert.Single(restored.CompletedVisits);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(journal.Entries), JsonSerializer.SerializeToElement(restored.Entries)));
        ReplayJournal prefix = restored.MarkUnavailable("failed suffix").GetCommittedPrefix();
        Assert.Single(prefix.CompletedVisits);
        Assert.Equal(-1, prefix.ActiveEntries[^1].Horizontal);
    }

    [Fact]
    public void Legacy_capsule_remains_literal_while_projection_labels_producer_hints_without_written_values()
    {
        ReplayJournalEntry[] literal = [Load(74), new(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 8, Value = 4096, Frames = 30 }, new(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 4, Horizontal = 1, Receipts = [new ReplayReceipt(ReplayReceiptKind.Command, 74, 2, 4, 8, 20, 17, 999)] }, Load(75), new(ReplayEntryKind.Action, SceneKind.Field, 75, 1) { Code = 4, Vertical = 1 }];
        ReplayJournal legacy = ReadLegacy(literal);
        string original = JsonSerializer.Serialize(legacy);
        Assert.Equal(1, legacy.Version);
        Assert.Equal(1, legacy.Entries[2].Horizontal);
        ReplayJournal semantic = legacy.GetSemanticProjection();
        ReplayCompletedVisit visit = Assert.Single(semantic.CompletedVisits);
        Assert.True(visit.Inferred);
        Assert.Equal(new[] { ReplayEntryKind.Action, ReplayEntryKind.ScriptGoal }, visit.Intents.Select(entry => entry.Kind));
        Assert.Equal(8, visit.Intents[0].Code);
        Assert.Equal(4096, visit.Intents[0].Value);
        Assert.Equal(1, visit.Intents[0].Frames);
        ReplayJournalEntry goal = visit.Intents[1];
        Assert.Equal(ReplayEntryKind.ScriptGoal, goal.Kind);
        Assert.True(goal.ScriptGoal!.Inferred);
        Assert.Equal(20, goal.ScriptGoal.Operation);
        Assert.Equal(0, goal.Value);
        Assert.Equal(1, semantic.ActiveEntries[^1].Vertical);
        Assert.Equal(original, JsonSerializer.Serialize(legacy));
        Assert.Equal(1, JsonSerializer.Deserialize<ReplayJournal>(original)!.Version);
    }

    [Fact]
    public void Legacy_interaction_keeps_only_incoming_direction_with_codec_and_visit_boundaries()
    {
        ReplayJournalEntry interaction = new(ReplayEntryKind.Action, SceneKind.Field, 129, 20) { Code = 3, Value = 9 };
        ReplayJournalEntry movement = new(ReplayEntryKind.Action, SceneKind.Field, 129, 1) { Code = 4, Horizontal = 0.6f, Vertical = -0.8f, Frames = 19, Advanced = 19 };
        ReplayJournal legacy = ReadLegacy([Load(129), movement, new(ReplayEntryKind.Action, SceneKind.Field, 129, 20) { Code = 5, Frames = 8, Advanced = 8 }, movement with { Horizontal = 0, Vertical = 0 }, movement with { Horizontal = -1, Vertical = 0, Accepted = false }, interaction, Load(130), interaction with { FieldId = 130 }, Load(131)]);
        string original = JsonSerializer.Serialize(legacy);
        ReplayJournal projected = legacy.GetSemanticProjection();
        ReplayJournalEntry retained = Assert.Single(projected.CompletedVisits[0].Intents);
        ReplayActorApproach approach = Assert.IsType<ReplayActorApproach>(retained.Approach);
        Assert.True(approach.Inferred);
        Assert.Equal(0.6f, approach.Horizontal, 5);
        Assert.Equal(-0.8f, approach.Vertical, 5);
        Assert.Equal(0, retained.Horizontal);
        Assert.Equal(0, retained.Vertical);
        Assert.Equal(1, retained.Frames);
        Assert.Null(Assert.Single(projected.CompletedVisits[1].Intents).Approach);
        ReplayJournal differentCadence = ReadLegacy([Load(129), movement with { Frame = 99, Frames = 1, Advanced = 1 }, interaction with { Frame = 100 }, Load(130)]).GetSemanticProjection();
        Assert.Equal(approach, Assert.Single(differentCadence.CompletedVisits[0].Intents).Approach);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(projected))!;
        Assert.Equal(approach, Assert.Single(restored.CompletedVisits[0].Intents).Approach);
        Assert.True(Assert.Single(restored.CompletedVisits[0].Intents).Approach!.Inferred);
        Assert.Equal(original, JsonSerializer.Serialize(legacy));
        GameSession current = Start();
        current.AppendReplayEntry(Load(129));
        ReplaySemanticTrigger admission = new(1, ReplaySemanticTriggerKind.ActorInteraction, 129, Hash, 4, 4, 8, 6, 2, Approach: approach with { Inferred = false });
        current.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.SemanticTrigger, SceneKind.Field, 129, 1) { SemanticTrigger = admission, SemanticAdmissions = [admission with { Approach = new ReplayActorApproach(1, 0), Sequence = 0 }] });
        current.AppendReplayEntry(Load(130));
        ReplayJournal currentRestored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(current.ReplayJournal))!;
        Assert.Equal(admission, Assert.Single(currentRestored.CompletedVisits[0].Intents).SemanticTrigger);
        Assert.False(Assert.Single(currentRestored.CompletedVisits[0].Intents).SemanticTrigger!.Approach!.Inferred);
        Assert.Throws<InvalidDataException>(() => new ReplayActorApproach(float.NaN, 0).Validate());
        Assert.Throws<InvalidDataException>(() => new ReplayActorApproach(0, 0).Validate());
        Assert.Throws<InvalidDataException>(() => new ReplayActorApproach(1, 1).Validate());
    }

    [Fact]
    public void Input_projection_keeps_fresh_mapped_pulse_and_one_release_but_omits_idle_zero_polls()
    {
        GameSession session = Start();
        int field = 74;
        long frame = 1;
        int priorRaw = 0;
        session.AppendReplayEntry(Load(field));
        Sample(0, 0);
        Sample(0, 0);
        Sample(128, 0);
        Sample(0, 0);
        Sample(0, 0);
        Sample(0, 0x40);
        Sample(0, 0);
        Sample(256, 0);
        field = 75;
        session.AppendReplayEntry(Load(field));
        Sample(0, 0);
        Sample(0, 0);
        session.AppendReplayEntry(Load(76));
        ReplayCompletedVisit first = session.ReplayJournal!.CompletedVisits[0];
        Assert.All(first.Intents, entry => Assert.Equal(ReplayEntryKind.InputSample, entry.Kind));
        Assert.Equal(new[] { 128, 0, 0, 256 }, first.Intents.Select(entry => entry.Value));
        Assert.Equal(new ushort[] { 0, 0, 0x40, 0 }, first.Intents.Select(entry => entry.MappedPulse));
        ReplayJournalEntry release = Assert.Single(session.ReplayJournal.CompletedVisits[1].Intents);
        Assert.Equal(0, release.Value);
        Assert.Equal((ushort)0, release.MappedPulse);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(session.ReplayJournal))!;
        Assert.Equal((ushort)0x40, restored.CompletedVisits[0].Intents[2].MappedPulse);
        Assert.Throws<InvalidDataException>(() => (first.Intents[2] with { MappedPulse = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, 74, 1) { MappedPulse = 0x40 }).Validate());
        field = 76;
        ReplayScriptGoal producer = new(field, Hash, Hash, 0, 1, 10, 39, false);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.NativeTraversal, SceneKind.Field, field, frame) { NativeTraversal = new ReplayNativeTraversal(1, producer, 4, true, false) });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.InputPulse, SceneKind.Field, field, frame) { ScriptGoal = producer, MappedPulse = 0x40 });
        Sample(0, 0);
        Sample(0, 0);
        session.AppendReplayEntry(Load(77));
        Assert.Equal(new[] { ReplayEntryKind.NativeTraversal, ReplayEntryKind.InputSample }, session.ReplayJournal!.CompletedVisits[2].Intents.Select(entry => entry.Kind));
        Assert.Throws<InvalidDataException>(() => (new ReplayJournalEntry(ReplayEntryKind.InputPulse, SceneKind.Field, 76, 1) { ScriptGoal = producer }).Validate());

        void Sample(int physical, ushort pulse)
        {
            int raw = physical | pulse;
            int pressed = raw & ~priorRaw;
            priorRaw = raw;
            ReplayReceipt receipt = new(ReplayReceiptKind.InputSample, field, frame, 0, 1, 20, 333, physical, Arguments: [raw, pressed, raw, pressed, pulse]);
            session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Step, SceneKind.Field, field, frame++) { Advanced = 1, Receipts = [receipt] });
        }
    }

    [Fact]
    public void Native_traversal_completion_retains_current_producer_and_direction_without_old_hold_cadence()
    {
        GameSession session = Start();
        session.AppendReplayEntry(Load(809));
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 809, 3) { Code = 8, Value = 4096 });
        const int ladderUp2 = 39;
        ReplayReceipt completion = new(ReplayReceiptKind.NativeTraversal, 809, 473, 0, 7, 65, ladderUp2, 1, Arguments: [4, 1, 4096], Sequence: 0);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 809, 4) { Code = 5, Frames = 470, Advanced = 470, Receipts = [completion] });
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 809, 474) { Code = 8 });
        session.AppendReplayEntry(Load(810));
        ReplayJournalEntry intent = Assert.Single(Assert.Single(session.ReplayJournal!.CompletedVisits).Intents);
        Assert.Equal(ReplayEntryKind.NativeTraversal, intent.Kind);
        ReplayNativeTraversal traversal = Assert.IsType<ReplayNativeTraversal>(intent.NativeTraversal);
        Assert.Equal((byte)4, traversal.Mode);
        Assert.True(traversal.Forward);
        Assert.False(traversal.Inferred);
        Assert.Equal(new ReplayScriptGoal(809, Hash, Hash, 0, 7, 65, ladderUp2, false), traversal.Producer);
        Assert.Equal(1, intent.Frames);
        Assert.Equal(0, intent.Advanced);
        Assert.Empty(intent.Receipts);
        GameSession next = Start();
        next.AppendReplayEntry(Load(809));
        next.AppendReplayEntry(intent with { Receipts = [completion with { Frame = 50 }] });
        next.AppendReplayEntry(Load(810));
        Assert.Equal(traversal, Assert.Single(Assert.Single(next.ReplayJournal!.CompletedVisits).Intents).NativeTraversal);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(next.ReplayJournal))!;
        Assert.Equal(traversal, Assert.Single(Assert.Single(restored.CompletedVisits).Intents).NativeTraversal);
    }

    [Fact]
    public void Actual_allocation_fences_enclosing_input_and_missing_effect_owner_refuses_atomically()
    {
        GameSession session = Start();
        session.AppendReplayEntry(Load(74));
        GameState before = session.State;
        WorldTransition packet = new(0, 0, 0);
        WorldState initial = new() { NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(before.SavedWorld!) };
        Assert.Throws<NotSupportedException>(() => session.CommitPreparedWorldEntry(packet, initial, before.WorldControls!, _ => { }));
        Assert.Same(before, session.State);
        using (session.BeginReplayCapture())
        {
            session.CommitPreparedWorldEntry(packet, initial, before.WorldControls!, _ => { }, allocation: new ReplayWorldAllocation(1, packet, Hash, Hash));
            Assert.Empty(session.ReplayJournal!.CompletedVisits);
            session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 3, Value = 4 });
        }
        ReplayCompletedVisit field = Assert.Single(session.ReplayJournal!.CompletedVisits);
        Assert.Equal(3, Assert.Single(field.Intents).Code);
        Assert.Equal(ReplayEntryKind.WorldAllocation, Assert.Single(session.ReplayJournal.ActiveEntries).Kind);
        ReplayWorldEffectCause cause = new(0, 10, 80, null, 2, true);
        ReplayJournalEntry input = new(ReplayEntryKind.PersistentEffect, SceneKind.WorldMap, 74, 0) { Code = (int)ReplayPersistentEffectKind.WorldCallbackEffects, Frames = 3, EffectArguments = cause.ToArguments() };
        GameState allocated = session.State;
        Assert.Throws<NotSupportedException>(() => session.ApplySemanticWorldEffect(input, (_, _, _) => throw new InvalidOperationException("The declared allocation has no character-vitals producer.")));
        Assert.Same(allocated, session.State);
        Assert.Same(initial, session.State.World);
        Assert.Equal(0, session.State.World!.Frame);
        Assert.Equal(0, session.State.WorldProcess!.MainLoopCallbacks);
        Assert.Equal(before.ExamTimer!.ClockCalls, session.State.ExamTimer!.ClockCalls);
        GameState after = session.State;
        Assert.Throws<NotSupportedException>(() => session.ApplySemanticWorldEffect(input with { Code = (int)ReplayPersistentEffectKind.WorldBattle }, (_, _, _) => throw new InvalidOperationException()));
        Assert.Same(after, session.State);
        Assert.Equal(ReplayEntryKind.WorldAllocation, JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(session.ReplayJournal))!.ActiveEntries[0].Kind);
    }

    private static GameSession Start()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        return session;
    }

    private static ReplayJournalEntry Load(int field)
    {
        return new ReplayJournalEntry(ReplayEntryKind.FieldLoad, SceneKind.Field, field, 0) { NativeSourceHash = Hash, Text = Hash };
    }

    private static ReplayJournal ReadLegacy(ReplayJournalEntry[] entries)
    {
        using MemoryStream compressed = new();
        using (BrotliStream stream = new(compressed, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(stream, entries);
        }
        string capsule = JsonSerializer.Serialize(new { Version = 1, Root = ReplayJournal.NewGameRoot, RandomSeed = 0, SessionId = "0123456789abcdef0123456789abcdef", EntryCount = entries.Length, IsComplete = true, Data = Convert.ToBase64String(compressed.ToArray()) });
        return JsonSerializer.Deserialize<ReplayJournal>(capsule)!;
    }
}
