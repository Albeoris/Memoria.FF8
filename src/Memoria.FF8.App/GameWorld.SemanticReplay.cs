using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static GameState RecreateSemanticSave(GameState source, string gameDirectory, string? mediaToolsDirectory)
    {
        ReplayJournal original = source.ReplayJournal ?? throw new NotSupportedException("This save has no authentic game-owned journal.");
        original.Validate();
        if (!original.IsComplete)
        {
            throw new NotSupportedException(original.UnavailableReason);
        }
        ReplayJournal plan = original.GetSemanticProjection();
        GameSession rebuilt = GameSession.CreateColdProcess();
        rebuilt.StartNewGame();
        using GameWorld world = new(rebuilt, gameDirectory, new MovieClock(rebuilt, gameDirectory, mediaToolsDirectory), persistReplayJournal: false);
        SemanticRecreation execution = new(world, rebuilt, gameDirectory);
        foreach (ReplayCompletedVisit visit in plan.CompletedVisits)
        {
            execution.RecreateVisit(visit);
        }
        execution.RecreateActiveVisit(plan.ActiveEntries);
        if (rebuilt.State.Scene == SceneKind.WorldMap)
        {
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
            DialogueState? dialogue = world.CurrentDialogue;
            if (dialogue is null || dialogue.Channel != draft.Channel || dialogue.MessageId != draft.MessageId)
            {
                throw new InvalidDataException("The saved pending input does not match the current semantic endpoint.");
            }
            if (draft.EnteredName is null || dialogue.IsNamePrompt && (draft.EnteredName.Length > 12 || draft.EnteredName.Any(char.IsControl)) || dialogue.FirstChoice is int first && (dialogue.LastChoice is not int last || draft.SelectedChoice < first || draft.SelectedChoice > last))
            {
                throw new InvalidDataException("The saved pending input exceeds the current name or choice constraints.");
            }
            rebuilt.SetDialogueDraft(draft);
        }
        return rebuilt.State;
    }

    private sealed class SemanticRecreation
    {
        private const int MaximumReadinessCallbacks = 30000;
        private readonly GameWorld world;
        private readonly GameSession session;
        private readonly string gameDirectory;
        private readonly HashSet<(int ObjectId, int ScriptId)> knownProducers = [];
        private readonly List<ReplaySemanticTrigger> admissions = [];
        private readonly HashSet<int> consumedAdmissions = [];
        private readonly List<ReplayReceipt> receipts = [];
        private readonly HashSet<int> consumedReceipts = [];
        private string? movieHash;
        private bool movieFinished;
        private int movieCallbackFrame = -1;
        private bool inferredVisit;
        private ReplayCompletedVisit? currentVisit;

        public SemanticRecreation(GameWorld world, GameSession session, string gameDirectory)
        {
            this.world = world;
            this.session = session;
            this.gameDirectory = gameDirectory;
            world.Events.CommandExecuted += command => knownProducers.Add((command.ObjectId, command.ScriptId));
            world.Events.SemanticTriggerAdmitted += trigger => admissions.Add(trigger);
            world.Events.CausalReceiptRecorded += receipt =>
            {
                receipts.Add(receipt);
                if (receipt.Kind is ReplayReceiptKind.Command or ReplayReceiptKind.InputSample or ReplayReceiptKind.NativeTraversal)
                {
                    knownProducers.Add((receipt.ObjectId, receipt.ScriptId));
                }
            };
        }

        public void RecreateVisit(ReplayCompletedVisit visit)
        {
            currentVisit = visit;
            inferredVisit = visit.Inferred;
            admissions.Clear();
            knownProducers.Clear();
            consumedAdmissions.Clear();
            receipts.Clear();
            consumedReceipts.Clear();
            EnsureAllocation(visit.EntryFieldLoad, visit.Scene, visit.FieldId);
            for (int index = 0; index < visit.Intents.Length; index++)
            {
                ReplayJournalEntry intent = visit.Intents[index];
                try
                {
                    if (HasDepartedCurrentVisit())
                    {
                        ValidateDepartedVisitRemainder(visit, index);
                        break;
                    }
                    ApplyIntent(intent, false);
                }
                catch (SemanticVisitDepartureException)
                {
                    ValidateDepartedVisitRemainder(visit, index);
                    break;
                }
                catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or EventScriptException)
                {
                    throw new InvalidDataException($"Semantic visit{visit.Ordinal} {visit.Scene}/{visit.FieldId} intent{index}/{intent.Kind} producer{intent.ScriptGoal?.ObjectId}/{intent.ScriptGoal?.ScriptId}/{intent.ScriptGoal?.Operation}, current{session.State.Scene}/{session.State.FieldId}/frame{world.Events.Frame}: {exception.Message}", exception);
                }
            }
            if (visit.Scene == SceneKind.WorldMap)
            {
                if (session.State.Scene == SceneKind.WorldMap)
                {
                    world.CompleteSemanticWorldVisit(visit);
                }
            }
            else
            {
                try
                {
                    CompleteFieldVisit(visit);
                }
                catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or EventScriptException)
                {
                    throw new InvalidDataException($"Semantic visit{visit.Ordinal} Field/{visit.FieldId} departure→{visit.Exit.Scene}/{visit.Exit.FieldId}, current{session.State.Scene}/{session.State.FieldId}/frame{world.Events.Frame}: {exception.Message}", exception);
                }
            }
            if (session.State.Scene != visit.Exit.Scene || session.State.FieldId != visit.Exit.FieldId)
            {
                throw new InvalidDataException($"Semantic visit {visit.Ordinal} did not reach its next recorded location: expected {visit.Exit.Scene}/{visit.Exit.FieldId}, actual {session.State.Scene}/{session.State.FieldId}.");
            }
            currentVisit = null;
        }

        private bool HasDepartedCurrentVisit()
        {
            return currentVisit is { Scene: not SceneKind.WorldMap } visit && (session.State.FieldId != visit.FieldId || session.State.Scene == SceneKind.WorldMap || world.Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition);
        }

        private void ValidateDepartedVisitRemainder(ReplayCompletedVisit visit, int index)
        {
            if (session.State.Scene != visit.Exit.Scene || session.State.FieldId != visit.Exit.FieldId)
            {
                throw new InvalidDataException($"The current native event departed to {session.State.Scene}/{session.State.FieldId} instead of the retained next location {visit.Exit.Scene}/{visit.Exit.FieldId}.");
            }
            if (visit.Intents.Skip(index).Any(entry => entry.Kind != ReplayEntryKind.ScriptGoal))
            {
                throw new InvalidDataException("The current native event departed before a retained external input could be applied.");
            }
        }

        private sealed class SemanticVisitDepartureException : Exception;

        public void RecreateActiveVisit(IReadOnlyList<ReplayJournalEntry> entries)
        {
            inferredVisit = false;
            foreach (ReplayJournalEntry entry in entries)
            {
                if (entry.Kind is ReplayEntryKind.FieldLoad or ReplayEntryKind.WorldAllocation)
                {
                    admissions.Clear();
                    knownProducers.Clear();
                    consumedAdmissions.Clear();
                    receipts.Clear();
                    consumedReceipts.Clear();
                    EnsureAllocation(entry, entry.Scene, entry.FieldId);
                    continue;
                }
                for (int repeat = 0; repeat < entry.Repeat; repeat++)
                {
                    ApplyIntent(entry, true);
                }
            }
        }

        private void EnsureAllocation(ReplayJournalEntry? allocation, SceneKind scene, int fieldId)
        {
            world.EnsureField();
            if (session.State.FieldId != fieldId || session.State.Scene != scene && !(scene == SceneKind.Field && session.State.Scene == SceneKind.Movie))
            {
                throw new InvalidDataException($"Semantic allocation differs from the actual engine transition: expected {scene}/{fieldId}, actual {session.State.Scene}/{session.State.FieldId}.");
            }
            if (scene == SceneKind.WorldMap)
            {
                if (allocation?.WorldAllocation is { } native && (native.ProgramHash != world.worldScene?.AllocationProgramSha256 || native.ExecutableHash != world.assets.NativeExecutableHash || native.Packet != session.State.WorldArrival))
                {
                    throw new InvalidDataException("Semantic world allocation differs from its actual current native resources or packet.");
                }
            }
            else if (allocation is not null && (world.Field is null || allocation.NativeSourceHash != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(world.Field.Jsm)) || allocation.Text != world.assets.NativeExecutableHash))
            {
                throw new InvalidDataException("Semantic allocation original script or executable identity changed.");
            }
        }

        private void ApplyIntent(ReplayJournalEntry entry, bool physicalTail)
        {
            switch (entry.Kind)
            {
                case ReplayEntryKind.FieldLoad:
                case ReplayEntryKind.WorldAllocation:
                    break;
                case ReplayEntryKind.SemanticTrigger:
                    if (entry.SemanticTrigger?.Kind != ReplaySemanticTriggerKind.WorldFieldEntry)
                    {
                        Admit(entry.SemanticTrigger ?? throw new InvalidDataException("Semantic input lost its native producer."));
                    }
                    break;
                case ReplayEntryKind.ScriptGoal:
                    ReachProducer(entry.ScriptGoal ?? throw new InvalidDataException("Semantic producer hint is missing."));
                    break;
                case ReplayEntryKind.InputSample:
                    SampleInput(entry);
                    break;
                case ReplayEntryKind.InputPulse:
                    ReplayScriptGoal pulseProducer = entry.ScriptGoal ?? throw new InvalidDataException("Input pulse lost its current producer.");
                    if (world.Events.IsSemanticProducerPending(pulseProducer))
                    {
                        world.Events.QueueSemanticInputPulse(pulseProducer, entry.MappedPulse);
                    }
                    break;
                case ReplayEntryKind.NativeTraversal:
                    ApplyTraversal(entry.NativeTraversal ?? throw new InvalidDataException("Native traversal lost its issuing producer."));
                    break;
                case ReplayEntryKind.PersistentEffect:
                    ApplyEffect(entry);
                    break;
                case ReplayEntryKind.MovieSample:
                    ApplyMovieSample(entry, physicalTail);
                    break;
                case ReplayEntryKind.MovieComplete:
                    if (!movieFinished && session.IsMoviePlaying)
                    {
                        session.SetReplayMovieSourceHash(ValidateMovieSource(entry, session.State, gameDirectory));
                        session.CompleteMovie(entry.Skipped);
                    }
                    movieFinished = true;
                    break;
                case ReplayEntryKind.BattleInput:
                    if (entry.BattleInput?.Kind != BattleInputKind.ConfigurePlaythrough)
                    {
                        Seek(() => world.Events.PendingInteractiveBattle is not null, "battle input");
                        NativeBattleCompletion battle = world.Events.PendingInteractiveBattle!;
                        if (battle.ObjectId != entry.ObjectId || battle.ScriptId != entry.ScriptId || battle.Operation != entry.Operation || battle.Encounter != entry.Value || battle.Flags != entry.Code || battle.ProgramHash != entry.NativeSourceHash)
                        {
                            throw new InvalidDataException("Semantic battle input differs from its current native issuing site.");
                        }
                    }
                    world.ApplyBattleInput(entry.BattleInput ?? throw new InvalidDataException("Semantic battle payload is missing."));
                    break;
                case ReplayEntryKind.Dialogue:
                    if (entry.Code != 0)
                    {
                        ApplyDecision(entry, entry.Code == 2 ? PlayerActionKind.EnterName : PlayerActionKind.Choose);
                    }
                    else if (physicalTail)
                    {
                        ApplyDecision(entry, PlayerActionKind.Confirm);
                    }
                    break;
                case ReplayEntryKind.Action:
                case ReplayEntryKind.WorldAction:
                    ApplyAction(entry, physicalTail);
                    break;
                case ReplayEntryKind.Step:
                    if (physicalTail)
                    {
                        world.Events.Step();
                        world.PublishReplayDeparture();
                    }
                    break;
                case ReplayEntryKind.Update:
                    if (physicalTail)
                    {
                        world.Events.Update(TimeSpan.FromTicks(entry.ElapsedTicks));
                        world.PublishReplayDeparture();
                    }
                    break;
                default:
                    throw new NotSupportedException($"Semantic intent {entry.Kind} has no current execution contract.");
            }
            if (world.Events.LastFault is EventFault fault)
            {
                throw new EventScriptException(fault.Message);
            }
            if (session.ReplayJournal?.IsComplete != true)
            {
                throw new InvalidDataException(session.ReplayJournal?.UnavailableReason ?? "Semantic execution lost its genuine New Game history.");
            }
        }

        private void ApplyAction(ReplayJournalEntry entry, bool physicalTail)
        {
            PlayerActionKind kind = (PlayerActionKind)entry.Code;
            if (kind is PlayerActionKind.Choose or PlayerActionKind.EnterName)
            {
                ApplyDecision(entry, kind);
            }
            else if (entry.Kind == ReplayEntryKind.WorldAction)
            {
                world.Apply(new PlayerAction(kind, entry.Value, entry.Text, entry.Horizontal, entry.Vertical, entry.Frames, entry.Quantity, entry.CameraHorizontal));
            }
            else if (kind == PlayerActionKind.Interact && !physicalTail)
            {
                if (entry.Accepted)
                {
                    Seek(() => world.Events.CanAdmitSemanticInput, "current interaction admission owner");
                    ReplaySemanticTrigger[] targets = world.Events.GetSemanticTriggerCandidates().Where(candidate => candidate.Kind == ReplaySemanticTriggerKind.ActorInteraction && candidate.ObjectId == entry.Value).ToArray();
                    if (targets.Length != 1)
                    {
                        throw new InvalidDataException("The historical interaction has no unique current actor producer.");
                    }
                    Admit(targets[0]);
                }
            }
            else if (kind == PlayerActionKind.SkipMovie)
            {
                Seek(() => session.IsMoviePlaying, "movie skip");
                world.ReplayMovieSkip(entry);
                movieFinished = true;
            }
            else if (kind == PlayerActionKind.FieldButtons && inferredVisit && !physicalTail)
            {
                ApplyLegacyButtons(entry);
            }
            else if (world.Events.PartyMenu is not null || kind is PlayerActionKind.SelectPartySlot or PlayerActionKind.SelectPartyMember or PlayerActionKind.BuyShopItem)
            {
                Seek(() => world.Events.PartyMenu is not null, "menu input");
                ValidatePartyMenuDecisionSite(world.Events, entry);
                PlayerActionResult result = world.Apply(new PlayerAction(kind, entry.Value, entry.Text, Frames: 1, Quantity: entry.Quantity));
                if (!result.Accepted)
                {
                    throw new InvalidDataException("The current native menu rejected a retained meaningful decision.");
                }
            }
            else if (kind == PlayerActionKind.FieldButtons || physicalTail)
            {
                if (entry.MessageId is not null && kind is PlayerActionKind.Confirm or PlayerActionKind.Cancel)
                {
                    ApplyDecision(entry, kind);
                }
                else
                {
                    world.Apply(new PlayerAction(kind, entry.Value, entry.Text, entry.Horizontal, entry.Vertical, entry.Frames, entry.Quantity));
                }
            }
        }

        private void ApplyDecision(ReplayJournalEntry entry, PlayerActionKind kind)
        {
            Seek(() => MatchesDecision(entry), "recorded decision");
            if (entry.Kind != ReplayEntryKind.WorldAction)
            {
                ValidateDialogueSite(world.Events, entry);
            }
            PlayerActionResult result = world.Apply(new PlayerAction(kind, entry.Value, entry.Text));
            if (!result.Accepted)
            {
                throw new InvalidDataException("The current native dialogue rejected a retained decision.");
            }
        }

        private bool MatchesDecision(ReplayJournalEntry entry)
        {
            DialogueState? dialogue = world.CurrentDialogue;
            return dialogue is { CanConfirm: true } && dialogue.MessageId == entry.MessageId && dialogue.Operation == entry.Operation && (entry.Kind == ReplayEntryKind.WorldAction || dialogue.ObjectId == entry.ObjectId && dialogue.ScriptId == entry.ScriptId);
        }

        private void Admit(ReplaySemanticTrigger trigger)
        {
            for (int index = 0; index < admissions.Count; index++)
            {
                if (!consumedAdmissions.Contains(index) && SameAdmission(admissions[index], trigger))
                {
                    consumedAdmissions.Add(index);
                    return;
                }
            }
            ReplaySemanticTrigger[] precedingEvents = admissions.Where(admission => admission.ObjectId != trigger.ObjectId && admission.Kind is ReplaySemanticTriggerKind.ActorInteraction or ReplaySemanticTriggerKind.DoorEntry or ReplaySemanticTriggerKind.DoorAction).ToArray();
            Seek(() => precedingEvents.All(admission => !world.Events.IsSemanticAdmissionPending(admission)), "preceding native event completion");
            Seek(() => world.Events.CanAdmitSemanticInput, "semantic native admission");
            PlayerActionResult result = world.Events.ApplySemanticTrigger(trigger);
            if (!result.Accepted)
            {
                throw new InvalidDataException("The current native dispatcher rejected a retained semantic admission.");
            }
            world.PublishReplayDeparture();
            knownProducers.Add((trigger.ObjectId, trigger.ScriptId));
            if (admissions.Count > 0)
            {
                consumedAdmissions.Add(admissions.Count - 1);
            }
        }

        private static bool SameAdmission(ReplaySemanticTrigger left, ReplaySemanticTrigger right)
        {
            return left.Kind == right.Kind && left.FieldId == right.FieldId && left.ProgramHash == right.ProgramHash && left.NativeObjectId == right.NativeObjectId && left.ObjectId == right.ObjectId && left.ScriptId == right.ScriptId && left.NativeSlot == right.NativeSlot && left.LocalScript == right.LocalScript && left.InfIndex == right.InfIndex && left.EventBits == right.EventBits && left.Contact == right.Contact && (right.ExecutableHash is null || left.ExecutableHash == right.ExecutableHash);
        }

        private void ReachProducer(ReplayScriptGoal goal)
        {
            bool pending = world.Events.IsSemanticProducerPending(goal);
            if (knownProducers.Contains((goal.ObjectId, goal.ScriptId)) || pending)
            {
                return;
            }
            ReplaySemanticTrigger[] targets = world.Events.GetSemanticProducerCandidates(goal).ToArray();
            if (targets.Length == 0)
            {
                if (world.Events.IsAutonomousSemanticProducer(goal))
                {
                    return;
                }
                Seek(() => knownProducers.Contains((goal.ObjectId, goal.ScriptId)) || world.Events.IsSemanticProducerPending(goal) || (targets = world.Events.GetSemanticProducerCandidates(goal).ToArray()).Length > 0 || world.Events.IsAutonomousSemanticProducer(goal), $"current producer admission {goal.FieldId}/{goal.ObjectId}/{goal.ScriptId}");
                if (knownProducers.Contains((goal.ObjectId, goal.ScriptId)) || world.Events.IsSemanticProducerPending(goal) || targets.Length == 0 && world.Events.IsAutonomousSemanticProducer(goal))
                {
                    return;
                }
            }
            if (targets.Any(target => admissions.Any(admission => SameAdmission(target, admission))))
            {
                return;
            }
            if (targets.Length > 1)
            {
                throw new InvalidDataException("The retained script producer has multiple current native admissions.");
            }
            if (targets.Length == 1)
            {
                Admit(targets[0]);
                return;
            }
        }

        private void SampleInput(ReplayJournalEntry entry)
        {
            ReplayScriptGoal goal = entry.ScriptGoal ?? throw new InvalidDataException("Input sample lost its native poll producer.");
            bool pending = world.Events.IsSemanticProducerPending(goal);
            bool ConsumeSample() => ConsumeReceipt(receipt => receipt.Kind == ReplayReceiptKind.InputSample && receipt.FieldId == goal.FieldId && receipt.ObjectId == goal.ObjectId && receipt.ScriptId == goal.ScriptId && receipt.Operation == goal.Operation && receipt.Code == goal.Code && receipt.Value == entry.Value && (receipt.Arguments is { Length: >= 5 } arguments ? arguments[4] : 0) == entry.MappedPulse);
            if (ConsumeSample())
            {
                return;
            }
            if (!pending && knownProducers.Contains((goal.ObjectId, goal.ScriptId)))
            {
                return;
            }
            world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, entry.Value));
            if (!pending)
            {
                ReachProducer(goal);
            }
            if (entry.MappedPulse != 0 && world.Events.IsSemanticProducerPending(goal))
            {
                world.Events.QueueSemanticInputPulse(goal, entry.MappedPulse);
            }
            Seek(() => ConsumeSample() || knownProducers.Contains((goal.ObjectId, goal.ScriptId)) && !world.Events.IsSemanticProducerPending(goal), "retained native input consumer");
        }

        private bool ConsumeReceipt(Func<ReplayReceipt, bool> matches)
        {
            for (int index = 0; index < receipts.Count; index++)
            {
                if (!consumedReceipts.Contains(index) && matches(receipts[index]))
                {
                    consumedReceipts.Add(index);
                    return true;
                }
            }
            return false;
        }

        private void ApplyTraversal(ReplayNativeTraversal traversal)
        {
            traversal.Validate();
            ReplayScriptGoal producer = traversal.Producer;
            world.Events.IsSemanticProducerPending(producer);
            bool ConsumeCompletion() => ConsumeReceipt(receipt => receipt.Kind == ReplayReceiptKind.NativeTraversal && receipt.FieldId == producer.FieldId && receipt.ObjectId == producer.ObjectId && receipt.ScriptId == producer.ScriptId && receipt.Operation == producer.Operation && receipt.Code == producer.Code && receipt.Value == (traversal.Forward ? 1 : -1) && receipt.Arguments is { Length: >= 2 } arguments && arguments[0] == traversal.Mode);
            if (ConsumeCompletion())
            {
                return;
            }
            ReachProducer(producer);
            Seek(() => world.Events.CanApplySemanticTraversal(traversal), "current native traversal owner");
            PlayerActionResult result = world.Events.ApplySemanticNativeTraversal(traversal);
            if (!result.Accepted)
            {
                throw new InvalidDataException("The current native ladder rejected its retained endpoint direction.");
            }
            Seek(ConsumeCompletion, "current native traversal endpoint");
            world.Events.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0));
            world.PublishReplayDeparture();
        }

        private void ApplyLegacyButtons(ReplayJournalEntry entry)
        {
            int firstReceipt = receipts.Count;
            world.Events.Apply(new PlayerAction(PlayerActionKind.FieldButtons, entry.Value));
            if (entry.Value == 0 && !world.Events.HasPendingSemanticButtonConsumer)
            {
                return;
            }
            for (int callback = 0; callback < MaximumReadinessCallbacks; callback++)
            {
                if (world.Events.PendingSemanticTraversal is not null)
                {
                    if (world.Events.ResolvePendingSemanticTraversal(entry.Value) is { } traversal)
                    {
                        ApplyTraversal(traversal);
                    }
                    return;
                }
                if (receipts.Skip(firstReceipt).Any(receipt => receipt.Kind == ReplayReceiptKind.InputSample) || world.CurrentDialogue is { CanConfirm: true } or { FirstChoice: not null } or { IsNamePrompt: true } || world.Events.PartyMenu is not null || world.Events.PendingInteractiveBattle is not null || session.IsMoviePlaying || world.Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition)
                {
                    return;
                }
                if (callback > 0 && !world.Events.HasPendingSemanticButtonConsumer)
                {
                    return;
                }
                Advance();
            }
            throw new InvalidDataException("Legacy button inference did not reach a current native input consumer.");
        }

        private void ApplyEffect(ReplayJournalEntry entry)
        {
            int[] arguments = entry.EffectArguments ?? throw new InvalidDataException("Retained persistent effect lost its primitive input.");
            if (entry.Scene == SceneKind.WorldMap)
            {
                List<GlobalSoundRequest> requests = [];
                NativeSemanticSalaryEffects effects = session.ApplySemanticWorldEffect(entry, (sample, presentation, process) =>
                {
                    NativeSalaryEffects prepared = world.Events.PrepareSalaryEffects(sample, presentation, process);
                    requests.AddRange(prepared.Requests);
                    return new NativeSemanticSalaryEffects(prepared.Presentation, prepared.SoundProcess);
                });
                world.Events.PublishSalaryEffects(new NativeSalaryEffects(effects.Presentation, effects.SoundProcess, requests));
                return;
            }
            bool MatchesCause(ReplayReceipt receipt) => receipt.Kind == ReplayReceiptKind.PersistentEffect && (ReplayPersistentEffectKind)receipt.Code is ReplayPersistentEffectKind.SalaryProgress or ReplayPersistentEffectKind.SalaryPayment && receipt.FieldId == entry.FieldId && receipt.Arguments is { } cause && cause.SequenceEqual(arguments);
            Seek(() => world.Events.CanApplySemanticEffect, "current field effect phase");
            int remaining = entry.Frames;
            while (remaining > 0 && ConsumeReceipt(MatchesCause))
            {
                remaining--;
            }
            if (remaining == 0)
            {
                return;
            }
            PlayerActionResult result = world.Events.ApplySemanticEffect(entry with { Frames = remaining });
            if (!result.Accepted)
            {
                throw new InvalidDataException("The current field rejected a retained persistent-effect input.");
            }
            for (int callback = 0; callback < remaining; callback++)
            {
                ConsumeReceipt(MatchesCause);
            }
            world.PublishReplayDeparture();
        }

        private void ApplyMovieSample(ReplayJournalEntry entry, bool physicalTail)
        {
            if (movieHash is null || movieFinished || entry.MovieSourceHash != movieHash)
            {
                Seek(() => session.IsMoviePlaying, "recorded movie");
                movieHash = ValidateMovieSource(entry, session.State, gameDirectory);
                movieFinished = false;
                movieCallbackFrame = -1;
            }
            if (entry.MovieSourceHash != movieHash)
            {
                throw new InvalidDataException("Semantic movie samples changed their bound original media.");
            }
            if (!session.IsMoviePlaying)
            {
                return;
            }
            session.SetReplayMovieSourceHash(movieHash);
            if (entry.MoviePlayback is MoviePlaybackState playback)
            {
                session.SetMoviePlayback(playback);
            }
            session.SetMoviePosition(entry.MovieSeconds);
            if (!physicalTail && entry.MoviePlayback is { FrameIndex: >= 2 } decoded && decoded.FrameIndex != movieCallbackFrame)
            {
                world.Events.Step();
                world.PublishReplayDeparture();
                movieCallbackFrame = decoded.FrameIndex;
            }
        }

        private void CompleteFieldVisit(ReplayCompletedVisit visit)
        {
            bool exitAdmitted = false;
            for (int callback = 0; callback < MaximumReadinessCallbacks; callback++)
            {
                if (session.State.Scene == visit.Exit.Scene && session.State.FieldId == visit.Exit.FieldId && (session.State.Scene == SceneKind.WorldMap || world.Events.FieldId != session.State.FieldId || world.Events.NextTrigger.Kind == PlayerTriggerKind.FieldTransition))
                {
                    return;
                }
                if (!exitAdmitted && world.Events.IsUserControlEnabled && world.CurrentDialogue is null && !session.IsMoviePlaying)
                {
                    ReplaySemanticTrigger[] exits = visit.Exit.Trigger is { } recorded ? [recorded] : world.Events.GetSemanticExitCandidates(visit.Exit.Scene, visit.Exit.FieldId).ToArray();
                    if (exits.Any(exit => admissions.Any(admission => SameAdmission(exit, admission))))
                    {
                        exitAdmitted = true;
                    }
                    else if (exits.Length == 1 || exits.Length > 1 && (world.Events.AreSemanticExitCandidatesEquivalent(exits) || visit.Inferred && world.Events.CanCanonicalizeLegacyGatewayEntries(exits)))
                    {
                        Admit(exits[0]);
                        exitAdmitted = true;
                    }
                    else if (exits.Length > 1)
                    {
                        throw new InvalidDataException($"Semantic visit{visit.Ordinal} field{visit.FieldId}→{visit.Exit.Scene}/{visit.Exit.FieldId} has multiple current exit producers: {string.Join(';', exits.Select(exit => $"{exit.Kind}/object{exit.ObjectId}/script{exit.ScriptId}/local{exit.LocalScript}/inf{exit.InfIndex}"))}.");
                    }
                }
                Advance();
            }
            throw new InvalidDataException("Semantic visit did not reach its native departure within the readiness budget.");
        }

        private void Seek(Func<bool> ready, string purpose)
        {
            for (int callback = 0; callback < MaximumReadinessCallbacks; callback++)
            {
                if (HasDepartedCurrentVisit())
                {
                    throw new SemanticVisitDepartureException();
                }
                if (ready())
                {
                    return;
                }
                if (world.CurrentDialogue is { } dialogue && (dialogue.FirstChoice is not null || dialogue.IsNamePrompt))
                {
                    throw new InvalidDataException($"A different native decision blocks {purpose}: {dialogue.ObjectId}/{dialogue.ScriptId}/{dialogue.Operation}/{dialogue.MessageId}.");
                }
                Advance();
            }
            throw new InvalidDataException($"Current engine did not reach {purpose} within the semantic readiness budget.");
        }

        private void Advance()
        {
            if (world.CurrentDialogue is { CanConfirm: true, FirstChoice: null, IsNamePrompt: false })
            {
                world.Apply(new PlayerAction(PlayerActionKind.Confirm));
            }
            else if (session.IsMoviePlaying || world.Events.PartyMenu is not null || world.Events.PendingInteractiveBattle is not null || world.CurrentDialogue is { FirstChoice: not null } or { IsNamePrompt: true })
            {
                throw new InvalidDataException("Semantic progress requires a retained external decision or media sample.");
            }
            else if (session.State.Scene == SceneKind.WorldMap)
            {
                world.Apply(new PlayerAction(PlayerActionKind.Wait));
            }
            else
            {
                world.Events.Step();
                world.PublishReplayDeparture();
            }
        }
    }
}
