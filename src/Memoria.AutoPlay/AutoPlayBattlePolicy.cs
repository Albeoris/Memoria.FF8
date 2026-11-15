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

/// <summary>Selects the first available ordinary attack in the host's native actor and target order.</summary>
public static class AutoPlayBattlePolicy
{
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
