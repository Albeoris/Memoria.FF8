using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MagicSystemSettlementTests
{
    [Fact]
    public void Aggregate_shared_bank_settles_one_final_draw_unit_without_record_zero_in_party()
    {
        NativeCharacterVitals before = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 99), shared: true);
        before = before with { CombatParty = [1, 2, 255] };
        NativeCharacterVitals after = before.ChangeMagicStock(1, 1, 10, MagicOperationReason.DrawStock, out MagicStockMutation first);
        after = after.ChangeMagicStock(2, 1, 10, MagicOperationReason.DrawStock, out MagicStockMutation second);
        Assert.Equal(1, first.Accepted);
        Assert.Equal(0, second.Accepted);
        BattleResult result = Result(before, after);
        result = result with { Party = result.Party.Select(member => member with { MagicChanges = [new BattleMagicChange(1, 99, 1)] }).ToArray() };
        NativeCharacterVitals settled = Project(result, before);
        Assert.Equal(100, settled.GetMagicCount(1, 1));
        Assert.Equal(0, settled.Records[1].MagicInventory.Sum(stock => stock.Count));
        Assert.Equal(0, settled.Records[2].MagicInventory.Sum(stock => stock.Count));
        Assert.Throws<InvalidDataException>(() => Project(result, settled));
    }

    [Fact]
    public void Extension_and_history_settle_together_and_recovery_happens_once()
    {
        NativeCharacterVitals before = MagicSystemFixture.Create();
        for (int spell = 1; spell <= 40; spell++)
        {
            before = MagicSystemFixture.Stock(before, spell <= 32 ? 0 : 1, spell, 10);
        }
        before = MagicSystemFixture.Modes(before, shared: true, infinite: true);
        before = MagicCastingService.Commit(MagicCastingService.Reserve(before, 1, 40, MagicCastContext.Field, "field"), "field");
        before = before with { CombatParty = [1, 2, 255] };
        NativeCharacterVitals after = MagicCastingService.BeginEncounter(before, "settlement");
        after = after.ChangeMagicStock(1, 40, 20, MagicOperationReason.DrawStock, out _);
        after = MagicCastingService.Commit(MagicCastingService.Reserve(after, 2, 40, MagicCastContext.Battle, "battle-cast"), "battle-cast");
        BattleResult result = Result(before, after);
        NativeCharacterVitals settled = Project(result, before);
        Assert.Equal(30, settled.GetMagicCount(2, 40));
        Assert.Equal(0, settled.MagicSystem.SharedFieldMasks[40]);
        Assert.Equal(1, settled.MagicSystem.SharedBattleMasks[40]);
        Assert.Contains("settlement", settled.MagicSystem.RecoveredResultIds);
        Assert.Same(settled, MagicCastingService.RecoverVictory(settled, "settlement"));
    }

    [Fact]
    public void Aggregate_receipt_rejects_rule_changes_and_dormant_stock_overwrite()
    {
        NativeCharacterVitals before = MagicSystemFixture.Modes(MagicSystemFixture.Stock(MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 1, 100), 2, 1, 20), shared: true);
        NativeCharacterVitals after = before.DeepCopy();
        BattleMagicSystemReceipt receipt = Receipt(before, after);
        Assert.Throws<InvalidDataException>(() => (receipt with { After = receipt.After with { InfiniteMagic = true, HistoriesInitialized = true } }).Validate());
        after.Records[2].MagicInventory[0] = new NativeMagicStock(1, 19);
        Assert.Throws<InvalidDataException>(() => Receipt(before, after).Validate());
    }

    private static BattleMagicSystemReceipt Receipt(NativeCharacterVitals before, NativeCharacterVitals after) => new(before.MagicSystem.DeepCopy(), after.MagicSystem.DeepCopy(), before.Records.Select(row => row.MagicInventory.ToArray()).ToArray(), after.Records.Select(row => row.MagicInventory.ToArray()).ToArray());

    private static BattleResult Result(NativeCharacterVitals before, NativeCharacterVitals after)
    {
        string hash = new('0', 64);
        BattleEncounterOccurrence encounter = new("settlement", 1, hash, SceneKind.Field, 74, 1, hash, null);
        return new BattleResult(encounter, BattleOutcome.Victory, before.CombatParty.Where(row => row != 255).Select(row => new BattlePartyResult(row, before.Records[row].CharacterId, 10, 10, 0, 0, [])).ToArray(), [], [], BattleResultProjection.EligibleGuardianForces(before), false, BattleRewardPolicy.NativeCombat, []) { MagicSystemReceipt = Receipt(before, after), NativeRewardItems = [] };
    }

    private static NativeCharacterVitals Project(BattleResult result, NativeCharacterVitals before)
    {
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        NativeBattleExperienceDefinition experience = new(before.KernelSha256, ImmutableArray.CreateRange(new uint[20]));
        return BattleResultProjection.Apply(result, before, economy, new Dictionary<int, int>(), new Dictionary<int, BattleEnemyRewardDefinition>(), experience, null, false).CharacterVitals;
    }
}
