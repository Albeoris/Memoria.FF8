using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static int ResolveMagicPreparationHitPoints(BattleNativeAction action, bool partyRecipient, int maximumHitPoints)
    {
        if (maximumHitPoints is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));
        }
        return partyRecipient && action.Power > 0 && action.AttackType is 3 or 32 ? 1 : maximumHitPoints;
    }
}
