using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioDiscoveryAdmissionTests
{
    [Fact]
    public void Stocked_magic_uses_the_runtime_player_admission_instead_of_shared_kernel_membership()
    {
        BattleNativeAction action = Rapture();
        Assert.False(ScenarioDiscovery.SupportsOrdinaryStockMagicRecipe(action));
        Assert.False(ScenarioDiscovery.SupportsOrdinaryStockMagicRecipe(action with { MagicId = 1 }));
        Assert.True(ExpandedBattleActionRules.SupportsDrawnEnemyMagic(action));
        Assert.True(ScenarioDiscovery.IsTypedLimitScenario(action with { Command = 16, CharacterId = 5 }));
        BattleNativeAction damage = action with { Power = 20, Statuses1 = 0 };
        Assert.True(ScenarioDiscovery.SupportsOrdinaryStockMagicRecipe(damage));
        Assert.Equal(ExpandedBattleActionRules.SupportsPlayerAction(BattleAbility.Magic, damage), ScenarioDiscovery.SupportsOrdinaryStockMagicRecipe(damage));
        Assert.True(ScenarioDiscovery.SupportsOrdinaryStockMagicRecipe(action with { AttackType = 3, Statuses1 = 0x100520D }));
    }

    [Fact]
    public void Cached_catalog_admission_excludes_only_unsupported_stock_invocations_and_preserves_declared_slots_owner()
    {
        BattleNativeAction action = Rapture();
        InstalledScenarioInventory inventory = new(1, "", "", "", DateTimeOffset.UnixEpoch, [],
        [
            new("c002-a053", "magic", action, "installed action"),
            new("c016-a053", "limit", action with { Command = 16, CharacterId = 5 }, "installed owner"),
            new("c002-a001", "magic", action with { ActionId = 1, Power = 20, Statuses1 = 0 }, "installed action")
        ], [], []);
        Assert.Equal(new[] { "action-c002-a053" }, ScenarioDiscovery.UnsupportedStockMagicCaseIds(inventory));
    }

    private static BattleNativeAction Rapture()
    {
        return new(2, 53, 1, 53) { MagicId = 288, AttackType = 2, HitCount = 1, Power = 0, Element = 0, TargetFlags = 0x48, AttackFlags = 0x23, StatusAttack = 254, Statuses0 = 0, Statuses1 = 0x10000, DamagePresentation = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
    }
}
