using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleActionAssetsTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void InstalledKernelOwnsEarlySpellsPotionAndThreeSummonEffects()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == 1);
        Assert.Equal("Fire", fire.Name);
        Assert.Equal(18, fire.Power);
        Assert.Equal(1, fire.Element);
        BattleActionDefinition potion = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Id == 1);
        Assert.Equal(4, potion.Power);
        Assert.True(potion.Allies);
        Assert.Equal(new[] { "Thunder Storm", "Diamond Dust", "Hell Fire" }, assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce).Select(action => action.Name));
        Assert.Equal(new[] { 4, 2, 1 }, assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce).Select(action => action.Element));
    }

    [Theory]
    [InlineData(34)]
    [InlineData(37)]
    [InlineData(512)]
    [InlineData(515)]
    [InlineData(521)]
    public void BalambAndTrainingFormationEnemiesReadOriginalStatsDrawBandsAndAp(int formation)
    {
        using BattleAssets assets = new(GameDirectory);
        foreach (BattleEnemyPlacement enemy in assets.LoadEncounter(formation).Enemies)
        {
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(enemy.ModelId).Project(8);
            Assert.True(stats.MaximumHp > 0);
            Assert.Equal(8, stats.ElementDefenses.Length);
            Assert.All(assets.ReadEnemyDrawSpells(enemy.ModelId, 8), spell => Assert.InRange(spell.Id, 1, 63));
            Assert.InRange(assets.ReadEnemyAbilityPoints(enemy.ModelId), 0, 255);
            Assert.InRange(assets.ReadEnemyPrimitiveAttack(enemy.ModelId, 8).Power, 0, 255);
        }
    }

    [Theory]
    [InlineData(0, 273)]
    [InlineData(1, 261)]
    [InlineData(2, 296)]
    public void InstalledEarlyGuardianAttackDispatchesNativeLevelOneDamage(int id, int expectedDamage)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleSession session = new(new BattleDefinition
        {
            Name = "Installed GF effects with explicit diagnostic actors and seed0",
            NativeSeed = 0,
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [id], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce] },
                new() { Id = "enemy", Name = "Diagnostic opponent", MaximumHp = 10000, CurrentHp = 10000, Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
        session.QueueAction("party", "enemy", BattleAbility.GuardianForce, id);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            session.Tick();
        }
        Assert.Equal(expectedDamage, Assert.Single(session.Events, entry => entry.Kind == "Damage").Amount);
        Assert.Equal(10000 - expectedDamage, session.Combatants[1].CurrentHp);
    }

    [Fact]
    public void OriginalNewGameSquallStatsUseKernelCurvesWeaponAndCurrentSavedBonuses()
    {
        using GameAssets game = new(GameDirectory);
        using BattleAssets assets = new(GameDirectory);
        BattlePlayerStats stats = assets.ReadPlayerStats(game.ReadNewGameCharacterVitals(), 0);
        Assert.Equal((17, 6, 6, 5, 21, 16, 255, 5), (stats.Strength, stats.Vitality, stats.Magic, stats.Spirit, stats.Speed, stats.Luck, stats.Hit, stats.Evasion));
    }
}
