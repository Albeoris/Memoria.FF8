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
    Cancel,
    FieldButtons,
    SelectPartyMember,
    BuyShopItem,
    SelectPartySlot
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
    WorldMap,
    PartyMenu,
    Battle
}

public sealed record PlayerAction(PlayerActionKind Kind, int Value = 0, string? Text = null, float Horizontal = 0, float Vertical = 0, int Frames = 1, int? Quantity = null, float CameraHorizontal = 0);
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
        LastFault is not null ? PlayerTriggerKind.Faulted : program is null ? PlayerTriggerKind.Unloaded : transitionRequested ? Scene.WorldTransitionRequest is null ? PlayerTriggerKind.FieldTransition : PlayerTriggerKind.WorldMap : PendingInteractiveBattle is not null ? PlayerTriggerKind.Battle : services.IsMoviePlaying ? PlayerTriggerKind.Movie : PartyMenu is not null ? PlayerTriggerKind.PartyMenu : dialogues.Values.Any(dialogue => dialogue.CanConfirm) ? PlayerTriggerKind.Dialogue : defaultsStarted && AreSelectedFieldScriptsStarted() && CanAcceptFieldMovement ? PlayerTriggerKind.Playable : PlayerTriggerKind.Waiting,
        FieldId, Frame, PartyMenu is not null ? null : CurrentDialogue?.MessageId, LastFault?.Message ?? (PartyMenu is not null ? null : CurrentDialogue?.Text));

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
            if (PendingInteractiveBattle is not null)
            {
                accepted = false;
                detail = "The interactive battle suspends ordinary field input until its outcome returns.";
            }
            else if (PartyMenu is not null)
            {
                accepted = action.Kind switch
                {
                    PlayerActionKind.SelectPartySlot => SelectPartyMenuSlot(action.Value),
                    PlayerActionKind.SelectPartyMember => SelectPartyMenuMember(action.Value),
                    PlayerActionKind.BuyShopItem => action.Quantity is int quantity && BuyNativeShopItem(action.Value, quantity),
                    PlayerActionKind.Confirm => ConfirmPartyMenu(),
                    PlayerActionKind.Cancel => PartyMenu.Kind switch { NativeFieldMenuKind.Shop => CloseNativeShop(), NativeFieldMenuKind.WeaponShop => CloseNativeWeaponShop(), NativeFieldMenuKind.PartySelection => CancelPartyMenuSelection(), _ => CloseNormalMenu() },
                    PlayerActionKind.Wait => true,
                    _ => false
                };
                if (accepted)
                {
                    if (action.Kind == PlayerActionKind.Wait)
                    {
                        for (int callback = 0; callback < action.Frames; callback++)
                        {
                            Step();
                            advanced++;
                            if (partyMenu is null)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        advanced = 1;
                    }
                }
                else
                {
                    detail = "Party menu requires an explicit eligible member selection and confirmation; ordinary field inputs are suspended.";
                }
            }
            else
            {
                switch (action.Kind)
                {
                    case PlayerActionKind.SelectPartySlot:
                    case PlayerActionKind.SelectPartyMember:
                    case PlayerActionKind.BuyShopItem:
                        accepted = false;
                        break;
                    case PlayerActionKind.FieldButtons:
                        accepted = program is not null && !services.IsMoviePlaying && !transitionRequested;
                        if (accepted)
                        {
                            SetFieldButtons(action.Value);
                        }
                        break;
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
                        ActorState? player = PlayerActor;
                        accepted = CanAcceptFieldMovement;
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
                                ValidateNativePlayerInput(player!);
                                int[] pushContacts = [];
                                IReadOnlyList<NativeLineContactChange> lineChanges = [];
                                pendingNativePlayerInput = () =>
                                {
                                    var native = ResolveNativePartyPlayerStep(player!, axes);
                                    pushContacts = native.PushContacts;
                                    lineChanges = MovePlayerCore(native.Position.Position.X, native.Position.Position.Y, native.Position.Position.Z, native.Position.Triangle, native.FixedPosition, native.Direction, native.NativeMovement, native.TriggerPosition);
                                    PublishNativePlayerMovementResult(native.MovementSucceeded);
                                    if (native.NativeMovement)
                                    {
                                        Scene.NativePlayerMovementVersion = 1;
                                        // 47A2F6 clears 1CDBFED after the native field-line query.
                                        Scene.NativePlayerEarlyRetryState = 0;
                                    }
                                };
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
                                if (!IsUserControlEnabled || IsMovementBlockedByDialogue || services.IsMoviePlaying || transitionRequested || PendingInteractiveBattle is not null)
                                {
                                    break;
                                }
                            }
                        }
                        break;
                    case PlayerActionKind.Wait:
                        for (int index = 0; index < action.Frames; index++)
                        {
                            if (NextTrigger.Kind is PlayerTriggerKind.Faulted or PlayerTriggerKind.Unloaded or PlayerTriggerKind.Dialogue or PlayerTriggerKind.Movie or PlayerTriggerKind.FieldTransition or PlayerTriggerKind.WorldMap or PlayerTriggerKind.Battle)
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
                            long beforeSkip = Frame;
                            MovieSkipRequested!.Invoke();
                            advanced = checked((int)(Frame - beforeSkip));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action));
                }
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


    private void RecordBranch(Fiber fiber, int operation, string predicate, int value, int jumpTarget)
    {
        var key = (FieldId, fiber.ObjectId, fiber.Script.Id, operation);
        BranchCoverage current = branchCoverage.GetValueOrDefault(key) ?? new BranchCoverage(FieldId, fiber.ObjectId, fiber.Script.Id, operation, predicate, 0, 0);
        branchCoverage[key] = value != 0 ? current with { TrueCount = current.TrueCount + 1 } : current with { FalseCount = current.FalseCount + 1 };
        BranchDecision decision = new(FieldId, fiber.ObjectId, fiber.Script.Id, operation, Frame, predicate, value, value != 0, jumpTarget, activeActionIndex ?? actionLog.Count);
        decisions.Add(decision);
        BranchEvaluated?.Invoke(decision);
    }

    private void RecordDialogueOutcome(DialogueState dialogue, int? choice, string? name)
    {
        DialogueOutcome outcome = new(FieldId, dialogue.ObjectId, dialogue.ScriptId, dialogue.Operation, Frame, dialogue.MessageId, choice, name, activeActionIndex ?? actionLog.Count);
        dialogueOutcomes.Add(outcome);
        DialogueCompleted?.Invoke(outcome);
    }
}
