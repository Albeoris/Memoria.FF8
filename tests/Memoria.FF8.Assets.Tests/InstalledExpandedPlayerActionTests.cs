using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

public sealed class InstalledExpandedPlayerActionTests
{
    [Fact]
    public void MeteorChoosesEachHitBeforeDamageAndReachesTheLastEnemy()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition meteor = Spell(assets, "Meteor");
        Assert.Equal(10, meteor.NativeAction!.HitCount);
        Assert.Equal(2, meteor.NativeAction.TargetFlags!.Value & 2);
        BattleSession battle = Create(assets, meteor);
        battle.QueueAction("party-10", "enemy-0", BattleAbility.Magic, meteor.Id);
        NativeBattleRandomState random = battle.Capture().Random!;
        List<string> targets = [];
        for (int hit = 0; hit < meteor.NativeAction.HitCount; hit++)
        {
            random = random.Draw(out byte value);
            targets.Add($"enemy-{value % 11}");
        }
        Assert.Contains("enemy-10", targets);

        BattleSession restored = BattleSession.Restore(battle.Capture());
        Advance(battle, restored);

        Assert.Equal(targets, battle.Events.Where(item => item.Kind == "EffectContact").Select(item => item.TargetId));
        Assert.Equal(1, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party-10").MagicStocks).Count);
        Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer), enemy => Assert.Equal(targets.Contains(enemy.Id), enemy.CurrentHp < enemy.MaximumHp));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FireReflectsOnceFromTheLastEnemyAndPreservesTheOriginalCaster(bool drawn)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition fire = Spell(assets, "Fire");
        BattleSession battle = Create(assets, fire, enemyReflect: true);
        BattleAbility ability = drawn ? BattleAbility.Draw : BattleAbility.Magic;
        battle.QueueAction("party-10", drawn ? "enemy-9" : "enemy-10", ability, fire.Id, drawCast: drawn, drawCastTargetId: drawn ? "enemy-10" : null);

        battle.Tick();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Advance(battle, restored);

        Assert.Equal("enemy-10", Assert.Single(battle.Events, item => item.Kind == "Reflect").TargetId);
        BattleEvent damage = Assert.Single(battle.Events, item => item.Kind == "Damage");
        Assert.Equal("party-10", damage.ActorId);
        Assert.StartsWith("party-", damage.TargetId);
        Assert.Equal(new[] { "enemy-10", damage.TargetId }, battle.Events.Where(item => item.Kind == "EffectContact").Select(item => item.TargetId));
        BattleCombatantSnapshot redirected = Assert.Single(battle.Capture().Combatants, actor => actor.Id == damage.TargetId);
        Assert.Equal((byte)247, redirected.NativeParticipant!.LastAction!.Command);
        Assert.Equal(drawn ? 2 : 1, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party-10").MagicStocks).Count);
        Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer), enemy => Assert.Equal(enemy.MaximumHp, enemy.CurrentHp));
        Assert.Single(battle.Events, item => item.Kind == "ActionComplete");
    }

    private static BattleActionDefinition Spell(BattleAssets assets, string name)
    {
        return Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == name);
    }

    private static BattleSession Create(BattleAssets assets, BattleActionDefinition spell, bool enemyReflect = false)
    {
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"party-{index}", Name = $"Party {index}", IsPlayer = true, ModelId = index, MaximumHp = 10_000, CurrentHp = 10_000, Speed = 1
        }).ToArray();
        party[^1] = party[^1] with { Magic = 90, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic, BattleAbility.Draw], MagicStocks = [new(spell.Id, 2)] };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", MaximumHp = 10_000, CurrentHp = 10_000, Spirit = 5, Speed = 1,
            NativeStatus1 = enemyReflect && index == 10 ? 0x80u : 0,
            DrawSpells = index == 9 ? [new(spell.Id, 0)] : []
        }).ToArray();
        BattleDefinition definition = new()
        {
            Combatants = [.. party, .. enemies],
            ActionDefinitions = [spell],
            NativeActions = assets.NativeActions.Where(action => action.ActionId == spell.Id && action.Command is 2 or 247).ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0, 0]),
            InitialRandom = NativeBattleRandomState.ForecastPhase(8)
        };
        return new(definition, BattleAtbMode.TurnBased);
    }

    private static void Advance(BattleSession battle, BattleSession restored)
    {
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
