using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class SmartAttackExecutionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void InstalledOrdinaryMagicUsesSelectedBankAndIndependentBattleBudget(bool shared, bool infinite, bool smart)
    {
        BattleSession battle = Create(shared, infinite, smart);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(infinite ? 10 : 9, battle.MagicVitals!.GetMagicCount(1, 1));
        Assert.Equal(infinite ? 1 : 0, shared ? battle.MagicVitals.MagicSystem.SharedBattleMasks[1] : battle.MagicVitals.MagicSystem.PersonalBattleMasks[1][1]);
        Assert.Equal(0, battle.MagicVitals.MagicSystem.SharedFieldMasks[1]);
        Assert.Equal(0, battle.MagicVitals.MagicSystem.PersonalFieldMasks[1][1]);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy");
        Assert.DoesNotContain(battle.Combatants, actor => actor.MagicRecordId == 0);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(0x20000u, 2, 3)]
    [InlineData(0x40000u, 3, 7)]
    public void SharedBoundRepeatedMagicReservesEachPerformedCastAcrossCheckpoint(uint status, int casts, int mask)
    {
        BattleSession battle = Create(true, true, false, status, bound: true, stock: 100);
        Assert.Contains(casts, battle.GetNativeMagicCastCounts("caster", 1));
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: Enumerable.Repeat("enemy", casts).ToArray());
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(casts, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Equal(100, battle.MagicVitals!.GetMagicCount(1, 1));
        Assert.Equal(mask, battle.MagicVitals.MagicSystem.SharedBattleMasks[1]);
        Assert.Empty(battle.MagicVitals.MagicSystem.Reservations);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void InstalledSmartAttackUsesMagicWithNonGunbladeCasterAndPreservesStock()
    {
        BattleSession battle = Create(true, true, true, enemyHp: 100);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("caster", "enemy");
        Assert.Equal(BattleAbility.Magic, preview.Ability);
        Assert.Equal(1, preview.ActionId);
        battle.QueueSmartAttack(preview);
        Complete(battle);
        Assert.Equal(10, battle.MagicVitals!.GetMagicCount(1, 1));
        Assert.Equal(1, battle.MagicVitals.MagicSystem.SharedBattleMasks[1]);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy");
    }

    private static BattleSession Create(bool shared, bool infinite, bool smart, uint status = 0, bool bound = false, int stock = 10, int enemyHp = 10_000)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "main.fs")), "Optional original battle resources are not configured.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        vitals = vitals with { CombatParty = [1, 255, 255] };
        if (bound)
        {
            vitals.Records[1] = vitals.Records[1] with { Junction = vitals.Records[1].Junction with { GuardianForces = 1 } };
            vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = vitals.GuardianForces[0].LearnedBank with { Low = vitals.GuardianForces[0].LearnedBank.Low | 2 } };
        }
        vitals = vitals.ChangeMagicStock(shared ? 0 : 1, 1, stock, MagicOperationReason.ScriptReward, out _);
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = shared, InfiniteMagic = infinite, SmartAttack = smart }).Vitals;
        if (bound)
        {
            vitals = vitals.SetJunctionMagic(1, 0, 1);
        }
        vitals.MagicSystem.AttackModes[1] = MagicAttackMode.SmartMagic;
        NativeCharacterVital character = vitals.Records[1];
        BattlePlayerStats stats = assets.ReadPlayerStats(vitals, 1);
        return new(new BattleDefinition
        {
            MagicVitals = vitals,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 1, MagicRecordId = 1, InitialAtb = BattleSession.AtbMaximum, MaximumHp = vitals.GetMagicMaximumHitPoints(1), CurrentHp = character.HitPoints.Minimum, Attack = 1, Magic = stats.Magic, NativeStatus1 = status, NativeDeathCount = 0, Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 1), NativePlayerStatRules = assets.ReadPlayerNativeStatRules(vitals, 1) with { NativeStatusPassiveFlags = 0 }, NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, MaximumHp = enemyHp, CurrentHp = enemyHp, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
    }
}
