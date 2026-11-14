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
        if (allies == 11)
        {
            Assert.False(battle.EscapeDisabled);
            battle.PublishNativeEscapeButtons(3);
            for (int tick = 0; tick < 1200 && battle.Outcome == BattleOutcome.Active; tick++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
            Assert.Equal((byte?)2, battle.NativeResult);
            Assert.Contains(battle.CaptureEscapeRemainingExperience(), receipt => receipt is { Slot: > 6, NativeIndex: null });
            Assert.Empty(world.PreviewBattleRewards().Items);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Null(session.State.PendingBattleEncounter);
            Assert.Null(session.State.RandomEncounters!.Pending);
            Assert.Single(session.State.AppliedBattleResultIds);
            return;
        }
        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.Empty(world.PreviewBattleRewards().Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorldRosterRestoresAgainstTheFrozenTerrainEncounterTable(bool hasSuspendedField)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState source = hasSuspendedField
            ? PlayerStateLoader.Load(TestResources.FixturePath("world-profile-return-source"), directory, sharedAssets: assets)
            : LoadBalambWorldSave(assets);
        GameSession session = new(source);
        using GameWorld world = new(session, directory, sharedAssets: assets, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(hasSuspendedField, world.Events.HasFieldProgram);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 1 });
        world.ApplyBattleInput(new(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
        world.ApplyBattleInput(new(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        if (!hasSuspendedField)
        {
            NativeWorldEncounterDefinition local = Assert.IsType<NativeWorldEncounterDefinition>(scene.ReadRandomEncounterDefinition(scene.State));
            RandomEncounterSessionState random = session.State.RandomEncounters!;
            foreach (ushort formation in local.Formations.Distinct())
            {
                session.SetRandomEncounters(random with { Pending = new(SceneKind.WorldMap, session.State.FieldId, scene.State.Frame, formation, scene.AllocationProgramSha256) });
                if (world.CreateEncounterBattle(formation).AddedEnemySources is { Count: > 0 })
                {
                    break;
                }
            }
        }
        else
        {
            for (int tick = 0; tick < 1200 && !world.HasPendingBattle; tick++)
            {
                int direction = tick / 8 % 4;
                float horizontal = direction == 0 ? 1 : direction == 2 ? -1 : 0;
                float vertical = direction == 1 ? 1 : direction == 3 ? -1 : 0;
                PlayerActionResult step = world.Apply(new(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical));
                Assert.True(step.Accepted, step.Detail);
                Assert.Equal(SceneKind.WorldMap, session.State.Scene);
            }
        }
        Assert.NotNull(session.State.RandomEncounters?.Pending);
        NativeWorldEncounterDefinition terrain = Assert.IsType<NativeWorldEncounterDefinition>(scene.ReadRandomEncounterDefinition(scene.State));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        BattleDefinition definition = world.Battle!.ReadDefinition();
        Assert.True(definition.AddedEnemySources is { Count: 1 }, $"Terrain {scene.State.GroundType}, formations {string.Join(", ", terrain.Formations)}, encounter {definition.EncounterId}.");
        Assert.Contains((ushort)Assert.Single(definition.AddedEnemySources).Value.Formation, terrain.Formations);

        GameState snapshot = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.PrepareSnapshot()))!;
        Assert.Equal(hasSuspendedField, snapshot.EventState.HasValue);
        if (!hasSuspendedField)
        {
            Assert.Null(snapshot.DepartingFieldState);
            Assert.NotNull(snapshot.WorldEventState);
        }
        AssertRestoredRoster(snapshot, directory, definition);
        AssertNonlocalDonorRejected(snapshot, directory, world.BattleResources, terrain.Formations);
    }

    private static GameState LoadBalambWorldSave(GameAssets assets)
    {
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, true);
        using WorldAssets terrain = new(assets.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 0));
        WorldSegment segment = terrain.GetSegment(WorldAssets.SegmentAt(26000, -29406), replacements);
        var ground = segment.Triangles.Where(triangle => triangle.GroundType == 4)
            .Select(triangle => (triangle.A + triangle.B + triangle.C) / 3)
            .Select(point => (X: (int)point.X, Z: (int)point.Z))
            .Select(point => (point.X, point.Z, Surface: terrain.LookupNativeGround(point.X, point.Z, replacements, [])))
            .First(point => point.Surface?.Ground.Triangle.GroundType == 4);
        document = document with { Resume = document.Resume with { World = document.Resume.World! with { X = ground.X, Z = ground.Z, Height = checked((short)-ground.Surface!.Ground.Height) } } };
        return GameplaySaveSessionLoader.Restore(document, assets.GameDirectory, sharedAssets: assets);
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
