namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public void AdvanceNativeGraphicsPresentation(TimeSpan elapsed, TimeSpan? idleWorkBudget = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        if (idleWorkBudget is { } budget)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(budget.Ticks);
        }
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("A live battle requires its bound presentation.");
        RequireExpandedPresentationDispatchIdle();
        long callbacks = process.AccumulateElapsed(elapsed);
        if (GuardianAnimationSkipPending)
        {
            AdvanceSkippedGuardianFormPresentation(idleWorkBudget ?? TimeSpan.FromMilliseconds(8));
            return;
        }
        for (long count = callbacks; count > 0; count--)
        {
            DispatchTurnBasedFrame();
            if (GuardianAnimationSkipPending)
            {
                AdvanceSkippedGuardianFormPresentation(idleWorkBudget ?? TimeSpan.FromMilliseconds(8));
                break;
            }
        }
    }

    private static string RequireCanonicalGraphicsActorId(BattleModelOwner owner)
    {
        return owner switch
        {
            BattlePartyActorModel party => party.Actor.ActorId,
            BattleEnemyActorModel enemy => enemy.Actor.ActorId,
            _ => throw new InvalidDataException("A foreign graphics owner has no logical combatant.")
        };
    }
}
