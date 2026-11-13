namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool IsNativeMeltdown(BattleNativeAction action)
    {
        return NativeBattleParameterRules.UsesConfiguredReduction(action);
    }

    private static int EffectiveNativeDefense(BattleCombatant target, int defense, bool physical)
    {
        return (NativeBattleStatusBits(target) & 0x1000000) != 0 ? ReduceMeltdownDefense(defense, (physical ? target.MeltdownVitalityReductionPercent : target.MeltdownSpiritReductionPercent) ?? 100) : defense;
    }

    private static int ReduceMeltdownDefense(int defense, int reductionPercent)
    {
        return defense * (100 - reductionPercent) / 100;
    }
}
