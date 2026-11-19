using System.Collections.Immutable;
using System.Diagnostics;
using static Memoria.FF8.Runtime.BattlePresentationProcess;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticPlayerStage2SelfInputs(BattlePartyActorReference Actor, byte NativeActorIndex, BattlePartyActorReference Attacker, byte AttackerNativeIndex, uint PassiveFlags, ushort SavedStatus, uint BattleStatus, uint CombatantFlags, bool Active);
public sealed record BattleDiagnosticPlayerStage2NoOp(BattleDiagnosticPlayerStage2SelfInputs Inputs, long Callback, long Timestamp, long CompletionToken, uint CompletionCallback, int HeaderCount, int OperationCount, int ProducedCommands)
{
    public long? CompletedCallback { get; init; }
    public long? CompletedTimestamp { get; init; }
}
public sealed record BattleDiagnosticAftercareBoundary(Guid LifecycleId, long Callback, long SimulationFrame, long GraphicsCallback, long Timestamp, long DispatchSerial, long HeaderGeneration, long ActionIssued, long CompletionIssued, long CompletionAcknowledged, int ObservedFrames, int NativeActive, uint CanonicalReturnedTask, BattleDiagnosticPlayerStage2SelfInputs Inputs, bool CallbackCompleted, BattleDiagnosticInvocationOwners Owners, BattleDiagnosticTextureQuiescence Textures, string ResidentFingerprint);
public sealed record BattleDiagnosticAftercarePresent(long PresentedFrame, long Timestamp, BattleDiagnosticAftercareBoundary Boundary);
public sealed record BattleDiagnosticAftercareTail(string TailPhase, int PresentedFrames, BattleDiagnosticAftercarePresent First, BattleDiagnosticAftercarePresent Last)
{
    public const string Phase = "focal-retirement-through-no-op-aftercare-present";
}

public sealed partial class BattleSession
{
    private BattleDiagnosticPlayerStage2SelfInputs? diagnosticPreparedPlayerStage2SelfInputs;
    private BattleDiagnosticPlayerStage2SelfInputs? diagnosticAftercareInputs;
    private DiagnosticHostedLedger? diagnosticAftercareLedger;
    private int diagnosticAftercareObservedFrames;
    private long diagnosticAftercareFrameTimestamp;
    private string? diagnosticAftercareResidentFingerprint;

    private BattleDiagnosticPlayerStage2SelfInputs? CaptureDiagnosticPlayerStage2SelfInputs(BattleCombatant actor, uint flags)
    {
        if (!actor.IsPlayer || !actor.IsActive || actor.NativeActorIndex is not int native || native is < 0 or > 2 || actor.LastAttackerId is not { } attackerId || attackerId != actor.Id || GetActor(attackerId) is not { IsPlayer: true, NativeActorIndex: int attackerNative } attacker || attackerNative != native)
        {
            return null;
        }
        return new(new(actor.Id), (byte)native, new(attacker.Id), (byte)attackerNative, flags, NativeSavedStatusBits(actor), NativeBattleStatusBits(actor), actor.NativeCombatantFlags, actor.IsActive);
    }

