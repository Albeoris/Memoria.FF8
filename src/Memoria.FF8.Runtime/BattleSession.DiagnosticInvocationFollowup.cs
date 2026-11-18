using System.Collections.Immutable;
using System.Diagnostics;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticInvocationFollowupCallback(BattleActorReference Actor, byte Stage, long QueuedCallback, long StartedCallback, long CompletedCallback);
public sealed record BattleDiagnosticInvocationPendingCallback(BattleActorReference Actor, byte Stage, long QueuedCallback);
public sealed record BattleDiagnosticInvocationFinalDrain(Guid LifecycleId, long Callback, long SimulationFrame, long GraphicsCallback, long Timestamp, BattleDiagnosticEffectDispatch FocalDispatch, BattleDiagnosticInvocationOwners Owners);
public sealed record BattleDiagnosticInvocationFollowupRelease(BattleDiagnosticInvocationRelease FocalRetirement, BattleDiagnosticInvocationFinalDrain FinalDrain, ImmutableArray<BattleDiagnosticInvocationFollowupCallback> Callbacks)
{
    public ImmutableArray<BattleDiagnosticInvocationTextureRelease> TextureReleases { get; init; } = [];
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticFollowupCallback(long Queued, long? Started = null, long? Completed = null);
    private Dictionary<BattleExpandedAiCallbackRequest, DiagnosticFollowupCallback>? diagnosticFollowupCallbacks;
    private bool diagnosticFollowupAmbiguous;
    private BattleDiagnosticInvocationRelease? diagnosticFollowupRetirement;
    public event Action<BattleDiagnosticInvocationFollowupRelease>? DiagnosticInvocationFollowupReleased;
    public event Action<BattleDiagnosticInvocationRelease>? DiagnosticInvocationFocalRetired;
    public BattleDiagnosticInvocationFollowupRelease? LastDiagnosticInvocationFollowupRelease { get; private set; }

    private bool ObservingDiagnosticInvocationFollowup => diagnosticInvocationPolicy is not null && (DiagnosticInvocationFollowupReleased is not null || DiagnosticInvocationFocalRetired is not null) && LastDiagnosticInvocationFollowupRelease is null;
    private bool ObservingDiagnosticParticipantCallbacks => ObservingDiagnosticCounter || ObservingDiagnosticInvocationFollowup;

    private void ObserveDiagnosticFollowupCallback(BattleParticipantCallbackReceipt receipt)
    {
        if (!ObservingDiagnosticInvocationFollowup || diagnosticInvocationDispatch is null || diagnosticFollowupAmbiguous)
        {
            return;
        }
        if (FindTargetActor(receipt.Request.Actor) is not { IsPlayer: true } || receipt.Request.Stage is not (2 or 3))
        {
            diagnosticFollowupAmbiguous |= diagnosticFollowupRetirement is not null;
            return;
        }
        long callback = NativeHostCallbacks ?? Frame;
        Dictionary<BattleExpandedAiCallbackRequest, DiagnosticFollowupCallback> callbacks = diagnosticFollowupCallbacks ??= [];
        if (receipt.Phase == BattleParticipantCallbackPhase.Queued)
        {
            if (diagnosticFollowupRetirement is not null || callbacks.Count >= 64 || !callbacks.TryAdd(receipt.Request, new(callback)))
            {
                diagnosticFollowupAmbiguous = true;
            }
            return;
        }
        if (!callbacks.TryGetValue(receipt.Request, out DiagnosticFollowupCallback? pending) || diagnosticFollowupRetirement is null)
        {
            diagnosticFollowupAmbiguous = true;
            return;
        }
        if (receipt.Phase == BattleParticipantCallbackPhase.Started && pending.Started is null && pending.Completed is null)
        {
            callbacks[receipt.Request] = pending with { Started = callback };
        }
        else if (receipt.Phase == BattleParticipantCallbackPhase.Completed && pending.Started is not null && pending.Completed is null)
        {
            callbacks[receipt.Request] = pending with { Completed = callback };
        }
        else
        {
            diagnosticFollowupAmbiguous = true;
        }
    }

