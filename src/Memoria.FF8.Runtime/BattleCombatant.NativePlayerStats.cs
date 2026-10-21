using System.Collections.Immutable;

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
        IReadOnlyList<NativeMagicStock> stock = MagicOwner is not null && MagicRecordId is int row ? MagicOwner().GetMagicInventory(row) : NativeMagicSlots;
        if (MagicOwner is not null && MagicRecordId is int record)
        {
            NativeCharacterVitals owner = MagicOwner();
            rules = rules with { MagicJunctions = owner.Records[record].Junction.MagicJunctions.ToImmutableArray(), Abilities = owner.Records[record].Junction.Abilities };
        }
        BattleNativeCharacterGrowth growth = NativeGrowth ?? throw new InvalidDataException("Native player statistic projection requires the current saved growth owner.");
        BattleNativePlayerStatistics stats = rules.Project(stock, growth);
        System.Collections.Immutable.ImmutableArray<byte> resistance = rules.ProjectStatusResistances(stock);
        BattleNativeAction? attack = definition.NativeAttack is { } source ? rules.ProjectAttack(source, stock, stats) : null;
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
