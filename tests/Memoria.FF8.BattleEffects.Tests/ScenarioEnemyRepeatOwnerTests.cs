using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioEnemyRepeatOwnerTests
{
    private static readonly byte[] NeutralInitialization = [14, 220, 0, 14, 221, 0, 14, 222, 0, 36, 0];

    [Fact]
    public void ProvedInitializedSelfKoOwnerRequiresASeparateNativeRepeatRecipe()
    {
        Assert.False(ScenarioDiscovery.TryValidateNativeEnemyRepeatOwner(SelfKo(), 200, new(0, 0, []), NeutralInitialization, out string? blocker));
        Assert.StartsWith("NON_REPEATABLE_NATIVE_OWNER:", blocker);
        Assert.False(ScenarioDiscovery.TryValidateNativeEnemyRepeatOwner(SelfKo() with { Command = 8, ActionId = 9, MagicId = 7, KernelIndex = 9 }, 200, new(0, 0, []), NeutralInitialization, out string? equivalent));
        Assert.Equal(blocker, equivalent);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("zombie")]
    [InlineData("petrify")]
    [InlineData("reflect")]
    [InlineData("immunity")]
    public void UnknownOrNonzeroInitialStatusesDoNotClaimTheSelfKoProof(string state)
    {
        NativeBattleStatusAllocation? status = state switch
        {
            "zombie" => new(0x40, 0, []),
            "petrify" => new(4, 0, []),
            "reflect" => new(0, 0x80, []),
            "immunity" => new(0, 0x800, []),
            _ => null
        };
        Assert.True(ScenarioDiscovery.TryValidateNativeEnemyRepeatOwner(SelfKo(), 200, status, NeutralInitialization, out string? blocker));
        Assert.Null(blocker);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 14, 220 })]
    [InlineData(new byte[] { 59, 200, 64, 0 })]
    [InlineData(new byte[] { 2, 0 })]
    public void UnprovedInitializationNeverCreatesARepeatOwnerBlock(byte[] initialization)
    {
        Assert.True(ScenarioDiscovery.TryValidateNativeEnemyRepeatOwner(SelfKo(), 200, new(0, 0, []), initialization, out string? blocker));
        Assert.Null(blocker);
    }

    [Theory]
    [InlineData("damage")]
    [InlineData("healing")]
    [InlineData("no_ko")]
    [InlineData("other_statuses")]
    [InlineData("resistible_status")]
    [InlineData("non_guaranteed_hit")]
    [InlineData("different_flags")]
    public void NativeFieldsOutsideTheProvedProfileRemainUnclassified(string difference)
    {
        BattleNativeAction action = difference switch
        {
            "damage" => SelfKo() with { Power = 20 },
            "healing" => SelfKo() with { AttackType = 3 },
            "no_ko" => SelfKo() with { Statuses0 = 0 },
            "other_statuses" => SelfKo() with { Statuses1 = 0x10000 },
            "resistible_status" => SelfKo() with { StatusAttack = 100 },
            "non_guaranteed_hit" => SelfKo() with { Hit = 70 },
            _ => SelfKo() with { AttackFlags = 1 }
        };
        Assert.True(ScenarioDiscovery.TryValidateNativeEnemyRepeatOwner(action, 200, new(0, 0, []), NeutralInitialization, out string? blocker));
        Assert.Null(blocker);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(205)]
    public void NonselfSelectorsDoNotAcquireAnEnemyOwnerConsumptionBlock(byte selector)
    {
        Assert.True(ScenarioDiscovery.TryValidateNativeEnemyRepeatOwner(SelfKo(), selector, new(0, 0, []), NeutralInitialization, out string? blocker));
        Assert.Null(blocker);
    }

    private static BattleNativeAction SelfKo() => new(236, 330, 3, 330)
    {
        MagicId = 218,
        AttackType = 2,
        Power = 0,
        HitCount = 1,
        TargetFlags = null,
        AttackFlags = 3,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 255,
        Statuses0 = 1,
        Statuses1 = 0,
        Hit = 255,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
