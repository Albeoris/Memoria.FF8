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
        if (allies == 3 && storedEnemies == 1)
        {
            GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.PrepareSnapshot()))!;
            Assert.Equal(storedEnemies, snapshot.GameplayPreferences.AdditionalEnemies);
            AssertRestoredRoster(snapshot, directory, definition);
            AssertRestoredRoster(snapshot with { GameplayPreferences = snapshot.GameplayPreferences with { AdditionalEnemies = 10 } }, directory, definition);
            AssertNonlocalDonorRejected(snapshot, directory, world.BattleResources, world.Field!.RandomEncounters!.Formations);
        }
        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.Empty(world.PreviewBattleRewards().Items);
    }

    [Fact]
    public void WorldRosterRestoresAgainstTheFrozenTerrainEncounterTable()
    {
        string directory = TestResources.GameDirectory;
        string path = TestResources.FixturePath("world-profile-return-source");
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets, validateWorldPresentation: false);
        world.EnsureField();
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 1 });
        world.ApplyBattleInput(new(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
        world.ApplyBattleInput(new(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
        for (int tick = 0; tick < 1200 && !world.HasPendingBattle; tick++)
        {
            int direction = tick / 8 % 4;
            float horizontal = direction == 0 ? 1 : direction == 2 ? -1 : 0;
            float vertical = direction == 1 ? 1 : direction == 3 ? -1 : 0;
            PlayerActionResult step = world.Apply(new(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical));
            Assert.True(step.Accepted, step.Detail);
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        }
        Assert.NotNull(session.State.RandomEncounters?.Pending);
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        NativeWorldEncounterDefinition terrain = Assert.IsType<NativeWorldEncounterDefinition>(scene.ReadRandomEncounterDefinition(scene.State));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        BattleDefinition definition = world.Battle!.ReadDefinition();
        Assert.Contains((ushort)Assert.Single(definition.AddedEnemySources!).Value.Formation, terrain.Formations);

        GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.PrepareSnapshot()))!;
        Assert.NotNull(snapshot.EventState);
        AssertRestoredRoster(snapshot, directory, definition);
        AssertNonlocalDonorRejected(snapshot, directory, world.BattleResources, terrain.Formations);
    }

    private static void AssertRestoredRoster(GameState snapshot, string directory, BattleDefinition expected)
    {
        using GameWorld restored = new(new GameSession(snapshot), directory, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.Equal(expected.AddedEnemySources, restored.Battle!.ReadDefinition().AddedEnemySources);
    }

    private static void AssertNonlocalDonorRejected(GameState snapshot, string directory, BattleAssets assets, IReadOnlyCollection<ushort> localFormations)
    {
        BattleSnapshot battle = snapshot.Battle!;
        var selected = Assert.Single(battle.Definition.AddedEnemySources!);
        int model = assets.LoadEncounter(selected.Value.Formation).Enemies.Single(enemy => enemy.Slot == selected.Value.SceneSlot).ModelId;
        (int Formation, BattleEnemyPlacement Placement) donor = Enumerable.Range(0, assets.FormationCount)
            .Where(formation => !localFormations.Contains((ushort)formation))
            .SelectMany(formation => assets.LoadEncounter(formation).Enemies.Select(enemy => (Formation: formation, Placement: enemy)))
            .First(entry => entry.Placement.ModelId == model && entry.Placement.IsEnemyModel && !entry.Placement.Hidden && !entry.Placement.Untargetable && !entry.Placement.InitiallySuspended);
        BattleEnemyRosterSource source = new(donor.Formation, donor.Placement.Slot, assets.FormationSourceHash(donor.Formation));
        BattleDefinition altered = battle.Definition with { AddedEnemySources = battle.Definition.AddedEnemySources!.SetItem(selected.Key, source) };
        using GameWorld restored = new(new GameSession(snapshot with { Battle = battle with { Definition = altered } }), directory, validateWorldPresentation: false);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => restored.EnsureField());
        Assert.Contains("outside the suspended exploration encounter table", error.Message);
    }
}
