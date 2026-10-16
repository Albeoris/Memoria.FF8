namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private int nativeActionCommitDepth;
    private byte? nativeReactionOrder;
    private bool flushingNativeReactions;

    private void BeginNativeReactionBatch()
    {
        //485460 LABEL3 resets only globalDF8 lowBYTE before dispatching one original event packet.
        nativeReactionOrder = 0;
    }

    private bool HasNativePendingReactions => nativeReactionOrder is > 0 && combatants.Any(actor => actor.NativeIndex is >= 0 and < 7 && actor.NativePendingReaction.Stage is > 0 && actor.NativePendingReaction.Order < nativeReactionOrder);

    private void SetNativeReactionActionGate(bool value)
    {
        NativeBattleAllocationControl control = nativeScriptState.AllocationControl ?? throw new NotSupportedException("Native DF9 mutation requires the retained DF9/DFD allocation gate owner.");
        nativeScriptState = nativeScriptState with { AllocationControl = control with { ActionGate = value } };
    }

    private void ResolveActionTransaction(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command)
    {
        if (nativeActionCommitDepth == 0 && nativeProductionContext is null)
        {
            BeginNativeReactionBatch();
            //TODO(TD-009): Generic components project ordinary player bank2 admission; retain the real event lane instead of the previous INIT callback
            SetNativeActionEvent(actor, actor.IsPlayer ? (byte)2 : (byte)0);
        }
        nativeActionCommitDepth++;
        bool completed = false;
        try
        {
            nativeAffinityAction = null;
            ResolveAction(actor, target, command);
            CompleteNativeActionOwners(actor, command);
            completed = true;
        }
        finally
        {
            nativeActionCommitDepth--;
            if (nativeActionCommitDepth == 0 && completed)
            {
                if (nativeProductionContext is null)
                {
                    //Generic fixture actions have no original native event/header admission.
                    CompleteNativeOrdinaryCommandCost(actor, command);
                    CompleteNativeActionStatuses(actor);
                    FlushNativeReactions();
                }
                RefreshNativeKnownMagicStocks();
                RefreshNativeEnemyDrawSlots();
            }
        }
    }

    private void CompleteNativeActionOwners(BattleCombatant actor, BattleActionPresentation command)
    {
        CompleteNativeOrdinaryCommand(actor, command);
        CompleteNativeActionInventory(actor, command);
        if (command.NativeMagicTargets.IsDefaultOrEmpty)
        {
            CompleteGuardianForceAffinity(actor, command);
        }
    }

    private void QueueNativeReaction(BattleCombatant actor, int stage)
    {
        if (actor.NativeAi is null && !actor.IsPlayer)
        {
            return;
        }
        if (stage is not (2 or 3))
        {
            throw new InvalidDataException("Native494410 only produces pending stages2/3.");
        }
        if (stage == 2 && (nativeContactWorking?.CommittingSecondary == true || (resolvingNativeAction?.NativeQueuedCommand ?? resolvingNativeAction?.NativeAction?.Command) is 0 or 14 || !actor.IsPlayer && nativeActionScratch.EventLane == 0))
        {
            return;
        }
        byte order = nativeReactionOrder ?? throw new NotSupportedException("Native pending reaction requires its actual485460 dispatch-order BYTE.");
        actor.NativePendingReaction = new(checked((byte)stage), order);
        nativeReactionOrder = unchecked((byte)(order + 1));
        if (actor.IsPlayer && stage == 3)
        {
            SetNativeReactionActionGate(true);
        }
    }

    private void FlushNativeReactions()
    {
        if (flushingNativeReactions)
        {
            return;
        }
        byte count = nativeReactionOrder ?? throw new NotSupportedException("Native47DDA0/47DE70 requires the retained pending-order BYTE.");
        bool partyCapable = combatants.Any(actor => actor.NativeIndex is >= 0 and < 3 && actor.IsActive && (NativeSavedStatusBits(actor) & 5) == 0);
        bool enemyCapable = combatants.Any(actor => actor.NativeIndex is >= 3 and < 7 && (NativeSavedStatusBits(actor) & 5) == 0);
        if (Outcome != BattleOutcome.Active || !NativeScriptAllocationPending && !(partyCapable && enemyCapable))
        {
            return;
        }
        if (count != 0 && combatants.Any(actor => actor.NativeIndex is >= 0 and < 7 && (actor.NativePendingReaction.Stage is null || actor.NativePendingReaction.Stage != 0 && actor.NativePendingReaction.Order is null)))
        {
            throw new NotSupportedException("Native pending reaction scan requires the retained physical BD7/BD8 bytes; old unknown values cannot be guessed.");
        }
        flushingNativeReactions = true;
        try
        {
            for (int order = 0; order < count; order++)
            {
                BattleCombatant? actor = combatants.Where(actor => actor.NativeIndex is >= 0 and < 7).OrderBy(actor => actor.NativeIndex).FirstOrDefault(actor => actor.NativePendingReaction.Stage is > 0 && actor.NativePendingReaction.Order == order);
                if (actor is null)
                {
                    continue;
                }
                byte stage = actor.NativePendingReaction.Stage!.Value;
                if (stage != 2 || (NativeSavedStatusBits(actor) & 1) == 0)
                {
                    QueueNativeCallback(actor, stage);
                }
            }
        }
        finally
        {
            flushingNativeReactions = false;
        }
    }
}
