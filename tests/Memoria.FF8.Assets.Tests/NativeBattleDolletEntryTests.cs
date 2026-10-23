using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDolletEntryTests
{
    [Theory]
    [InlineData(309, 30, 30, 31, 31)]
    [InlineData(310, 32, 33, 33, 32)]
    public void TowerApproachUsesItsOriginalRegionalEncounterTable(int fieldId, int first, int second, int third, int fourth)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeFieldEncounterDefinition encounters = Assert.IsType<NativeFieldEncounterDefinition>(assets.LoadField(fieldId).RandomEncounters);
        Assert.Equal((byte)12, encounters.Rate);
        Assert.Equal(new[] { first, second, third, fourth }, encounters.Formations.Select(value => (int)value));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public void OriginalRegionalFormationInitializesTakesATurnAndContinuesAfterJsonRestore(int encounterId)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        BattleSession battle = new(CreateDefinition(assets, game, encounterId), BattleAtbMode.Normal);
        battle.SetDifficulty(BattleDifficulty.Hard);
        for (int frame = 0; frame < 200; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Contains(battle.Capture().Combatants, actor => actor.Id.StartsWith("enemy-") && actor.NativeParticipant!.TurnCount > 0);
        BattleSession restored = Restore(battle);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(BattleDifficulty.Normal)]
    [InlineData(BattleDifficulty.Hard)]
    public void OriginalTowerTurnProgramKeepsWedgeSuspendedUntilBiggsFourthTurn(BattleDifficulty difficulty)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        BattleSession battle = new(CreateDefinition(assets, game, 29), BattleAtbMode.TurnBased);
        battle.SetDifficulty(difficulty);
        BattleCombatant biggs = battle.Combatants.Single(actor => actor.SceneSlot == 0);
        BattleCombatant wedge = battle.Combatants.Single(actor => actor.SceneSlot == 1);
        Assert.True(wedge.Suspended && wedge.Hidden && wedge.Untargetable);
        for (int frame = 0; ; frame++)
        {
            Assert.True(frame < 2000, "Biggs did not reach his fourth turn within the bounded continuation.");
            battle.Tick();
            byte turn = battle.Capture().Combatants.Single(actor => actor.Id == biggs.Id).NativeParticipant!.TurnCount;
            Assert.Equal(turn < 4, wedge.Suspended);
            Assert.Equal(turn < 4, wedge.Hidden);
            Assert.Equal(turn < 4, wedge.Untargetable);
            if (turn == 4)
            {
                break;
            }
        }
        Assert.False(battle.Combatants.Single(actor => actor.SceneSlot == 2).IsActive);
        Restore(battle);
    }

    private static BattleSession Restore(BattleSession battle)
    {
        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
        return restored;
    }

    private static BattleDefinition CreateDefinition(BattleAssets assets, GameAssets game, int encounterId)
    {
        BattleEncounter formation = assets.LoadEncounter(encounterId);
        List<BattleCombatantDefinition> actors = Enumerable.Range(0, 3).Select(slot => new BattleCombatantDefinition { Id = $"party-{slot}", Name = $"Party {slot}", ModelId = slot, IsPlayer = true, Level = 7, MaximumHp = 1_000_000, CurrentHp = 1_000_000, Speed = 1, NativeStatusResistances = Enumerable.Repeat((byte)100, 40).ToArray() }).ToList();
        IEnumerable<BattleEnemyPlacement> placements = encounterId == 29 ? formation.Slots.Take(3) : formation.Enemies;
        foreach (BattleEnemyPlacement placement in placements)
        {
            int level = placement.Enabled ? NativeBattleEnemyLevelRules.Resolve((byte)placement.Level, [7], 0, 0) : 0;
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
            actors.Add(new() { Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), ModelId = placement.ModelId, SceneSlot = placement.Slot, InitialActive = placement.Enabled, InitialHidden = placement.Hidden, InitialSuspended = placement.InitiallySuspended, InitialUntargetable = placement.Untargetable, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 10_000, InitialAtb = 10_000, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = assets.ReadEnemyAi(placement.ModelId, level), NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, (byte)placement.Level) });
        }
        return new() { Name = "Original Dollet formation", EncounterId = encounterId, StageId = formation.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), PersistentAiVariables = new uint[8], Combatants = actors.ToArray(), ActionDefinitions = assets.ActionDefinitions.ToArray(), NativeActions = assets.NativeActions.ToArray(), NativeStatusRules = assets.ReadNativeStatusRules(0), NativeMessageSpeed = 1, NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages() };
    }
}
