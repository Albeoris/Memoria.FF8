using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private ReplayJournalArchive? journalArchive;
    private long lastJournalFlush = Stopwatch.GetTimestamp();

    private void PersistReplayJournal(ReplayJournal journal)
    {
        if (journalArchive is null)
        {
            return;
        }
        ReplayJournalEntry? endpoint = journal.Endpoint;
        if (endpoint?.Kind == ReplayEntryKind.FieldLoad || endpoint?.ResultFieldId != endpoint?.FieldId || Stopwatch.GetElapsedTime(lastJournalFlush).TotalSeconds >= 30 || !journal.IsComplete)
        {
            FlushReplayJournal(journal);
        }
    }

    private void FlushReplayJournal(ReplayJournal journal)
    {
        try
        {
            journalArchive?.Persist(journal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The complete in-memory capsule remains available. Explicit saving still requires successful durable pinning.
            Console.Error.WriteLine("Session journal flush deferred: " + exception.Message);
        }
        lastJournalFlush = Stopwatch.GetTimestamp();
    }

    /// <summary>Recomputes state under this engine from the save's authentic New Game causal history.</summary>
    public static GameState RecreateCommittedPrefix(GameState source, string gameDirectory, string? mediaToolsDirectory = null)
    {
        ReplayJournal journal = source.ReplayJournal ?? throw new NotSupportedException("This checkpoint has no game-recorded committed prefix.");
        ReplayJournal prefix = journal.GetCommittedPrefix();
        ReplayJournalEntry? endpoint = prefix.Endpoint;
        return RecreateSave(new GameState { Scene = endpoint?.ResultScene ?? SceneKind.Field, FieldId = endpoint?.ResultFieldId ?? 74, ReplayJournal = prefix }, gameDirectory, mediaToolsDirectory);
    }

    /// <summary>Recomputes a complete save; incomplete histories are never accepted as successful checkpoints.</summary>
    public static GameState RecreateSave(GameState source, string gameDirectory, string? mediaToolsDirectory = null)
    {
        ReplayJournal journal = source.ReplayJournal ?? throw new NotSupportedException("This save has no game-recorded history from New Game.");
        journal.Validate();
        if (!journal.IsComplete)
        {
            throw new NotSupportedException(journal.UnavailableReason);
        }
        var rebuilt = GameSession.CreateColdProcess();
        rebuilt.StartNewGame();
        var clock = new MovieClock(rebuilt, gameDirectory, mediaToolsDirectory);
        using var world = new GameWorld(rebuilt, gameDirectory, clock, persistReplayJournal: false);
        ReplayJournalEntry? movieSkip = null;
        world.Events.MovieSkipRequested -= clock.Skip;
        world.Events.MovieSkipRequested += () =>
        {
            MoviePlaybackState playback = movieSkip?.MoviePlayback ?? throw new NotSupportedException("The movie skip has no game-recorded terminal clock.");
            string hash = MovieClock.GetSourceHash(gameDirectory, rebuilt.State.MovieIndex);
            if (movieSkip.MovieSourceHash is null || movieSkip.MovieSourceHash != hash)
            {
                throw new InvalidDataException("The game-recorded movie clock does not match the installed clip.");
            }
            rebuilt.SetReplayMovieSourceHash(hash);
            rebuilt.SetMoviePlayback(playback with { IsPlaying = true, Completed = false, Skipped = false });
            rebuilt.CompleteMovie(true);
        };
        Queue<(int FieldId, string ScriptHash, string? ExecutableHash)> autonomousAllocations = new();
        int index = 0;
        foreach (ReplayJournalEntry entry in journal.Entries)
        {
            entry.Validate();
            for (int repeat = 0; repeat < entry.Repeat; repeat++)
            {
                bool passiveFieldCadence = entry.Scene is SceneKind.Field or SceneKind.Movie && (entry.Kind is ReplayEntryKind.Step or ReplayEntryKind.Update || entry.Kind == ReplayEntryKind.Action && entry.Code == (int)PlayerActionKind.Wait);
                bool currentFieldVm = rebuilt.State.Scene is SceneKind.Field or SceneKind.Movie;
                if (!passiveFieldCadence || !currentFieldVm)
                {
                    if (rebuilt.State.Scene != entry.Scene || rebuilt.State.FieldId != entry.FieldId)
                    {
                        throw new InvalidDataException($"Replay causal boundary diverged at entry {index}: expected {entry.Scene}/field{entry.FieldId}, got {rebuilt.State.Scene}/field{rebuilt.State.FieldId}.");
                    }
                }
                else if (entry.Kind is ReplayEntryKind.Step or ReplayEntryKind.Update && world.Events.FieldId != entry.FieldId)
                {
                    throw new InvalidDataException("Recorded field VM cadence does not own the actually allocated interpreter.");
                }
                else if (entry.Kind == ReplayEntryKind.Action && world.EnsureField())
                {
                    autonomousAllocations.Enqueue((world.Field!.Id, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(world.Field.Jsm)), world.assets.NativeExecutableHash));
                }
                PlayerActionResult? result = null;
                switch (entry.Kind)
                {
                    case ReplayEntryKind.FieldLoad:
                        if (autonomousAllocations.Count > 0)
                        {
                            var allocation = autonomousAllocations.Dequeue();
                            if (allocation.FieldId != entry.FieldId || allocation.ScriptHash != entry.NativeSourceHash || allocation.ExecutableHash != entry.Text)
                            {
                                throw new InvalidDataException("A current-code autonomous allocation does not match the next recorded native allocation barrier.");
                            }
                        }
                        else if (!world.EnsureField())
                        {
                            throw new InvalidDataException("A recorded field allocation did not perform a fresh load.");
                        }
                        if (world.Field is null || entry.NativeSourceHash != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(world.Field.Jsm)) || entry.Text != world.assets.NativeExecutableHash)
                        {
                            throw new InvalidDataException("Replay field bytes or native executable provenance differs from its game-recorded source.");
                        }
                        break;
                    case ReplayEntryKind.Step:
                        world.Events.Step();
                        world.PublishReplayDeparture();
                        break;
                    case ReplayEntryKind.Update:
                        world.Events.Update(TimeSpan.FromTicks(entry.ElapsedTicks));
                        world.PublishReplayDeparture();
                        break;
                    case ReplayEntryKind.Action:
                        ValidatePartyMenuDecisionSite(world.Events, entry);
                        AwaitRecordedDialogueSite(world.Events, entry, () =>
                        {
                            world.Events.Step();
                            world.PublishReplayDeparture();
                        });
                        ValidateDialogueSite(world.Events, entry);
                        movieSkip = entry.Code == (int)PlayerActionKind.SkipMovie ? entry : null;
                        result = world.Apply(new PlayerAction((PlayerActionKind)entry.Code, entry.Value, entry.Text, entry.Horizontal, entry.Vertical, entry.Frames, entry.Quantity));
                        break;
                    case ReplayEntryKind.Dialogue:
                        AwaitRecordedDialogueSite(world.Events, entry, () =>
                        {
                            world.Events.Step();
                            world.PublishReplayDeparture();
                        });
                        ValidateDialogueSite(world.Events, entry);
                        world.Events.ConfirmDialogue(entry.Code == 1 ? entry.Value : null, entry.Code == 2 ? entry.Text : null);
                        break;
                    case ReplayEntryKind.WorldAction:
                        result = world.Apply(new PlayerAction((PlayerActionKind)entry.Code, entry.Value, entry.Text, entry.Horizontal, entry.Vertical, entry.Frames));
                        break;
                    case ReplayEntryKind.MovieSample:
                        rebuilt.SetReplayMovieSourceHash(ValidateMovieSource(entry, rebuilt.State.MovieIndex, gameDirectory));
                        if (entry.MoviePlayback is MoviePlaybackState playback)
                        {
                            rebuilt.SetMoviePlayback(playback);
                        }
                        rebuilt.SetMoviePosition(entry.MovieSeconds);
                        break;
                    case ReplayEntryKind.MovieComplete:
                        rebuilt.SetReplayMovieSourceHash(ValidateMovieSource(entry, rebuilt.State.MovieIndex, gameDirectory));
                        rebuilt.CompleteMovie(entry.Skipped);
                        break;
                    default:
                        throw new NotSupportedException($"Replay transaction {entry.Kind} is not implemented.");
                }
                if (world.Events.LastFault is EventFault fault)
                {
                    throw new EventScriptException($"Replay entry {index}: {fault.Message}");
                }
                if (rebuilt.ReplayJournal?.IsComplete != true)
                {
                    throw new InvalidDataException(rebuilt.ReplayJournal?.UnavailableReason ?? "The rebuilt causal history became unavailable.");
                }
                ValidateDecisionReceipts(entry, rebuilt.ReplayJournal.Endpoint!);
                if (result is not null && result.Accepted != entry.Accepted)
                {
                    throw new InvalidDataException($"Replay action acceptance diverged at entry {index}.");
                }
                if (entry.ResultScene.GetValueOrDefault(entry.Scene) != entry.Scene || entry.ResultFieldId.GetValueOrDefault(entry.FieldId) != entry.FieldId)
                {
                    for (int callback = 0; rebuilt.State.Scene == entry.Scene && rebuilt.State.FieldId == entry.FieldId && callback < 30000; callback++)
                    {
                        if (world.Events.CurrentDialogue is not null || world.Events.LastFault is not null || world.NextTrigger.Kind is PlayerTriggerKind.Movie or PlayerTriggerKind.WorldMap or PlayerTriggerKind.FieldTransition)
                        {
                            break;
                        }
                        // The recorded consequence is a readiness barrier, never a requested destination.
                        world.Events.Step();
                        world.PublishReplayDeparture();
                    }
                }
                if ((!passiveFieldCadence || rebuilt.State.Scene is not (SceneKind.Field or SceneKind.Movie)) && (rebuilt.State.Scene != entry.ResultScene.GetValueOrDefault(entry.Scene) || rebuilt.State.FieldId != entry.ResultFieldId.GetValueOrDefault(entry.FieldId)))
                {
                    throw new InvalidDataException($"Replay progress diverged after entry {index}/{entry.Kind}: expected {entry.ResultScene.GetValueOrDefault(entry.Scene)}/field{entry.ResultFieldId.GetValueOrDefault(entry.FieldId)}, actual {rebuilt.State.Scene}/field{rebuilt.State.FieldId}/frame{world.Events.Frame}/trigger{world.NextTrigger.Kind}.");
                }
            }
            index++;
        }
        if (autonomousAllocations.Count != 0)
        {
            throw new InvalidDataException("Reconstruction ended before its autonomous native allocations were bound to recorded allocation barriers.");
        }
        if (rebuilt.State.Scene != source.Scene || rebuilt.State.FieldId != source.FieldId)
        {
            throw new InvalidDataException("The complete journal does not reach this save's checkpoint.");
        }
        ReplayJournalEntry? final = journal.Endpoint;
        if (final is not null && final.Kind is not (ReplayEntryKind.MovieSample or ReplayEntryKind.MovieComplete))
        {
            if ((int)world.NextTrigger.Kind != final.ResultTrigger)
            {
                throw new InvalidDataException($"Reconstruction did not reach the recorded checkpoint readiness boundary: expected {(PlayerTriggerKind)final.ResultTrigger}, actual {world.NextTrigger.Kind} at field{world.Events.FieldId}/frame{world.Events.Frame}.");
            }
            if (final.ResultMessageId is not null)
            {
                DialogueState? dialogue = world.Events.CurrentDialogue;
                if (dialogue is null || dialogue.MessageId != final.ResultMessageId || dialogue.ObjectId != final.ResultObjectId || dialogue.ScriptId != final.ResultScriptId || dialogue.Operation != final.ResultOperation)
                {
                    throw new InvalidDataException("Reconstruction did not reach the recorded final dialogue site.");
                }
            }
        }
        if (rebuilt.State.Scene == SceneKind.WorldMap)
        {
            // Promote the exact issuing VM before discarding its pending departure marker.
            // Capturing after world callbacks would mix the current global timer into a frozen field VM.
            world.EnsureField();
        }
        else if (world.Field is not null)
        {
            if (world.Events.FieldId != rebuilt.State.FieldId || world.Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
            {
                rebuilt.SetDepartingFieldState(world.Events.Capture());
            }
            else
            {
                rebuilt.SetEventState(world.Events.Capture());
            }
        }
        if (source.DialogueDraft is DialogueDraft draft)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            if (world.Events.FieldId != rebuilt.State.FieldId || dialogue is null || draft.Channel != dialogue.Channel || draft.MessageId != dialogue.MessageId)
            {
                throw new InvalidDataException("The saved pending input does not match the recreated dialogue endpoint.");
            }
            if (draft.EnteredName is null || dialogue.IsNamePrompt && (draft.EnteredName.Length > 12 || draft.EnteredName.Any(char.IsControl)) || dialogue.FirstChoice is int first && (dialogue.LastChoice is not int last || draft.SelectedChoice < first || draft.SelectedChoice > last))
            {
                throw new InvalidDataException("The saved pending input exceeds the recreated dialogue's name or choice constraints.");
            }
            rebuilt.SetDialogueDraft(draft);
        }
        return rebuilt.State;
    }

    internal static void AwaitRecordedDialogueSite(EventEngine engine, ReplayJournalEntry entry, Action advance)
    {
        if (entry.MessageId is null || entry.Kind == ReplayEntryKind.Action && (PlayerActionKind)entry.Code is not (PlayerActionKind.Confirm or PlayerActionKind.Choose or PlayerActionKind.EnterName or PlayerActionKind.Cancel))
        {
            return;
        }
        // A recorded decision owns its visible script site, not the old interpreter's callback latency.
        // Only real autonomous callbacks run here; no input, answer or snapshot value is injected.
        for (int callback = 0; engine.CurrentDialogue is null || !engine.CurrentDialogue.CanConfirm; callback++)
        {
            if (callback == 30000 || engine.FieldId != entry.FieldId || engine.LastFault is not null || engine.NextTrigger.Kind is PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap or PlayerTriggerKind.Movie)
            {
                throw new InvalidDataException($"The recorded dialogue decision cannot mature across a fault, scene transition or unbounded autonomous wait: expected field{entry.FieldId}/message{entry.MessageId}/object{entry.ObjectId}/script{entry.ScriptId}/operation{entry.Operation}, actual field{engine.FieldId}/frame{engine.Frame}/trigger{engine.NextTrigger}/fault{engine.LastFault}, callbacks{callback}.");
            }
            advance();
        }
        if (engine.FieldId != entry.FieldId)
        {
            throw new InvalidDataException("The recorded dialogue decision matured in a different field.");
        }
        ValidateDialogueSite(engine, entry);
    }

    private static void ValidatePartyMenuDecisionSite(EventEngine engine, ReplayJournalEntry entry)
    {
        bool decision = entry.Code is (int)PlayerActionKind.SelectPartySlot or (int)PlayerActionKind.SelectPartyMember or (int)PlayerActionKind.BuyShopItem || entry.Code is (int)PlayerActionKind.Confirm or (int)PlayerActionKind.Cancel && entry.Trigger == (int)PlayerTriggerKind.PartyMenu;
        if (!decision || entry.Trigger != (int)PlayerTriggerKind.PartyMenu && entry.Accepted == false)
        {
            return;
        }
        NativePartyMenuState? menu = engine.PartyMenu;
        if (entry.MessageId is not null || menu is null || entry.Accepted && menu.Phase != NativePartyMenuPhase.Active || engine.FieldId != entry.FieldId || menu.ObjectId != entry.ObjectId || menu.ScriptId != entry.ScriptId || menu.Operation != entry.Operation || menu.ProgramHash != entry.NativeSourceHash)
        {
            throw new InvalidDataException("Recorded menu decision no longer matches its actual native request site and active menu.");
        }
    }

    private static void ValidateDialogueSite(EventEngine engine, ReplayJournalEntry entry)
    {
        if (entry.MessageId is null || entry.Kind == ReplayEntryKind.Action && (PlayerActionKind)entry.Code is not (PlayerActionKind.Confirm or PlayerActionKind.Choose or PlayerActionKind.EnterName or PlayerActionKind.Cancel))
        {
            return;
        }
        DialogueState? dialogue = engine.CurrentDialogue;
        if (dialogue is null || dialogue.MessageId != entry.MessageId || dialogue.ObjectId != entry.ObjectId || dialogue.ScriptId != entry.ScriptId || dialogue.Operation != entry.Operation)
        {
            throw new InvalidDataException($"Replay decision no longer matches its original dialogue/script site: expected {entry.MessageId}/{entry.ObjectId}/{entry.ScriptId}/{entry.Operation}, actual {dialogue?.MessageId}/{dialogue?.ObjectId}/{dialogue?.ScriptId}/{dialogue?.Operation} at field{engine.FieldId}/frame{engine.Frame}, open[{string.Join("; ", engine.Dialogues.Select(window => $"channel{window.Channel}/{window.MessageId}/{window.ObjectId}/{window.ScriptId}/{window.Operation}/confirm{window.CanConfirm}"))}].");
        }
    }

    private void PublishReplayDeparture()
    {
        if (Events.FieldId != session.State.FieldId || Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition || session.State.Scene == SceneKind.WorldMap)
        {
            session.SetDepartingFieldState(Events.Capture());
        }
    }

    private static void ValidateDecisionReceipts(ReplayJournalEntry expected, ReplayJournalEntry actual)
    {
        // Values and branches are recomputed by the current VM. Player decisions must retain their causal sites and payloads.
        ReplayReceipt[] original = expected.Receipts.Where(receipt => receipt.Kind == ReplayReceiptKind.Dialogue).ToArray();
        ReplayReceipt[] rebuilt = actual.Receipts.Where(receipt => receipt.Kind == ReplayReceiptKind.Dialogue).ToArray();
        if (original.Length != rebuilt.Length || original.Where((receipt, index) => receipt with { Frame = 0, Arguments = null } != rebuilt[index] with { Frame = 0, Arguments = null } || (receipt.Arguments is null) != (rebuilt[index].Arguments is null) || receipt.Arguments is not null && !receipt.Arguments.SequenceEqual(rebuilt[index].Arguments!)).Any())
        {
            throw new InvalidDataException("Recreated dialogue outcomes differ from the game-recorded decisions.");
        }
    }

    private static string ValidateMovieSource(ReplayJournalEntry entry, int index, string gameDirectory)
    {
        string hash = MovieClock.GetSourceHash(gameDirectory, index);
        if (entry.MovieSourceHash is null || entry.MovieSourceHash != hash)
        {
            throw new InvalidDataException("The recorded movie sample belongs to another installed clip.");
        }
        return hash;
    }
}
