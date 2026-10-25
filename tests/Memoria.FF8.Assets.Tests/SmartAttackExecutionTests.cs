using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class SmartAttackExecutionTests
{
    [Theory]
    [InlineData(0x20000u, 2, false, false)]
    [InlineData(0x20000u, 2, false, true)]
    [InlineData(0x20000u, 2, true, false)]
    [InlineData(0x20000u, 2, true, true)]
    [InlineData(0x40000u, 3, false, false)]
    [InlineData(0x40000u, 3, false, true)]
    [InlineData(0x40000u, 3, true, false)]
    [InlineData(0x40000u, 3, true, true)]
    public void Installed_status_spell_enables_repeated_manual_and_smart_magic_after_json_restore(uint status, int casts, bool infinite, bool smart)
    {
        BattleSession battle = Create(true, infinite, smart, bound: infinite, stock: 100, statusSpell: status);
        BattleActionDefinition spell = battle.Capture().Definition.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.NativeAction?.Statuses1 == status);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        BattleCombatant support = battle.Combatants.Single(actor => actor.Id == "support");
        Assert.True(BattleSession.IsValidSelectedActionTarget(support, spell, caster));
        Assert.False(NativeBattleLimitRules.IsFixedAllyTarget(spell.NativeAction!.TargetFlags!.Value));
        battle.QueueAction("support", "caster", BattleAbility.Magic, spell.Id);
        Complete(battle);
        Assert.Equal(status, battle.Capture().Combatants.Single(actor => actor.Id == "caster").NativeStatus!.Battle & status);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        if (!restored.Combatants.Single(actor => actor.Id == "caster").IsReady)
        {
            restored.AdvanceTurn();
        }
        Assert.Contains(casts, restored.GetNativeMagicCastCounts("caster", 1));
        if (smart)
        {
            BattleSmartAttackPreview preview = restored.PreviewSmartAttack("caster", "enemy");
            Assert.Equal(BattleAbility.Magic, preview.Ability);
            Assert.Equal(1, preview.ActionId);
            Assert.Equal(casts, preview.CastCount);
            restored.QueueSmartAttack(preview);
        }
        else
        {
            restored.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: Enumerable.Repeat("enemy", casts).ToArray());
        }
        BattleSession queued = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(restored.Capture()))!);
        Complete(restored);
        Complete(queued);
        Assert.Equal(casts, restored.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Equal(infinite ? 100 : 100 - casts, restored.MagicVitals!.GetMagicCount(1, 1));
        Assert.Equal(infinite ? (1 << casts) - 1 : 0, restored.MagicVitals.MagicSystem.SharedBattleMasks[1]);
        Assert.Empty(restored.MagicVitals.MagicSystem.Reservations);
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(queued.Capture()));
    }

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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void InstalledSmartDrawUsesSeparateRecipientAndRestoresWithoutSpendingMagic(bool shared, bool infinite, bool drawOnce)
    {
        BattleDefinition definition = Create(shared, infinite, true, stock: 0).Capture().Definition;
        using GameAssets game = new(TestResources.GameDirectory);
        BattleCombatantDefinition caster = definition.Combatants[0] with { Abilities = [BattleAbility.Attack, BattleAbility.Draw] };
        BattleCombatantDefinition source = definition.Combatants[1] with { Id = "source", Name = "Source", ModelId = 3, NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] };
        BattleSession battle = new(definition with { DrawOncePerSpell = drawOnce, NativeKnownMagic = game.ReadNewGameKnownMagic().Add(1), Combatants = [caster, definition.Combatants[1], source] }, BattleAtbMode.TurnBased);
        string before = JsonSerializer.Serialize(battle.MagicVitals);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("caster", "enemy");
        Assert.Equal(BattleAbility.Draw, preview.Ability);
        Assert.Equal("source", preview.DrawSourceId);
        battle.QueueSmartAttack(preview);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Complete(battle);
        Complete(restored);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy" && item.Amount > 0);
        Assert.Equal(10_000, battle.Combatants.Single(actor => actor.Id == "source").CurrentHp);
        foreach (BattleSession completed in new[] { battle, restored })
        {
            BattleCombatant drawSource = completed.Combatants.Single(actor => actor.Id == "source");
            Assert.Equal(drawOnce, completed.Capture().Definition.DrawOncePerSpell);
            Assert.Equal(drawOnce, drawSource.DrawnMagicIds.Contains((byte)1));
            Assert.Equal(!drawOnce, drawSource.NativeDrawSlots.Any(slot => slot.Id == 1));
            BattleSmartAttackPreview next = completed.PreviewSmartAttack("caster", "enemy");
            Assert.Equal(drawOnce ? BattleAbility.Attack : BattleAbility.Draw, next.Ability);
            Assert.Equal(drawOnce ? null : "source", next.DrawSourceId);
        }
        Assert.Equal(before, JsonSerializer.Serialize(battle.MagicVitals));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession Create(bool shared, bool infinite, bool smart, uint status = 0, bool bound = false, int stock = 10, int enemyHp = 10_000, uint statusSpell = 0)
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
        if (statusSpell != 0)
        {
            BattleActionDefinition spell = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.NativeAction?.Statuses1 == statusSpell);
            vitals = vitals.ChangeMagicStock(shared ? 0 : 1, spell.Id, 10, MagicOperationReason.ScriptReward, out _);
        }
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
                .. statusSpell == 0 ? Array.Empty<BattleCombatantDefinition>() : [new() { Id = "support", Name = "Support", IsPlayer = true, ModelId = 3, MagicRecordId = 3, InitialAtb = BattleSession.AtbMaximum, MaximumHp = 1000, CurrentHp = 1000, NativeDeathCount = 0, Abilities = [BattleAbility.Magic], NativeStatusResistances = new byte[40] }],
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
