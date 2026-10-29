using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>484720's physical actor BYTE and callback WORD; BYTE1=FF distinguishes this from an action packet.</summary>
public sealed record BattleNativeCallback([property: JsonRequired] byte ActorIndex, [property: JsonRequired] ushort Stage)
{
    public void Validate()
    {
        if (ActorIndex >= 7 || Stage is not (0 or 1 or 2 or 3 or 6 or 7 or 8))
        {
            throw new InvalidDataException("Native queued callback is outside the owned0/1/2/3/6/7/8 physical packet contract.");
        }
    }
}

public sealed partial class BattleSession
{
    private List<BattleAttackCommand>? nativeCallbackTail;

    private void QueueNativeCallback(BattleCombatant actor, ushort stage, byte bank = 0)
    {
        BattleNativeCallback callback = new(checked((byte)actor.NativeIndex), stage);
        callback.Validate();
        NativeBattlePendingReaction diagnosticPending = actor.NativePendingReaction;
        if (bank == 0)
        {
            actor.NativePendingReaction = actor.NativePendingReaction with { Stage = 0 };
        }
        BattleAttackCommand packet = new(actor.Id, actor.Id, BattleAbility.EnemyAbility, 0) { NativeCallback = callback };
        (NativeBattleCommandBanks state, NativeBattleCommandLocation location) = nativeCommandBanks.EnqueueCallback(bank, callback.ActorIndex, callback.Stage);
        nativeCommandBanks = state.BindCommand(location, 0, packet);
        ObserveDiagnosticCounterCallbackQueued(actor, stage, diagnosticPending, location);
    }

    private void QueueNativeParticipantInitialization(BattleCombatant actor)
    {
        QueueNativeCallback(actor, 0);
    }

    private void QueueNativeInitialParticipants()
    {
        foreach (BattleCombatant actor in combatants.Where(actor => actor.NativeIndex is >= 0 and < 7 && actor.IsActive && (actor.NativeCombatantFlags & 0x10) != 0 && !actor.Suspended).OrderBy(actor => actor.NativeIndex))
        {
            QueueNativeParticipantInitialization(actor);
        }
    }

    private void DispatchNativeCallback(BattleNativeCallback callback)
    {
        callback.Validate();
        if (nativeCallbackTail is not null)
        {
            throw new InvalidOperationException("Native queued callback cannot recursively drain its serialized packet queue.");
        }
        BattleCombatant? actor = combatants.SingleOrDefault(candidate => candidate.NativeIndex == callback.ActorIndex && candidate.IsActive);
        //4877F0 tests the physical slot's allocated bit before every callback, including0/7/8.
        if (actor is null)
        {
            return;
        }
        //48E830 consumes commands produced by this event before later484720 events. Newly requested events append to the existing tail.
        //The temporary CPU-dispatch list cannot span a snapshot or presentation callback; one persistent FIFO remains authoritative.
        ObserveDiagnosticCounterCallback(actor, callback.Stage, BattleDiagnosticCounterCallbackPhase.Started);
        List<BattleAttackCommand> tail = actions.ToList();
        nativeCallbackTail = tail;
        actions.Clear();
        SetNativeActionEvent(actor, 0);
        NativeBattleActionProductionContext? previousContext = nativeProductionContext;
        nativeProductionContext ??= new(0, 0, false, 0x47DDA0);
        try
        {
            if (callback.Stage is 0 or 1)
            {
                RunNativeAi(actor, callback.Stage);
            }
            else if (callback.Stage is 2 or 3)
            {
                if (callback.Stage == 3)
                {
                    actor.AiDeathHandled = true;
                }
                if (actor.IsPlayer)
                {
                    RunNativePlayerReaction(actor, callback.Stage);
                }
                else
                {
                    RunNativeAi(actor, callback.Stage);
                }
            }
            else if (callback.Stage == 6)
            {
                //4877F0 rechecks KO when the serialized status event is dispatched.
                if ((NativeSavedStatusBits(actor) & 1) == 0)
                {
                    BeginNativeBattleActionPause();
                    QueueNativeCommand(actor, 0, 4, 0, checked((ushort)(1 << callback.ActorIndex)));
                    QueueNativeActionClockCompletion(0x47DDA0);
                }
            }
            else if (callback.Stage == 7)
            {
                BeginNativeBattleActionPause();
                ExpandNativeAutomaticGuardian(actor);
                QueueNativeActionClockCompletion(0x47DDA0);
            }
            else
            {
                BeginNativeBattleActionPause();
                ExpandNativePeriodicAngelo(actor);
                QueueNativeActionClockCompletion(0x47DDA0);
            }
        }
        finally
        {
            foreach (BattleAttackCommand packet in tail)
            {
                actions.Enqueue(packet);
            }
            nativeCallbackTail = null;
            nativeProductionContext = previousContext;
            ObserveDiagnosticCounterCallback(actor, callback.Stage, BattleDiagnosticCounterCallbackPhase.Completed);
        }
    }

    private static bool IsNativeSupportCallback(BattleNativeCallback? callback)
    {
        return callback?.Stage is 7 or 8;
    }

    private void ValidateSavedNativeCallback(BattleAttackCommand packet)
    {
        BattleNativeCallback callback = packet.NativeCallback ?? throw new InvalidDataException("Native control packet lacks its callback owner.");
        callback.Validate();
        if (!byId.ContainsKey(packet.ActorId) || IsNativeSupportCallback(callback) && nativeSupport is not { EntryInitialized: true, SelectedAction: not null } || callback.Stage == 8 && nativeSupport?.SelectedTargets is null || packet.Ability != BattleAbility.EnemyAbility || packet.ActionId != 0 || packet.ActorId != packet.TargetId || packet.NativeAiCallbackStage is not null || packet.NativeAction is not null || packet.NativeRenzokuken is not null || packet.NativeDuel is not null || packet.NativeSlots is not null || packet.NativeGraphic is not null || packet.NativePlayerReaction is not null || packet.NativeQueuedCommand is not null || packet.NativeTargetMask != 0 || packet.DrawCast || packet.NativeTargetSelector != 0 || packet.NativeSequence is not null || packet.FreeNativeMagic || packet.DrawCastTargetId is not null || packet.NativeDraw is not null || packet.NativeSpecial is not null || packet.IndependentGuardian is not null || packet.NativeLimitCallback is not null || packet.ReservedNativeItemSlot is not null || !packet.NativeMagicTargets.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native callback control packet also contains incompatible command/effect owners.");
        }
    }
}
