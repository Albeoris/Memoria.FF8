using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class InstalledNativeActionExecutionTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Theory]
    [InlineData(0, 273)]
    [InlineData(1, 261)]
    [InlineData(2, 296)]
    public void InstalledEarlyGfsRetainTheirExactOriginalDamage(int id, int expected)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleSession session = Create(assets, BattleAbility.GuardianForce, id);
        Execute(session, BattleAbility.GuardianForce, id);
        Assert.Equal(expected, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
    }

    [Fact]
    public void DiablosUsesCurrentGfLevelAndOriginalGravityImmunity()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleSession normal = Create(assets, BattleAbility.GuardianForce, 5, level: 50);
        Execute(normal, BattleAbility.GuardianForce, 5);
        Assert.Equal(5_000, Assert.Single(normal.Events, item => item.Kind == "Damage").Amount);
        BattleSession immune = Create(assets, BattleAbility.GuardianForce, 5, level: 50, enemyFlags: 0x10000);
        Execute(immune, BattleAbility.GuardianForce, 5);
        Assert.Equal(10_000, immune.Combatants[1].CurrentHp);
    }

    [Theory]
    [InlineData(6, 0x80u)]
    [InlineData(9, 0x60000u)]
    public void SupportGfsApplyNativeStatusesToPartyInsteadOfEnemy(int id, uint expected)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleSession session = Create(assets, BattleAbility.GuardianForce, id);
        Execute(session, BattleAbility.GuardianForce, id, "party-0");
        BattleSnapshot saved = session.Capture();
        Assert.Equal(expected, saved.Combatants[0].NativeStatus!.Battle & expected);
        Assert.Equal(0u, saved.Combatants[1].NativeStatus!.Battle & expected);
        Assert.Equal(10_000, session.Combatants[1].CurrentHp);
    }

    [Fact]
    public void CactuarRetainsWideDamageCapAndNativeIntegerLevelSteps()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleSession session = Create(assets, BattleAbility.GuardianForce, 13, level: 100, enemyHp: 100_000);
        Execute(session, BattleAbility.GuardianForce, 13);
        Assert.Equal(10_000, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(90_000, session.Combatants[1].CurrentHp);
    }

    [Fact]
    public void MeteorExecutesAllTenNativeRandomHitsAndConsumesOneStock()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleActionDefinition meteor = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Name == "Meteor");
        BattleSession session = Create(assets, BattleAbility.Magic, meteor.Id, enemyHp: 100_000);
        Execute(session, BattleAbility.Magic, meteor.Id);
        Assert.Equal(10, session.Events.Count(item => item.Kind == "Damage"));
        Assert.Equal(1, Assert.Single(session.Combatants[0].MagicStocks).Count);
    }

    private static BattleSession Create(BattleAssets assets, BattleAbility ability, int id, int level = 1, uint enemyFlags = 0, int enemyHp = 10_000)
    {
        return new BattleSession(new BattleDefinition
        {
            NativeStatusRules = assets.ReadNativeStatusRules(0), NativeBattleSpeed = 0, GuardianForces = Enumerable.Range(0, 16).Select(gf => new BattleGuardianForceVital(gf, 1000, 1000)).ToArray(),
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.Select(action => action.Ability == BattleAbility.GuardianForce ? action with { Level = level } : action).ToArray(),
            Combatants =
            [
                new() { Id = "party-0", Name = "Caster", IsPlayer = true, Magic = 30, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [id], GuardianForceCompatibility = new ushort[16], Abilities = [BattleAbility.Attack, ability], MagicStocks = ability == BattleAbility.Magic ? [new BattleStock(id, 2)] : [], NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Opponent", MaximumHp = enemyHp, CurrentHp = enemyHp, Spirit = 5, Speed = 1, NativeCombatantFlags = enemyFlags, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static void Execute(BattleSession session, BattleAbility ability, int id, string target = "enemy")
    {
        session.QueueAction("party-0", target, ability, id);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            session.Tick();
        }
    }
}
