namespace Memoria.FF8.Runtime;

public sealed partial class BattleCombatant
{
    public BattleNativePlayerStatRules? NativePlayerStatRules => definition.NativePlayerStatRules;

    public void ReprojectNativePlayerStatistics()
    {
        if (definition.NativePlayerStatRules is not { } rules)
        {
            return;
        }
        BattleNativeCharacterGrowth growth = NativeGrowth ?? throw new InvalidDataException("Native player statistic projection requires the current saved growth owner.");
        BattleNativePlayerStatistics stats = rules.Project(NativeMagicSlots, growth);
        System.Collections.Immutable.ImmutableArray<byte> resistance = rules.ProjectStatusResistances(NativeMagicSlots);
        BattleNativeAction? attack = definition.NativeAttack is { } source ? rules.ProjectAttack(source, NativeMagicSlots, stats) : null;
        definition = definition with { MaximumHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = Math.Max(1, stats.Speed), Luck = stats.Luck, Hit = stats.Hit, Evasion = stats.Evasion, ElementDefenses = stats.ElementDefenses.ToArray(), ElementAttack = stats.ElementAttack, ElementAttackPercent = stats.ElementAttackPercent, NativeAttack = attack };
        RefreshNativeAtbSpeed();
        MutableNativeStatusResistances = resistance.ToArray();
        if (CurrentHp > MaximumHp)
        {
            CurrentHp = MaximumHp;
        }
        RefreshNativeHpStatusFlags();
    }
}
