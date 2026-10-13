using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum PlayerActionKind
{
    Confirm,
    Choose,
    EnterName,
    Interact,
    Move,
    Wait,
    SkipMovie,
    Cancel
}

public enum PlayerTriggerKind
{
    Unloaded,
    Waiting,
    Dialogue,
    Movie,
    FieldTransition,
    Playable,
    Faulted,
    WorldMap
}

public sealed record PlayerAction(PlayerActionKind Kind, int Value = 0, string? Text = null, float Horizontal = 0, float Vertical = 0, int Frames = 1);
public sealed record PlayerTrigger(PlayerTriggerKind Kind, int FieldId, long Frame, int? MessageId, string? Text);
public sealed record PlayerActionResult(bool Accepted, PlayerTrigger Trigger, int FramesAdvanced, string? Detail = null);
public sealed record PlayerActionObservation(long Index, int FieldId, long Frame, PlayerAction Action, PlayerActionResult Result);
public sealed record WalkmeshPosition(Vector3 Position, int Triangle);
public sealed record BranchDecision(int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, string Predicate, int Value, bool ConditionPassed, int JumpTarget, long ActionIndex);
public sealed record BranchCoverage(int FieldId, int ObjectId, int ScriptId, int Operation, string Predicate, int TrueCount, int FalseCount);
public sealed record DialogueOutcome(int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int MessageId, int? Choice, string? Name, long ActionIndex);

public sealed partial class EventEngine
{
    private readonly List<PlayerActionObservation> actionLog = [];
    private readonly Dictionary<(int Field, int Object, int Script, int Operation), BranchCoverage> branchCoverage = [];
    private readonly List<BranchDecision> decisions = [];
    private readonly List<DialogueOutcome> dialogueOutcomes = [];
    private long? activeActionIndex;
    public IReadOnlyList<PlayerActionObservation> ActionLog => actionLog;
    public IReadOnlyCollection<BranchCoverage> Coverage => branchCoverage.Values;
    public IReadOnlyList<BranchDecision> Decisions => decisions;
    public IReadOnlyList<DialogueOutcome> DialogueOutcomes => dialogueOutcomes;
    public Func<float, float, WalkmeshPosition?>? ResolveWalkmesh { get; set; }
    public event Action<BranchDecision>? BranchEvaluated;
    public event Action? MovieSkipRequested;
    public event Action<DialogueOutcome>? DialogueCompleted;

    public PlayerTrigger NextTrigger => new(
        LastFault is not null ? PlayerTriggerKind.Faulted : program is null ? PlayerTriggerKind.Unloaded : transitionRequested ? Scene.WorldTransitionRequest is null ? PlayerTriggerKind.FieldTransition : PlayerTriggerKind.WorldMap : services.IsMoviePlaying ? PlayerTriggerKind.Movie : dialogues.Values.Any(dialogue => dialogue.CanConfirm) ? PlayerTriggerKind.Dialogue : IsUserControlEnabled && defaultsStarted && !fibers.Any(fiber => !fiber.Complete && !fiber.Started) ? PlayerTriggerKind.Playable : PlayerTriggerKind.Waiting,
        FieldId, Frame, CurrentDialogue?.MessageId, LastFault?.Message ?? CurrentDialogue?.Text);

