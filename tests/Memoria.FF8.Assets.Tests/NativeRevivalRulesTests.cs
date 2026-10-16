using Memoria.FF8.Runtime;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRevivalRulesTests
{
    [Theory]
    [InlineData(0, 125)]
    [InlineData(2, 250)]
    public void Native_item_revival_uses_quarter_max_HP_only_with_the_equipped_item_passive(uint passive, int expectedHp)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { NativeStatusPassiveFlags = passive };
        BattleActionDefinition item = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Item && action.NativeAction?.AttackType == 5);
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeFieldBattleRules = 0,
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            ActionDefinitions = [item],
            Items = [new(item.Id, 1)],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, Level = rules.Level, Abilities = [BattleAbility.Item], InitialAtb = BattleSession.AtbMaximum, NativePlayerStatRules = rules, NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40] },
                new() { Id = "ko", Name = "KO", IsPlayer = true, MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
        battle.QueueAction("caster", "ko", BattleAbility.Item, item.Id);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(expectedHp, battle.Combatants.Single(actor => actor.Id == "ko").CurrentHp);
    }

    [Theory]
    [InlineData(5, 0, 125, true)]
    [InlineData(5, 0x40, 125, false)]
    [InlineData(6, 0, 1000, true)]
    [InlineData(6, 0x40, 1000, false)]
    public void Native_field_prohibition_retains_KO_while_the_revive_formula_still_returns_its_HP(byte type, byte rules, int hp, bool alive)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition life = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == type);
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeFieldBattleRules = rules,
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            ActionDefinitions = [life],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, Abilities = [BattleAbility.Magic], MagicStocks = [new(life.Id, 2)], InitialAtb = BattleSession.AtbMaximum, NativeStatusResistances = new byte[40] },
                new() { Id = "ko", Name = "KO", IsPlayer = true, MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
        battle.QueueAction("caster", "ko", BattleAbility.Magic, life.Id);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }

        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == "ko");
        Assert.Equal(hp, target.CurrentHp);
        Assert.Equal(alive, target.IsAlive);
        Assert.Equal(alive ? 0 : 1, target.PersistentStatus & 1);
    }
}
