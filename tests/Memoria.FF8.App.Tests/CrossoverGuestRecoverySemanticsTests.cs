using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestRecoverySemanticsTests
{
    [Fact]
    public void Recovery_uses_original_formula_and_flags_instead_of_item_or_effect_identity()
    {
        BattleNativeAction fixedHp = Action(2, 4, 20);
        Assert.Null(CrossoverGuestHostActions.OriginItemUnsupportedReason(fixedHp));
        Assert.Equal((ushort)1000, CrossoverGuestHostActions.DescribeOriginItem(fixedHp, 0).HitPoints);
        Assert.Equal((ushort)2000, CrossoverGuestHostActions.DescribeOriginItem(fixedHp, 2).HitPoints);
        Assert.Equal((ushort)9999, CrossoverGuestHostActions.DescribeOriginItem(fixedHp with { Power = 255 }, 2).HitPoints);
        Assert.Equal(CrossoverGuestHostActions.DescribeOriginItem(fixedHp, 0), CrossoverGuestHostActions.DescribeOriginItem(fixedHp with { ActionId = 197, MagicId = 1234, Name = "Different source name" }, 0));
        BattleNativeAction revive = Action(198, 5, 0) with { TargetFlags = 21, Statuses0 = 1, StatusAttack = 200 };
        Assert.Equal(8, CrossoverGuestHostActions.DescribeOriginItem(revive, 0).ReviveDivisor);
        Assert.Equal(4, CrossoverGuestHostActions.DescribeOriginItem(revive, 2).ReviveDivisor);
        BattleNativeAction fraction = Action(33, 32, 8);
        Assert.Equal(8, CrossoverGuestHostActions.DescribeOriginItem(fraction, 0).MaximumHitPointFraction);
        Assert.Equal(8, CrossoverGuestHostActions.DescribeOriginItem(fraction, 2).MaximumHitPointFraction);
        Assert.Equal(2, CrossoverGuestHostActions.DescribeOriginItem(fraction, 2).RecoveryMultiplier);
        Assert.Equal(9999, CrossoverGuestHostActions.DescribeOriginItem(fraction, 0).FractionRecoveryLimit);
        Assert.Equal(60000, CrossoverGuestHostActions.DescribeOriginItem(fraction with { AttackFlags = 0xEA }, 0).FractionRecoveryLimit);
        BattleNativeAction remedy = Action(44, 4, 0) with { Statuses0 = 2, Statuses1 = 1, StatusAttack = 200 };
        Ff7BattleGuestItemEffect status = CrossoverGuestHostActions.DescribeOriginItem(remedy, 0);
        Assert.Equal(Ff7NativeBattleActionResolver.Poison | Ff7NativeBattleActionResolver.Sleep, status.RemoveStatuses);
        Assert.Equal(0, status.HitPoints);
    }

    [Fact]
    public void Unsupported_source_operations_are_reported_before_presentation_or_inventory_debit()
    {
        BattleNativeAction source = Action(2, 4, 20);
        foreach (BattleNativeAction unsupported in new[] { source with { AttackType = 1 }, source with { HitCount = 2 }, source with { TargetFlags = 0x34 }, source with { TargetFlags = 0x16 }, source with { Hit = 80 }, source with { Statuses0 = 2, StatusAttack = 50 }, source with { Statuses1 = 0x04000000 }, source with { Element = 1 } })
        {
            Assert.NotNull(CrossoverGuestHostActions.OriginItemUnsupportedReason(unsupported));
            Assert.Throws<NotSupportedException>(() => CrossoverGuestHostActions.DescribeOriginItem(unsupported, 0));
        }
        BattleNativeAction statusRecovery = source with { Statuses0 = 0x7E, Statuses1 = 0x0100560D, StatusAttack = 200 };
        Assert.Null(CrossoverGuestHostActions.OriginItemStatusUnsupportedReason(statusRecovery, 0, 0));
        Assert.NotNull(CrossoverGuestHostActions.OriginItemStatusUnsupportedReason(statusRecovery, 0x40, 0));
        Assert.NotNull(CrossoverGuestHostActions.OriginItemStatusUnsupportedReason(statusRecovery, 0, 0x01000000));
    }

    [Fact]
    public void Native_stock_and_action_records_select_the_menu_and_generic_receipts_restore_exactly()
    {
        NativeItemStock[] slots = Enumerable.Repeat(new NativeItemStock(0, 0), 198).ToArray();
        slots[4] = new(2, 2);
        slots[8] = new(33, 1);
        slots[12] = new(71, 1);
        BattleNativeAction[] actions = [Action(2, 4, 20), Action(33, 32, 8), Action(71, 4, 0) with { Command = 2 }, Action(9, 32, 16)];
        Assert.Equal(new ushort[] { 2, 33 }, CrossoverGuestHostActions.ReadOriginItemIds(slots, actions));
        Assert.False(CrossoverGuestItemInventory.Supports(0));
        Assert.False(CrossoverGuestItemInventory.Supports(256));
        Assert.True(CrossoverGuestItemInventory.Supports(2));
        GameplaySaveEconomy economy = new(slots, null, 0, 0, 0, 0);
        Ff7BattleGuestItemSelection selection = new(7, 10, 0, 4, 2);
        CrossoverGuestItemInventory reserved = CrossoverGuestItemInventory.Begin(economy).Reserve(selection);
        CrossoverGuestItemInventory restored = JsonSerializer.Deserialize<CrossoverGuestItemInventory>(JsonSerializer.Serialize(reserved))!.Restore(economy, 7, [0], 10);
        Assert.Equal(selection, Assert.Single(restored.AllPending).Selection);
        CrossoverGuestItemInventory completed = restored.Complete(0, 4, 2, refund: false);
        Assert.Equal(1, completed.Slots.Single(stock => stock.ItemId == 2).Count);
        Assert.Equal(selection, Assert.Single(completed.Ledger).Selection);
        Assert.Empty(completed.AllPending);
    }

    private static BattleNativeAction Action(ushort id, byte formula, byte power) => new(4, id, 7, id)
    {
        MagicId = 777,
        AttackType = formula,
        Power = power,
        HitCount = 1,
        TargetFlags = 20,
        AttackFlags = 0xE2,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 0,
        Statuses0 = 0,
        Statuses1 = 0,
        Hit = 255,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