    private bool HasDiagnosticAftercareBinding(BattleDiagnosticPlayerStage2SelfInputs inputs, NativeBattleGraphicsState graphics)
    {
        return inputs.NativeActorIndex <= 2 && inputs.AttackerNativeIndex == inputs.NativeActorIndex && inputs.Actor == inputs.Attacker &&
            graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(inputs.NativeActorIndex), graphics.ActorModels) == new BattlePartyActorModel(inputs.Actor);
    }

    private BattleDiagnosticPlayerStage2NoOp? CaptureDiagnosticPlayerStage2NoOp(BattleSession candidate, BattleExpandedAiCallbackRequest request, BattleModelOwner owner, ImmutableArray<BattleActionHeaderReference> headers)
    {
        if (request.Stage != 2 || request.Actor is not BattlePartyActorReference actor || candidate.diagnosticPreparedPlayerStage2SelfInputs is not { } inputs || inputs.Actor != actor || !headers.IsEmpty || candidate.actions.Count != 0 ||
            expandedOperationCursor.Operations is not [ExpandedHostedDescriptor { Control: HostedDispatchControl.Complete { Completion: { Callback: 0x47DDA0, CapturedBeforeAi: true } completion } }] || completion.Owner != owner || owner != new BattlePartyActorModel(actor) || completion.ActorId != actor.ActorId ||
            Presentation is not { } process || !HasDiagnosticAftercareBinding(inputs, process.Graphics))
        {
            return null;
        }
        DiagnosticHostedLedger ledger = process.ReadDiagnosticHostedLedger();
        return new(inputs, NativeHostCallbacks ?? Frame, Stopwatch.GetTimestamp(), ledger.Completions, completion.Callback, headers.Length, expandedOperationCursor.Operations.Length, candidate.actions.Count);
    }

    private void BeginDiagnosticAftercareInterval(BattleDiagnosticInvocationRelease retirement)
    {
        if (Presentation is not { } process || retirement.PendingTextures.IsDefaultOrEmpty || retirement.PendingFollowups is not [{ Actor: BattlePartyActorReference actor, Stage: 2 }] || actor.ActorId != retirement.Dispatch.ActorId ||
            FindTargetActor(actor) is not { NativePlayerStatRules.NativeStatusPassiveFlags: uint flags } combatant || CaptureDiagnosticPlayerStage2SelfInputs(combatant, flags) is not { } inputs || !HasDiagnosticAftercareBinding(inputs, process.Graphics))
        {
            return;
        }
        diagnosticAftercareInputs = inputs;
        diagnosticAftercareLedger = process.ReadDiagnosticHostedLedger();
        if (diagnosticAftercareLedger.Value.Completions != retirement.CompletionToken || diagnosticAftercareLedger.Value.Acknowledged != retirement.CompletionToken)
        {
            diagnosticAftercareInputs = null;
            diagnosticAftercareLedger = null;
        }
    }

    public BattleDiagnosticAftercareBoundary? ObserveDiagnosticAftercareBoundary()
    {
        if (diagnosticAftercareInputs is not { } inputs || diagnosticAftercareLedger is not { } initial || diagnosticFollowupAmbiguous || LastDiagnosticInvocationFollowupRelease is not null && !diagnosticFollowupResidentTail || diagnosticFollowupRetirement is not { } retirement || !ReferenceEquals(retirement, LastDiagnosticInvocationCompletion) ||
            Presentation is not { } process || retirement.LifecycleId != DiagnosticLifecycleId || retirement.Dispatch.Serial != diagnosticDispatchSerial || retirement.Dispatch.HeaderReference is not { } reference || process.ReadDiagnosticCounterHeaderGeneration(reference.Ordinal) is not long generation || generation != retirement.Dispatch.NativeHeaderGeneration ||
            diagnosticFollowupCallbacks is not { Count: 1 } callbacks || callbacks.Single().Key is not { Actor: BattlePartyActorReference actor, Stage: 2 } || actor != inputs.Actor ||
            FindTargetActor(actor) is not { NativePlayerStatRules.NativeStatusPassiveFlags: uint flags } combatant || CaptureDiagnosticPlayerStage2SelfInputs(combatant, flags) != inputs || !HasDiagnosticAftercareBinding(inputs, process.Graphics) ||
            diagnosticFollowupTextures is not { Count: > 0 } textures || textures.Values.Any(texture => texture.Release is not null || !ReferenceEquals(process.Graphics.Auxiliary.Slots[texture.Pending.Slot], texture.Task) || texture.Task.ModelOwner != texture.Owner))
        {
            return null;
        }
        DiagnosticFollowupCallback callback = callbacks.Single().Value;
        bool completed = callback.Completed is not null;
        DiagnosticHostedLedger ledger = process.ReadDiagnosticHostedLedger();
        if (ledger.Actions != initial.Actions || (completed ? callback.NoOp is not { } proof || proof.Inputs != inputs || proof.CompletionToken != initial.Completions + 1 || ledger.Completions != proof.CompletionToken || ledger.Acknowledged != proof.CompletionToken : callback.Started is not null || ledger.Completions != initial.Completions || ledger.Acknowledged != initial.Acknowledged))
        {
            return null;
        }
        BattleDiagnosticInvocationOwners owners = CaptureDiagnosticInvocationOwnerState(process.Graphics);
        if (!owners.AuxiliaryHead || !DiagnosticFollowupOwnersEmpty(owners with { ActionOrCommand = false, AuxiliaryHead = false }) || ActiveAction is not null ||
            (completed ? actions.Count != 0 || owners.ActionOrCommand || expandedHostedEvent is not null : actions.Count != 1 || !owners.ActionOrCommand || actions.Peek().ExpandedAiCallback != callbacks.Single().Key || actions.Peek() != ExpandedAiCallbackPacket(callbacks.Single().Key)))
        {
            return null;
        }
        BattleDiagnosticTextureQuiescence held = CaptureDiagnosticTextureQuiescence(process.Graphics);
        if (!held.IsTerminalQuiescent || held.Tasks.Length != retirement.PendingTextures.Length || held.Tasks.Any(task => task.ModelActorId != inputs.Actor.ActorId))
        {
            return null;
        }
        string fingerprint = process.CaptureDiagnosticResidentTextureFingerprint(owners, held);
        if (diagnosticAftercareResidentFingerprint is { } baseline && fingerprint != baseline)
        {
            return null;
        }
        if (process.Memory.Active != 0 || process.Memory.ReadReturnedTask5044B0() != 0)
        {
            return null;
        }
        if (completed && process.ObserveComponentTerminalTextures(retirement.Dispatch.RequestedEffect, owners, ["effect-or-auxiliary-task"])?.ResidentFingerprint != fingerprint)
        {
            return null;
        }
        return new(DiagnosticLifecycleId, NativeHostCallbacks ?? Frame, Frame, process.Graphics.Callback, diagnosticAftercareFrameTimestamp, diagnosticDispatchSerial, generation, ledger.Actions, ledger.Completions, ledger.Acknowledged, diagnosticAftercareObservedFrames, process.Memory.Active, checked((uint)process.Memory.ReadReturnedTask5044B0()), inputs, completed, owners, held, fingerprint);
    }

    private void ObserveDiagnosticAftercareFrameEnded()
    {
        if (diagnosticAftercareInputs is not null && diagnosticFollowupRetirement is not null && (LastDiagnosticInvocationFollowupRelease is null || diagnosticFollowupResidentTail))
        {
            BattleDiagnosticAftercareBoundary? boundary = ObserveDiagnosticAftercareBoundary();
            if (boundary is null)
            {
                diagnosticFollowupAmbiguous = true;
            }
            else
            {
                diagnosticAftercareResidentFingerprint ??= boundary.ResidentFingerprint;
                diagnosticAftercareObservedFrames = checked(diagnosticAftercareObservedFrames + 1);
                diagnosticAftercareFrameTimestamp = Stopwatch.GetTimestamp();
            }
        }
    }
}
