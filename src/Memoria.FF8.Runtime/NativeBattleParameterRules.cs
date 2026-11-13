namespace Memoria.FF8.Runtime;

public sealed record NativeBattleParameterOperation(uint StatusMask, int ResistanceIndex, int VitalityReductionPercent, int SpiritReductionPercent);

/// <summary>Describes the native parameter status independently of its animation and numeric consumer.</summary>
public static class NativeBattleParameterRules
{
    public const uint DefenseReductionStatus = 0x1000000;

    public static NativeBattleParameterOperation? Describe(BattleNativeAction action, GameplayTuning? tuning = null)
    {
        if ((action.Statuses1 & DefenseReductionStatus) == 0)
        {
            return null;
        }
        bool tuned = UsesConfiguredReduction(action);
        return new(DefenseReductionStatus, 32, tuned ? tuning?.MeltdownVitalityReductionPercent ?? 100 : 100, tuned ? tuning?.MeltdownSpiritReductionPercent ?? 100 : 100);
    }

    internal static bool UsesConfiguredReduction(BattleNativeAction action)
    {
        return action is { Command: 2 or 247, ActionId: 49, KernelSection: 1, KernelIndex: 49 } && (action.Statuses1 & DefenseReductionStatus) != 0;
    }
}