    private void ObserveDiagnosticFollowupRetirement(BattleDiagnosticInvocationRelease retirement)
    {
        bool textures = retirement.Owners.AuxiliaryHead;
        if (!ObservingDiagnosticInvocationFollowup || diagnosticFollowupRetirement is not null || diagnosticFollowupAmbiguous || diagnosticFollowupCallbacks is not { Count: > 0 } callbacks || retirement.Boundary != BattleDiagnosticInvocationRelease.GraphicsFrameBoundary || !retirement.NativeEventCompleted || !retirement.PresentationAcknowledged || !retirement.HeaderCapturedBeforeReset || !retirement.CurrentCompletionPacketGuaranteedRemoval || !retirement.PacketRemoved || retirement.ExecutingCompletion is null || retirement.TerminalTextures is not null ||
            (textures ? retirement.Blockers is not ["effect-or-auxiliary-task", "action-or-reaction"] : retirement.Blockers is not ["action-or-reaction"]) ||
            !retirement.Owners.ActionOrCommand || !DiagnosticFollowupOwnersEmpty(retirement.Owners with { ActionOrCommand = false, AuxiliaryHead = false }) || ActiveAction is not null || actions.Count == 0 || actions.Count != callbacks.Count)
        {
            return;
        }
        foreach (BattleAttackCommand command in actions)
        {
            if (command.ExpandedAiCallback is not { } request || FindTargetActor(request.Actor) is not { IsPlayer: true } || request.Stage is not (2 or 3) || command != ExpandedAiCallbackPacket(request) || !callbacks.TryGetValue(request, out DiagnosticFollowupCallback? callback) || callback.Started is not null || callback.Completed is not null)
            {
                return;
            }
        }
        if (!TryCaptureDiagnosticFollowupTextures(retirement, out ImmutableArray<BattleDiagnosticInvocationPendingTexture> pendingTextures))
        {
            return;
        }
        diagnosticFollowupRetirement = retirement with { PendingFollowups = callbacks.Select(pair => new BattleDiagnosticInvocationPendingCallback(pair.Key.Actor, pair.Key.Stage, pair.Value.Queued)).ToImmutableArray(), PendingTextures = pendingTextures };
        LastDiagnosticInvocationCompletion = diagnosticFollowupRetirement;
        DiagnosticInvocationFocalRetired?.Invoke(diagnosticFollowupRetirement);
    }

    private void ObserveDiagnosticFollowupDrain(BattlePresentationProcess process)
    {
        if (!ObservingDiagnosticInvocationFollowup || diagnosticFollowupRetirement is not { } retirement || diagnosticFollowupAmbiguous || retirement.LifecycleId != DiagnosticLifecycleId || !ReferenceEquals(retirement.Dispatch, diagnosticInvocationDispatch) || diagnosticFollowupCallbacks is not { Count: > 0 } pending || pending.Values.Any(callback => callback.Started is null || callback.Completed is null) || diagnosticFollowupTextures?.Values.Any(texture => texture.Release is null) == true || (NativeHostCallbacks ?? Frame) <= retirement.Callback || process.Graphics.Callback <= retirement.GraphicsCallback || Frame < retirement.SimulationFrame)
        {
            return;
        }
        BattleDiagnosticInvocationOwners owners = CaptureDiagnosticInvocationOwnerState(process.Graphics);
        if (!DiagnosticFollowupOwnersEmpty(owners))
        {
            return;
        }
        ImmutableArray<BattleDiagnosticInvocationFollowupCallback> callbacks = pending.Select(pair => new BattleDiagnosticInvocationFollowupCallback(pair.Key.Actor, pair.Key.Stage, pair.Value.Queued, pair.Value.Started!.Value, pair.Value.Completed!.Value)).ToImmutableArray();
        LastDiagnosticInvocationFollowupRelease = new(retirement, new(DiagnosticLifecycleId, NativeHostCallbacks ?? Frame, Frame, process.Graphics.Callback, Stopwatch.GetTimestamp(), retirement.Dispatch, owners), callbacks)
        {
            TextureReleases = diagnosticFollowupTextures?.Values.Select(texture => texture.Release!).ToImmutableArray() ?? []
        };
        DiagnosticInvocationFollowupReleased?.Invoke(LastDiagnosticInvocationFollowupRelease);
    }

    private static bool DiagnosticFollowupOwnersEmpty(BattleDiagnosticInvocationOwners owners)
    {
        return owners.DescriptorPackets == 0 && owners.NonPumpDescriptorTasks == 0 && owners.PendingContacts == 0 && owners.Effects == 0 && !owners.EffectHead && !owners.AuxiliaryHead && owners.Modules == 0 && !owners.ModuleHead && owners.Files == 0 && owners.ReturnedTaskPointer == 0 && !owners.DetachedReturnedTask && owners.CameraMask == 0 && !owners.SoundBusy && !owners.ReplacementResource && !owners.ActionOrCommand && !owners.TimedOrReaction && owners.ActionHeaders == 0 && owners.ContactRows == 0 && owners.ObservedOriginalEffect && owners.SelectedReturnedDescriptor == 0 && !owners.HeaderGraphicsBusy && !owners.OriginalPools.IsDefault && owners.OriginalPools.All(pool => pool is not null && pool.ActiveRows == 0 && !pool.Head && !pool.Tail && pool.DescriptorAddress != 0 && pool.BankAddress != 0 && pool.Stride != 0);
    }
}
