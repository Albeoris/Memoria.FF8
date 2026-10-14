using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private ReplayCapture? replayCapture;
    private ulong? recordedMemoryDigest;

    public void Restore(JsonElement snapshot)
    {
        bool authentic = services is GameSession session && (session.State.EventState is JsonElement current && JsonElement.DeepEquals(current, snapshot) || session.State.DepartingFieldState is JsonElement departing && JsonElement.DeepEquals(departing, snapshot));
        RestoreCore(snapshot);
        if (!authentic)
        {
            services.MarkReplayUnavailable("A VM restore was not bound to this save's recorded checkpoint.");
        }
        recordedMemoryDigest = ReplayMemoryDigest();
    }

    public void LoadField(int fieldId, byte[] jsm, IReadOnlyList<string> fieldMessages, FieldTransition? arrival = null, int? previousFieldId = null, NativeFieldRuntimeProfile? nativeRuntimeProfile = null, JsonElement? worldEntityAllocationContext = null)
    {
        ReplayJournalEntry input = ReplayBoundary(ReplayEntryKind.FieldLoad) with { FieldId = fieldId, Frame = 0, NativeSourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(jsm)), Text = nativeRuntimeProfile?.ExecutableHash };
        RunReplay(input, () =>
        {
            LoadFieldCore(fieldId, jsm, fieldMessages, arrival, previousFieldId, nativeRuntimeProfile, worldEntityAllocationContext);
            return true;
        });
    }

    public void Step()
    {
        RunReplay(ReplayBoundary(ReplayEntryKind.Step), () =>
        {
            PlayerTrigger before = NextTrigger;
            StepCore();
            RecordReplayProgress(before);
            return true;
        });
    }

    public void Update(TimeSpan elapsed)
    {
        RunReplay(ReplayBoundary(ReplayEntryKind.Update) with { ElapsedTicks = elapsed.Ticks }, () =>
        {
            UpdateCore(elapsed);
            return true;
        });
    }

    public void ConfirmDialogue(int? choice = null, string? name = null)
    {
        RunReplay(ReplayBoundary(ReplayEntryKind.Dialogue) with { Value = choice ?? 0, Code = choice.HasValue ? 1 : name is null ? 0 : 2, Text = name }, () =>
        {
            ConfirmDialogueCore(choice, name);
            return true;
        });
    }

    public PlayerActionResult Apply(PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunReplay(ReplayBoundary(ReplayEntryKind.Action) with { Code = (int)action.Kind, Value = action.Value, Text = action.Text, Horizontal = action.Horizontal, Vertical = action.Vertical, Frames = action.Frames }, () => ApplyCore(action));
    }

    private ReplayJournalEntry ReplayBoundary(ReplayEntryKind kind)
    {
        DialogueState? dialogue = CurrentDialogue;
        return new ReplayJournalEntry(kind, services.ReplayScene, FieldId, Frame)
        {
            Trigger = (int)NextTrigger.Kind,
            MessageId = dialogue?.MessageId,
            ObjectId = dialogue?.ObjectId,
            ScriptId = dialogue?.ScriptId,
            Operation = dialogue?.Operation,
            StartPose = ReplayPose()
        };
    }

    private ReplayPlayerPose? ReplayPose()
    {
        ActorState? player = actors.FirstOrDefault(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
        FieldFixedPosition? position = Scene.NativePartyTraceVersion == 1 ? Scene.NativePlayerPosition : null;
        return player is null ? null : new ReplayPlayerPose(FieldId, player.X, player.Y, player.Z, player.WalkmeshTriangle, (int)player.Direction, position?.X, position?.Y, position?.Z);
    }

    private T RunReplay<T>(ReplayJournalEntry input, Func<T> operation)
    {
        if (replayCapture is not null || services.ReplayJournal is null || services.IsReplayCaptureActive)
        {
            return operation();
        }
        ValidateReplayMemory();
        replayCapture = new ReplayCapture();
        using IDisposable? scope = services.BeginReplayCapture();
        try
        {
            T result = operation();
            ReplayCapture capture = replayCapture;
            PlayerActionResult? actionResult = result as PlayerActionResult;
            DialogueState? resultDialogue = CurrentDialogue;
            services.RecordReplayEntry(input with
            {
                Advanced = actionResult?.FramesAdvanced ?? checked((int)Math.Max(0, Frame - input.Frame)),
                Accepted = actionResult?.Accepted ?? true,
                ResultScene = services.ReplayScene,
                ResultFieldId = services.ReplayFieldId ?? NextTrigger.FieldId,
                ResultFrame = Frame,
                ResultTrigger = (int)NextTrigger.Kind,
                ResultMessageId = resultDialogue?.MessageId,
                ResultObjectId = resultDialogue?.ObjectId,
                ResultScriptId = resultDialogue?.ScriptId,
                ResultOperation = resultDialogue?.Operation,
                EndPose = ReplayPose(),
                Navigation = capture.Navigation.ToArray(),
                Receipts = capture.Receipts.ToArray(),
                CommandCount = capture.CommandCount,
                CommandDigest = capture.CommandDigest,
                MovieSourceHash = input.Code == (int)PlayerActionKind.SkipMovie && input.Kind == ReplayEntryKind.Action ? services.ReplayMovieSourceHash : input.MovieSourceHash,
                MoviePlayback = input.Kind == ReplayEntryKind.Action && input.Code == (int)PlayerActionKind.SkipMovie ? services.ReplayMoviePlayback : input.MoviePlayback
            });
            recordedMemoryDigest = ReplayMemoryDigest();
            return result;
        }
        catch
        {
            services.MarkReplayUnavailable("An engine causal transaction failed before its complete outcome was recorded.");
            throw;
        }
        finally
        {
            replayCapture = null;
        }
    }

    private void RecordReplayNavigation(ActorState player, WalkmeshPosition end, FieldFixedPosition? fixedPosition = null, int? direction = null, bool nativeMovement = false, FieldFixedPosition? triggerPosition = null, int[]? pushContacts = null)
    {
        if (replayCapture is null)
        {
            return;
        }
        ReplayPlayerPose start = ReplayPose()!;
        int endDirection = direction ?? (start.X != end.Position.X || start.Y != end.Position.Y ? NativeDirection(start.X, start.Y, end.Position.X, end.Position.Y) : start.Direction);
        ReplayPlayerPose destination = new(FieldId, end.Position.X, end.Position.Y, end.Position.Z, end.Triangle, endDirection, fixedPosition?.X, fixedPosition?.Y, fixedPosition?.Z);
        replayCapture.Navigation.Add(new ReplayNavigationStimulus(start, destination, nativeMovement, triggerPosition?.X, triggerPosition?.Y, triggerPosition?.Z, pushContacts));
    }

    private void RecordReplayProgress(PlayerTrigger before)
    {
        PlayerTrigger after = NextTrigger;
        if (replayCapture is not null && (before.Kind != after.Kind || before.FieldId != after.FieldId || before.MessageId != after.MessageId))
        {
            replayCapture.Receipts.Add(new ReplayReceipt(ReplayReceiptKind.Progress, FieldId, Frame, -1, -1, -1, (int)after.Kind, after.MessageId ?? -1));
        }
    }

    private void RecordReplayExecution(Fiber fiber, JsmOperation operation)
    {
        if (replayCapture is not ReplayCapture capture)
        {
            return;
        }
        capture.CommandCount++;
        foreach (int value in new[] { FieldId, fiber.ObjectId, fiber.Script.Id, operation.Index, (int)operation.Opcode, operation.Parameter })
        {
            capture.CommandDigest = unchecked((capture.CommandDigest ^ (uint)value) * 1099511628211UL);
        }
        if (operation.Opcode is Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L)
        {
            capture.Receipts.Add(new ReplayReceipt(ReplayReceiptKind.Command, FieldId, Frame, fiber.ObjectId, fiber.Script.Id, operation.Index, (int)operation.Opcode, operation.Parameter));
        }
    }

    private ulong ReplayMemoryDigest()
    {
        ulong digest = 14695981039346656037UL;
        foreach ((int address, int value) in variables.OrderBy(pair => pair.Key))
        {
            digest = unchecked((digest ^ (uint)address) * 1099511628211UL);
            digest = unchecked((digest ^ (uint)value) * 1099511628211UL);
        }
        return digest;
    }

    private void ValidateReplayMemory()
    {
        if (replayCapture is null && !services.IsReplayCaptureActive && recordedMemoryDigest is ulong digest && digest != ReplayMemoryDigest())
        {
            services.MarkReplayUnavailable("Script memory changed outside an engine causal transaction.");
        }
    }

    private sealed class ReplayCapture
    {
        public List<ReplayReceipt> Receipts { get; } = [];
        public List<ReplayNavigationStimulus> Navigation { get; } = [];
        public long CommandCount { get; set; }
        public ulong CommandDigest { get; set; } = 14695981039346656037UL;
    }
}
