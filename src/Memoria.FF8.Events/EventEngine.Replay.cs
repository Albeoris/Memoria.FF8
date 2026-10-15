using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private ReplayCapture? replayCapture;
    private ulong? recordedMemoryDigest;
    public event Action<ReplayReceipt>? CausalReceiptRecorded;

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
        if (action.CameraHorizontal != 0)
        {
            throw new NotSupportedException("Manual camera input belongs to the ordinary world motor; field cameras require their executed script contract.");
        }
        if (action.Kind == PlayerActionKind.BuyShopItem && (action.Value is < 0 or > byte.MaxValue || action.Quantity is null or < 1 or > byte.MaxValue))
        {
            throw new ArgumentException("Shop purchase requires a BYTE item and an explicit positive BYTE quantity.", nameof(action));
        }
        if (action.Kind == PlayerActionKind.SelectPartySlot && (uint)action.Value >= 3 || action.Kind == PlayerActionKind.SelectPartyMember && (uint)action.Value >= 8 && action.Value != 255)
        {
            throw new ArgumentException("Party menu input requires an explicit slot0..2 or native member0..7/emptyFF.", nameof(action));
        }
        return RunReplay(ReplayBoundary(ReplayEntryKind.Action) with { Code = (int)action.Kind, Value = action.Value, Quantity = action.Quantity, Text = action.Text, Horizontal = action.Horizontal, Vertical = action.Vertical, Frames = action.Frames }, () => ApplyCore(action));
    }

    private ReplayJournalEntry ReplayBoundary(ReplayEntryKind kind)
    {
        DialogueState? dialogue = CurrentDialogue;
        return new ReplayJournalEntry(kind, services.ReplayScene, FieldId, Frame)
        {
            Trigger = (int)NextTrigger.Kind,
            MessageId = PartyMenu is not null ? null : dialogue?.MessageId,
            NativeSourceHash = PartyMenu?.ProgramHash,
            ObjectId = PartyMenu?.ObjectId ?? dialogue?.ObjectId,
            ScriptId = PartyMenu?.ScriptId ?? dialogue?.ScriptId,
            Operation = PartyMenu?.Operation ?? dialogue?.Operation,
            StartPose = ReplayPose()
        };
    }

    private ReplayPlayerPose? ReplayPose()
    {
        ActorState? player = PlayerActor;
        FieldFixedPosition? position = Scene.NativePartyTraceVersion == 1 ? Scene.NativePlayerPosition : null;
        return player is not { Active: true, Visible: true } ? null : new ReplayPlayerPose(FieldId, player.X, player.Y, player.Z, player.WalkmeshTriangle, (int)player.Direction, position?.X, position?.Y, position?.Z);
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
                ResultMessageId = PartyMenu is not null ? null : resultDialogue?.MessageId,
                ResultObjectId = PartyMenu?.ObjectId ?? resultDialogue?.ObjectId,
                ResultScriptId = PartyMenu?.ScriptId ?? resultDialogue?.ScriptId,
                ResultOperation = PartyMenu?.Operation ?? resultDialogue?.Operation,
                EndPose = ReplayPose(),
                Navigation = capture.Navigation.ToArray(),
                Receipts = capture.Receipts.ToArray(),
                SemanticAdmissions = capture.SemanticAdmissions.ToArray(),
                CommandCount = capture.CommandCount,
                CommandDigest = capture.CommandDigest,
                MovieSourceHash = input.Code == (int)PlayerActionKind.SkipMovie && input.Kind == ReplayEntryKind.Action ? services.ReplayMovieSourceHash : input.MovieSourceHash,
                MovieSkipFrames = input.Kind == ReplayEntryKind.Action && input.Code == (int)PlayerActionKind.SkipMovie ? services.ReplayMovieSkipFrames : null,
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
            RecordCausalReceipt(new ReplayReceipt(ReplayReceiptKind.Progress, FieldId, Frame, -1, -1, -1, (int)after.Kind, after.MessageId ?? -1));
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
        if (IsDurableReplayCommand(operation.Opcode))
        {
            RecordCausalReceipt(new ReplayReceipt(ReplayReceiptKind.Command, FieldId, Frame, fiber.ObjectId, fiber.Script.Id, operation.Index, (int)operation.Opcode, operation.Parameter));
        }
        if (operation.Opcode is Jsm.Opcode.KEYON or Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2)
        {
            NativeFieldButtonInput input = ButtonInput ?? throw new InvalidDataException("An executed native input predicate lost its actual sampled owner.");
            ushort extraRaw = (ushort)(input.RawHeld & ~input.PhysicalHeld);
            ushort mappedPulse = extraRaw == 0 ? (ushort)0 : services.InputConfiguration?.Map(extraRaw) ?? throw new NotSupportedException("Native input pulse capture requires its actual current input mapping.");
            RecordCausalReceipt(new ReplayReceipt(ReplayReceiptKind.InputSample, FieldId, Frame, fiber.ObjectId, fiber.Script.Id, operation.Index, (int)operation.Opcode, input.PhysicalHeld, Arguments: [input.RawHeld, input.RawPressed, (int?)input.Held ?? -1, (int?)input.Pressed ?? -1, mappedPulse]));
        }
    }

    private void RecordCausalReceipt(ReplayReceipt receipt)
    {
        if (replayCapture is ReplayCapture capture)
        {
            ReplayReceipt recorded = receipt with { Sequence = capture.NextSequence++ };
            capture.Receipts.Add(recorded);
            CausalReceiptRecorded?.Invoke(recorded);
        }
    }

    private static bool IsDurableReplayCommand(Jsm.Opcode opcode)
    {
        return opcode is Jsm.Opcode.CLEAR or Jsm.Opcode.SETTIMER or Jsm.Opcode.KILLTIMER or Jsm.Opcode.HOLD or Jsm.Opcode.Unknown16 or Jsm.Opcode.SARALYDISPON or Jsm.Opcode.SARALYDISPOFF or Jsm.Opcode.POPM_B or Jsm.Opcode.POPM_W or Jsm.Opcode.POPM_L or Jsm.Opcode.ADDITEM or Jsm.Opcode.ADDGIL or Jsm.Opcode.ADDPASTGIL or Jsm.Opcode.ADDMEMBER or Jsm.Opcode.SUBMEMBER or Jsm.Opcode.ADDPARTY or Jsm.Opcode.SUBPARTY or Jsm.Opcode.SETPARTY or Jsm.Opcode.CHANGEPARTY or Jsm.Opcode.JUNCTION or Jsm.Opcode.RESETGF or Jsm.Opcode.DYING or Jsm.Opcode.SETHP or Jsm.Opcode.REST or Jsm.Opcode.PHSPOWER or Jsm.Opcode.GETCARD or Jsm.Opcode.SETCARD or Jsm.Opcode.SETDRAWPOINT or Jsm.Opcode.DRAWPOINT or Jsm.Opcode.ADDSEEDLEVEL or Jsm.Opcode.SARALYON or Jsm.Opcode.SARALYOFF or Jsm.Opcode.SAVEENABLE or Jsm.Opcode.PHSENABLE or Jsm.Opcode.SETPLACE or Jsm.Opcode.DISC or Jsm.Opcode.BATTLE or Jsm.Opcode.BATTLEMODE or Jsm.Opcode.MOVIE or Jsm.Opcode.MENUPHS or Jsm.Opcode.MENUSAVE or Jsm.Opcode.MENUNORMAL or Jsm.Opcode.MENUSHOP or Jsm.Opcode.MENUNAME or Jsm.Opcode.MAPJUMP or Jsm.Opcode.MAPJUMP3 or Jsm.Opcode.DISCJUMP or Jsm.Opcode.WORLDMAPJUMP;
    }

    private ulong ReplayMemoryDigest()
    {
        ulong digest = 14695981039346656037UL;
        NativeEventMemory memory = EventMemory;
        digest = unchecked((digest ^ (uint)memory.Version) * 1099511628211UL);
        foreach ((int address, NativeMemoryValue cell) in memory.Cells.OrderBy(pair => pair.Key))
        {
            digest = unchecked((digest ^ (uint)address) * 1099511628211UL);
            digest = unchecked((digest ^ (uint)cell.Width) * 1099511628211UL);
            digest = unchecked((digest ^ (cell.Value.IsExact ? 1UL : 0UL)) * 1099511628211UL);
            if (cell.Value.IsExact)
            {
                digest = unchecked((digest ^ (uint)cell.Value.Values[0]) * 1099511628211UL);
                continue;
            }
            byte[] scalar = JsonSerializer.SerializeToUtf8Bytes(cell.Value, CheckpointOptions);
            digest = unchecked((digest ^ (uint)scalar.Length) * 1099511628211UL);
            foreach (byte value in scalar)
            {
                digest = unchecked((digest ^ value) * 1099511628211UL);
            }
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
        public int NextSequence { get; set; }
        public List<ReplayReceipt> Receipts { get; } = [];
        public List<ReplaySemanticTrigger> SemanticAdmissions { get; } = [];
        public List<ReplayNavigationStimulus> Navigation { get; } = [];
        public long CommandCount { get; set; }
        public ulong CommandDigest { get; set; } = 14695981039346656037UL;
    }
}
