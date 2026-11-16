namespace Memoria.AutoPlay;

public enum AutoPlayBattleDecisionKind
{
    Tick,
    Return,
    Attack
}

public readonly record struct AutoPlayBattleActor(bool CanChooseCommand, bool IsReady, bool CanAct, bool IsBusy, bool HasAttack);
public readonly record struct AutoPlayBattleTarget(bool IsOpponent, bool IsActive, bool IsAlive, bool Untargetable);
public readonly record struct AutoPlayBattleDecision(AutoPlayBattleDecisionKind Kind, int ActorIndex = -1, int TargetIndex = -1);
public readonly record struct AutoPlayBattleRecoveryTarget(uint HitPoints, uint MaximumHitPoints, bool KnockedOut);
public readonly record struct AutoPlayBattleRecoveryOption(int CommandIndex, int TargetIndex, bool Revives);

/// <summary>Selects the first available ordinary attack in the host's native actor and target order.</summary>
public static class AutoPlayBattlePolicy
{
    public static bool ShouldRetreat(bool canEscape, bool recoveryAvailable, IReadOnlyList<AutoPlayBattleRecoveryTarget> targets)
    {
        if (!canEscape || recoveryAvailable)
        {
            return false;
        }
        bool living = false;
        foreach (AutoPlayBattleRecoveryTarget target in targets)
        {
            if (target.KnockedOut || target.HitPoints == 0)
            {
                continue;
            }
            if (target.MaximumHitPoints == 0 || (ulong)target.HitPoints * 4 > target.MaximumHitPoints)
            {
                return false;
            }
            living = true;
        }
        return living;
    }

    /// <summary>Stabilizes critical living allies, then restores knocked-out allies, then heals injured allies.</summary>
    public static AutoPlayBattleRecoveryOption? ChooseRecovery(IReadOnlyList<AutoPlayBattleRecoveryTarget> targets, IReadOnlyList<AutoPlayBattleRecoveryOption> options)
    {
        AutoPlayBattleRecoveryOption? urgent = ChooseHealing(4);
        if (urgent is not null)
        {
            return urgent;
        }
        foreach (AutoPlayBattleRecoveryOption option in options)
        {
            if (option.Revives && targets[option.TargetIndex].KnockedOut)
            {
                return option;
            }
        }
        return ChooseHealing(2);

        AutoPlayBattleRecoveryOption? ChooseHealing(uint thresholdDivisor)
        {
            AutoPlayBattleRecoveryOption? selected = null;
            foreach (AutoPlayBattleRecoveryOption option in options)
            {
                AutoPlayBattleRecoveryTarget target = targets[option.TargetIndex];
                if (option.Revives || target.KnockedOut || target.MaximumHitPoints == 0 || (ulong)target.HitPoints * thresholdDivisor > target.MaximumHitPoints)
                {
                    continue;
                }
                if (selected is null || (ulong)target.HitPoints * targets[selected.Value.TargetIndex].MaximumHitPoints < (ulong)targets[selected.Value.TargetIndex].HitPoints * target.MaximumHitPoints)
                {
                    selected = option;
                }
            }
            return selected;
        }
    }

    public static AutoPlayBattleDecision Choose(bool canReturn, bool isActive, string outcome, bool entryReady, bool paused, IReadOnlyList<AutoPlayBattleActor> actors, IReadOnlyList<AutoPlayBattleTarget> targets)
    {
        if (canReturn)
        {
            return new(AutoPlayBattleDecisionKind.Return);
        }
        if (!isActive)
        {
            throw new NotSupportedException($"Automatic combat stopped at its actual {outcome} outcome.");
        }
        if (entryReady && !paused)
        {
            int actorIndex = -1;
            for (int index = 0; index < actors.Count; index++)
            {
                AutoPlayBattleActor actor = actors[index];
                if (actor.CanChooseCommand && actor.IsReady && actor.CanAct && !actor.IsBusy && actor.HasAttack)
                {
                    actorIndex = index;
                    break;
                }
            }
            if (actorIndex >= 0)
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    AutoPlayBattleTarget target = targets[index];
                    if (target.IsOpponent && target.IsActive && target.IsAlive && !target.Untargetable)
                    {
                        return new(AutoPlayBattleDecisionKind.Attack, actorIndex, index);
                    }
                }
            }
        }
        return new(AutoPlayBattleDecisionKind.Tick);
    }
}
