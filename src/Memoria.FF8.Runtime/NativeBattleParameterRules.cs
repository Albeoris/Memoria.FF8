using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleParameterOperation(uint StatusMask, int ResistanceIndex, int VitalityReductionPercent, int SpiritReductionPercent);

/// <summary>Projects the native Vit0 status for battle hosts with defensive parameter slots.</summary>
public static class NativeBattleParameterRules
{
    public const uint DefenseReductionStatus = 1u << ((int)NativeStatusId.Vit0 - 16);
    public const int DefenseReductionResistanceIndex = (int)NativeStatusId.Vit0 - 8;

    public static NativeBattleParameterOperation? Describe(BattleNativeAction action, ImmutableArray<StatusStatModifier> rules = default)
    {
        if ((action.Statuses1 & DefenseReductionStatus) == 0)
        {
            return null;
        }
        if (rules.IsDefaultOrEmpty)
        {
            rules = StatusStatRules.Native;
        }
        return new(DefenseReductionStatus, DefenseReductionResistanceIndex, GetReduction(BattleStatId.Vitality), GetReduction(BattleStatId.Spirit));

        int GetReduction(BattleStatId stat)
        {
            return StatusStatRules.MaximumPercent - StatusStatRules.Apply(rules, stat, StatusStatRules.MaximumPercent, 0, DefenseReductionStatus);
        }
    }
}
