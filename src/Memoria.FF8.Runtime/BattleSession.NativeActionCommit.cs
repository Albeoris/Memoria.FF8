namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private int nativeActionCommitDepth;
    private byte? nativeReactionOrder;

    private void BeginNativeReactionBatch()
    {
        //485460 LABEL3 resets only globalDF8 lowBYTE before dispatching one original event packet.
        nativeReactionOrder = 0;
    }

    private bool HasNativePendingReactions => combatants.Any(actor => actor.NativePendingReaction.Stage is 2 or 3);

    private void SetNativeReactionActionGate(bool value)
    {
        NativeBattleAllocationControl control = nativeScriptState.AllocationControl ?? throw new NotSupportedException("Native DF9 mutation requires the retained DF9/DFD allocation gate owner.");
        nativeScriptState = nativeScriptState with { AllocationControl = control with { ActionGate = value } };
    }

    private void CompleteNativeActionOwners(BattleCombatant actor, BattleActionPresentation command, bool silencedMagic)
    {
        CompleteNativeOrdinaryCommand(actor, command);
        bool continueAffinity = CompleteNativeActionInventory(actor, command, silencedMagic);
        if (continueAffinity && command.MagicCasts.IsDefaultOrEmpty)
        {
            CompleteGuardianForceAffinity(actor, command);
        }
    }

    private void QueueNativeReaction(BattleCombatant actor, int stage)
    {
        if (stage is not (2 or 3))
        {
            throw new InvalidDataException("Native494410 only produces pending stages2/3.");
        }
        if (nativeReactionOrder is null || !actor.IsPlayer && actor.NativeAi is null)
        {
            return;
        }
        if (stage == 2 && (nativeContactWorking?.CommittingSecondary == true || (resolvingNativeAction?.NativeQueuedCommand ?? resolvingNativeAction?.NativeAction?.Command) is 0 or 14 || !actor.IsPlayer && nativeActionScratch.EventLane == 0))
        {
            return;
        }
        byte order = nativeReactionOrder ?? throw new NotSupportedException("Native pending reaction requires its actual485460 dispatch-order BYTE.");
        actor.NativePendingReaction = new(checked((byte)stage), order);
        nativeReactionOrder = unchecked((byte)(order + 1));
        ObserveDiagnosticCounterReaction(actor, stage);
    }

    private void FlushExpandedEnemyReactions()
    {
        byte count = nativeReactionOrder ?? 0;
        nativeReactionOrder = null;
        foreach (BattleCombatant actor in combatants.Where(actor => actor.NativePendingReaction is { Stage: 2 or 3, Order: byte order } && order < count).OrderBy(actor => actor.NativePendingReaction.Order))
        {
            NativeBattlePendingReaction pending = actor.NativePendingReaction;
            byte stage = pending.Stage!.Value;
            actor.NativePendingReaction = new(0, 0);
            if (actor.IsActive && (stage == 3 || (NativeSavedStatusBits(actor) & 1) == 0))
            {
                if (actor.IsPlayer && stage == 2 && (!allies.Any(IsNativeBattleCapable) || !enemies.Any(IsNativeBattleCapable)))
                {
                    continue;
                }
                QueueExpandedAiCallback(actor, stage, pending);
            }
        }
    }

}
