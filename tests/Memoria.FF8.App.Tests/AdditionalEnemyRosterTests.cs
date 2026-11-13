using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AdditionalEnemyRosterTests
{
    [Theory]
    [InlineData(3, 0, 0)]
    [InlineData(3, 1, 1)]
    [InlineData(4, 0, 2)]
    [InlineData(11, 10, 10)]
    [InlineData(3, 0, 1, BattleDifficulty.Hardcore)]
    public void AreaRosterKeepsAuthoredEnemiesAndResumesItsSelectedPlacements(int allies, int storedEnemies, int requested, BattleDifficulty difficulty = BattleDifficulty.Normal)
    {
        const ushort formation = 87;
        string directory = TestResources.GameDirectory;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        session.SetCharacterVitals(vitals.Rest());
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allies, AdditionalEnemies = storedEnemies, Difficulty = difficulty });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition definition = battle.ReadDefinition();
        BattleEncounter original = world.BattleResources.LoadEncounter(formation);
        BattleCombatantDefinition[] enemies = definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive).ToArray();
        Assert.Equal(allies, definition.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(original.Enemies.Select(enemy => enemy.ModelId), enemies.Take(original.Enemies.Count).Select(enemy => enemy.ModelId));
        Assert.True(Math.Min(11, original.Enemies.Count + requested) == enemies.Length, $"Expanded={definition.UsesExpandedRoster}, original slots={definition.UsesOriginalEnemySlots}, sources={definition.AddedEnemySources?.Count}, field rate={world.Field?.RandomEncounters?.Rate}, alive enemy allocations={enemies.Length}.");
        Assert.Equal(requested > 0, battle.UsesExpandedRoster);
        if (requested == 0)
        {
            Assert.Null(definition.AddedEnemySources);
            Assert.NotNull(definition.NativeGraphics);
            return;
        }
        Assert.NotNull(definition.AddedEnemySources);
        Assert.Equal(enemies.Length - original.Enemies.Count, definition.AddedEnemySources.Count);
        Assert.All(definition.AddedEnemySources, entry =>
        {
            BattleEnemyRosterSource placement = entry.Value;
            Assert.Contains((ushort)placement.Formation, world.Field!.RandomEncounters!.Formations);
            Assert.Equal(world.BattleResources.FormationSourceHash(placement.Formation), placement.FormationHash);
            BattleEnemyPlacement donor = Assert.Single(world.BattleResources.LoadEncounter(placement.Formation).Enemies, candidate => candidate.Slot == placement.SceneSlot);
            Assert.Equal(donor.ModelId, enemies[entry.Key].ModelId);
            Assert.Equal(world.BattleResources.ReadEnemyAbilityPoints(donor.ModelId), enemies[entry.Key].AbilityPoints);
        });
        BattleSnapshot before = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(restored.Capture()));
        BattleDefinition rebuilt = world.CreateEncounterBattle(formation, 4, restoredDefinition: before.Definition);
        Assert.Equal(definition.AddedEnemySources, rebuilt.AddedEnemySources);
        Assert.Equal(enemies.Select(enemy => enemy.ModelId), rebuilt.Combatants.Where(actor => !actor.IsPlayer).Select(enemy => enemy.ModelId));
        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.Empty(world.PreviewBattleRewards().Items);
    }
}
