namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly Dictionary<string, (BattleCombatant Actor, int Stage, long Order)> pendingNativeReactions = [];
    private int nativeActionCommitDepth;
    private long nativeReactionOrder;
    private bool flushingNativeReactions;

    private void ResolveActionTransaction(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command)
    {
        nativeActionCommitDepth++;
        bool completed = false;
        try
        {
            nativeAffinityAction = null;
            ResolveAction(actor, target, command);
            CompleteNativeOrdinaryCommand(actor, command);
            CompleteNativeActionInventory(actor, command);
            if (command.NativeMagicTargets.IsDefaultOrEmpty)
            {
                CompleteGuardianForceAffinity(actor, command);
            }
            CompleteNativeOrdinaryCommandCost(actor, command);
            CompleteNativeActionStatuses(actor);
            completed = true;
        }
        finally
        {
            nativeActionCommitDepth--;
            if (nativeActionCommitDepth == 0)
            {
                if (completed)
                {
                    FlushNativeReactions();
                }
                else
                {
                    pendingNativeReactions.Clear();
                }
            }
        }
    }

    private void QueueNativeReaction(BattleCombatant actor, int stage)
    {
        if (actor.NativeAi is null)
        {
            return;
        }
        // Native494410 replaces the actor's pending stage and its order;47DE70 consumes one final receipt per actor.
        pendingNativeReactions[actor.Id] = (actor, stage, nativeReactionOrder++);
        if (nativeActionCommitDepth == 0)
        {
            FlushNativeReactions();
        }
    }

    private void FlushNativeReactions()
    {
        if (flushingNativeReactions)
        {
            return;
        }
        flushingNativeReactions = true;
        try
        {
            while (pendingNativeReactions.Count != 0)
            {
                var reaction = pendingNativeReactions.Values.MinBy(reaction => reaction.Order);
                BattleCombatant actor = reaction.Actor;
                int stage = reaction.Stage;
                pendingNativeReactions.Remove(actor.Id);
                if (stage == 2 && !actor.IsAlive || stage == 3 && actor.AiDeathHandled)
                {
                    continue;
                }
                if (stage == 3)
                {
                    actor.AiDeathHandled = true;
                }
                RunNativeAi(actor, stage);
            }
        }
        finally
        {
            flushingNativeReactions = false;
        }
    }
}
