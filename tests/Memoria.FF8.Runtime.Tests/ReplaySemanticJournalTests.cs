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
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 4, Horizontal = 1, Advanced = 1, SemanticAdmissions = [admission], Receipts = [new ReplayReceipt(ReplayReceiptKind.InputSample, 74, 2, 4, 8, 20, 333, 128, Sequence: 1), new ReplayReceipt(ReplayReceiptKind.PersistentEffect, 74, 2, -1, -1, -1, (int)ReplayPersistentEffectKind.SalaryProgress, Arguments: [25, 1], Sequence: 2)] });
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
        Assert.All(visit.Intents, entry => { Assert.Null(entry.StartPose); Assert.Empty(entry.Navigation); Assert.Equal(0, entry.Horizontal); });
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
        ReplayJournalEntry[] literal = [Load(74), new(ReplayEntryKind.Action, SceneKind.Field, 74, 1) { Code = 4, Horizontal = 1, Receipts = [new ReplayReceipt(ReplayReceiptKind.Command, 74, 2, 4, 8, 20, 17, 999)] }, Load(75), new(ReplayEntryKind.Action, SceneKind.Field, 75, 1) { Code = 4, Vertical = 1 }];
        ReplayJournal legacy = ReadLegacy(literal);
        string original = JsonSerializer.Serialize(legacy);
        Assert.Equal(1, legacy.Version);
        Assert.Equal(1, legacy.Entries[1].Horizontal);
        ReplayJournal semantic = legacy.GetSemanticProjection();
        ReplayCompletedVisit visit = Assert.Single(semantic.CompletedVisits);
        Assert.True(visit.Inferred);
        ReplayJournalEntry goal = Assert.Single(visit.Intents);
        Assert.Equal(ReplayEntryKind.ScriptGoal, goal.Kind);
        Assert.True(goal.ScriptGoal!.Inferred);
        Assert.Equal(20, goal.ScriptGoal.Operation);
        Assert.Equal(0, goal.Value);
        Assert.Equal(1, semantic.ActiveEntries[^1].Vertical);
        Assert.Equal(original, JsonSerializer.Serialize(legacy));
        Assert.Equal(1, JsonSerializer.Deserialize<ReplayJournal>(original)!.Version);
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
