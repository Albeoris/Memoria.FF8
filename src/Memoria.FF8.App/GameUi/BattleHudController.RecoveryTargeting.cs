using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private string? SmartRecoveryTarget(bool drawSource)
    {
        if (drawSource || HasGuardianFormCommands || SelectedCommand is not (BattleAbility.Magic or BattleAbility.Item) && !SelectingDrawCastRecipient || SelectedActor is not { } actor || SelectedAction is not { } action || SelectingTargetGroup || !action.Allies || action.AllTargets)
        {
            return null;
        }
        int bestScore = 0;
        string? best = null;
        foreach (BattleCombatant target in SelectableTargets)
        {
            if (target.IsPlayer != actor.IsPlayer)
            {
                continue;
            }
            BattleStatusPresentation removable = session.ReadRemovableRecoveryStatuses(target.Id, action);
            int score = StatusRecoveryTargeting.Score(removable.Saved, removable.Battle);
            if (score > bestScore)
            {
                bestScore = score;
                best = target.Id;
            }
        }
        return best;
    }
}