    /// <summary>Applies an ordinary player action; no variable or script-PC mutation API is exposed.</summary>
    private PlayerActionResult ApplyCore(PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (activeActionIndex is not null)
        {
            throw new InvalidOperationException("Player actions cannot be applied recursively from an event callback.");
        }
        if (action.Frames < 1 || action.Frames > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Action duration must be between one and 30000 simulation frames.");
        }
        if (LastFault is EventFault fault)
        {
            throw new EventScriptException(fault.Message);
        }
        bool accepted = true;
        int advanced = 0;
        string? detail = null;
        int sourceField = FieldId;
        long sourceFrame = Frame;
        activeActionIndex = actionLog.Count + 1;
        try
        {
            switch (action.Kind)
            {
                case PlayerActionKind.Confirm:
                    accepted = CanConfirmDialogue && CurrentDialogue?.FirstChoice is null && CurrentDialogue?.IsNamePrompt == false;
                    if (accepted)
                    {
                        QueueConfirmEdge();
                        ConfirmDialogue();
                    }
                    else if (CurrentDialogue is null && program is not null && !services.IsMoviePlaying && !transitionRequested)
                    {
                        accepted = true;
                        QueueConfirmEdge();
                        if (SelectInteractionTarget() is int target)
                        {
                            InteractWith(target);
                        }
                        Step();
                        advanced = 1;
                    }
                    break;
                case PlayerActionKind.Cancel:
                    accepted = CurrentDialogue is { CanConfirm: true } cancelDialogue && (cancelDialogue.CancelChoice is not null || !cancelDialogue.IsNamePrompt && cancelDialogue.FirstChoice is null);
                    if (accepted)
                    {
                        QueueButtonEdge(0x10);
                        ConfirmDialogue(CurrentDialogue!.CancelChoice);
                    }
                    else if (CurrentDialogue is null && program is not null && !services.IsMoviePlaying && !transitionRequested)
                    {
                        accepted = true;
                        QueueButtonEdge(0x10);
                        Step();
                        advanced = 1;
                    }
                    break;
                case PlayerActionKind.Choose:
                    accepted = CurrentDialogue?.FirstChoice is not null;
                    if (accepted)
                    {
                        QueueConfirmEdge();
                        ConfirmDialogue(action.Value);
                    }
                    break;
                case PlayerActionKind.EnterName:
                    accepted = CurrentDialogue?.IsNamePrompt == true;
                    if (accepted)
                    {
                        QueueConfirmEdge();
                        ConfirmDialogue(name: action.Text);
                    }
                    break;
                case PlayerActionKind.Interact:
                    accepted = InteractWith(action.Value);
                    if (accepted)
                    {
                        QueueConfirmEdge();
                    }
                    break;
                case PlayerActionKind.Move:
                    if (!float.IsFinite(action.Horizontal) || !float.IsFinite(action.Vertical) || Math.Abs(action.Horizontal) > 1 || Math.Abs(action.Vertical) > 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(action), "Movement axes must be finite and between -1 and 1.");
                    }
                    ActorState? player = actors.FirstOrDefault(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
                    accepted = player is not null && IsUserControlEnabled && !IsDialogueBlocking && !services.IsMoviePlaying;
                    if (accepted && ResolveWalkmesh is null)
                    {
                        throw new InvalidOperationException("Player movement requires the field walkmesh resolver.");
                    }
                    if (accepted)
                    {
                        Vector2 axes = new(action.Horizontal, action.Vertical);
                        if (axes.LengthSquared() > 1)
                        {
                            axes = Vector2.Normalize(axes);
                        }
                        for (int index = 0; index < action.Frames; index++)
                        {
                            if (player!.NativeScriptMovement is { Active: true } || player.NativeJump is { Active: true })
                            {
                                FieldFixedPosition position = ReadNativeScriptPosition(player) ?? throw new NotSupportedException("Scripted player input suppression requires the actual retained raw body position.");
                                RecordReplayNavigation(player, new WalkmeshPosition(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle), position, (int)player.Direction, false, null, []);
                                Step();
                                advanced++;
                                if (!IsUserControlEnabled || IsDialogueBlocking || services.IsMoviePlaying || transitionRequested)
                                {
                                    break;
                                }
                                continue;
                            }
                            int[] pushContacts = [];
                            IReadOnlyList<NativeLineContactChange> lineChanges = [];
                            if (Scene.NativePartyTraceVersion == 1)
                            {
                                var native = ResolveNativePartyPlayerStep(player!, axes);
                                pushContacts = native.PushContacts;
                                RecordReplayNavigation(player!, native.Position, native.FixedPosition, native.Direction, native.NativeMovement, native.TriggerPosition, native.PushContacts);
                                lineChanges = MovePlayerCore(native.Position.Position.X, native.Position.Position.Y, native.Position.Position.Z, native.Position.Triangle, native.FixedPosition, native.Direction, native.NativeMovement, native.TriggerPosition);
                                if (native.NativeMovement)
                                {
                                    Scene.NativePlayerMovementVersion = 1;
                                    // 47A2F6 clears 1CDBFED after the native field-line query.
                                    Scene.NativePlayerEarlyRetryState = 0;
                                }
                            }
                            else
                            {
                                Vector2 rotatedAxes = Vector2.Transform(axes, Matrix3x2.CreateRotation(InputAxisDirection * MathF.Tau / 256f));
                                float speed = Math.Max(1, player!.MovementSpeed) / 256f;
                                WalkmeshPosition? position = ResolveWalkmesh!(player.X + rotatedAxes.X * speed, player.Y + rotatedAxes.Y * speed);
                                if (position is not null)
                                {
                                    RecordReplayNavigation(player, position);
                                    MovePlayer(position.Position.X, position.Position.Y, position.Position.Z, position.Triangle);
                                }
                                else
                                {
                                    RecordReplayNavigation(player, new WalkmeshPosition(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle));
                                }
                            }
                            Step();
                            // Native4767B0 runs scripts before player movement.
                            // New byte584 writes belong to the next update,
                            // including a gateway update with no destination tick.
                            foreach (int objectId in pushContacts)
                            {
                                actors[objectId].NativePushContactPending = true;
                            }
                            CommitNativeLineMovement(lineChanges);
                            advanced++;
                            if (!IsUserControlEnabled || IsDialogueBlocking || services.IsMoviePlaying || transitionRequested)
                            {
                                break;
                            }
                        }
                    }
                    break;
                case PlayerActionKind.Wait:
                    for (int index = 0; index < action.Frames; index++)
                    {
                        if (NextTrigger.Kind is PlayerTriggerKind.Faulted or PlayerTriggerKind.Unloaded or PlayerTriggerKind.Dialogue or PlayerTriggerKind.Movie or PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap)
                        {
                            break;
                        }
                        Step();
                        advanced++;
                        if (NextTrigger.Kind != PlayerTriggerKind.Waiting)
                        {
                            break;
                        }
                    }
                    detail = NextTrigger.Kind == PlayerTriggerKind.Waiting ? "Frame budget reached while scripts still wait." : null;
                    break;
                case PlayerActionKind.SkipMovie:
                    accepted = services.IsMoviePlaying && MovieSkipRequested is not null;
                    if (accepted)
                    {
                        MovieSkipRequested!.Invoke();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
            PlayerActionResult result = new(accepted, NextTrigger, advanced, detail);
            actionLog.Add(new PlayerActionObservation(activeActionIndex.Value, sourceField, sourceFrame, action, result));
            return result;
        }
        finally
        {
            activeActionIndex = null;
        }
    }

    /// <summary>Replays ordinary actions in order. The host must supply movie and field services as during live play.</summary>
    public IReadOnlyList<PlayerActionResult> Replay(IEnumerable<PlayerAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return actions.Select(Apply).ToArray();
    }

    private void RecordBranch(Fiber fiber, int operation, string predicate, int value, int jumpTarget)
    {
        var key = (FieldId, fiber.ObjectId, fiber.Script.Id, operation);
        BranchCoverage current = branchCoverage.GetValueOrDefault(key) ?? new BranchCoverage(FieldId, fiber.ObjectId, fiber.Script.Id, operation, predicate, 0, 0);
        branchCoverage[key] = value != 0 ? current with { TrueCount = current.TrueCount + 1 } : current with { FalseCount = current.FalseCount + 1 };
        BranchDecision decision = new(FieldId, fiber.ObjectId, fiber.Script.Id, operation, Frame, predicate, value, value != 0, jumpTarget, activeActionIndex ?? actionLog.Count);
        decisions.Add(decision);
        replayCapture?.Receipts.Add(new ReplayReceipt(ReplayReceiptKind.Branch, FieldId, Frame, fiber.ObjectId, fiber.Script.Id, operation, jumpTarget, value, predicate));
        BranchEvaluated?.Invoke(decision);
    }

    private void RecordDialogueOutcome(DialogueState dialogue, int? choice, string? name)
    {
        DialogueOutcome outcome = new(FieldId, dialogue.ObjectId, dialogue.ScriptId, dialogue.Operation, Frame, dialogue.MessageId, choice, name, activeActionIndex ?? actionLog.Count);
        dialogueOutcomes.Add(outcome);
        replayCapture?.Receipts.Add(new ReplayReceipt(ReplayReceiptKind.Dialogue, FieldId, Frame, dialogue.ObjectId, dialogue.ScriptId, dialogue.Operation, dialogue.MessageId, choice ?? -1, name));
        DialogueCompleted?.Invoke(outcome);
    }
}
