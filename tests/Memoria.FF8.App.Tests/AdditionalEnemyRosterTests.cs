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
    [InlineData(4, 0, 1)]
    [InlineData(11, 10, 18)]
    [InlineData(3, 0, 1, BattleDifficulty.Hardcore)]
    [InlineData(5, 0, 3, BattleDifficulty.Hardcore)]
    [InlineData(3, 5, 5, BattleDifficulty.Hardcore)]
    [InlineData(4, 5, 6, BattleDifficulty.Hardcore)]
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
        if (allies > NativeCharacterVitals.NativeRecordCount)
        {
            vitals = vitals.WithExtendedRoster(true);
        }
        vitals = vitals with { Records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray() };
        byte[] selectedRows = Enumerable.Range(0, allies).Select(row => checked((byte)row)).ToArray();
        session.SetCharacterVitals(vitals.Rest().WithBattleParty(selectedRows, allies));
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = allies, AdditionalEnemies = storedEnemies, Difficulty = difficulty });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
        GameState? sourceField = allies == 3 && storedEnemies == 1 ? session.PrepareSnapshot() : null;
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));

        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition definition = battle.ReadDefinition();
        BattleEncounter original = world.BattleResources.LoadEncounter(formation);
        BattleCombatantDefinition[] enemies = definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive).ToArray();
        Assert.Equal(allies, definition.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(original.Enemies.Select(enemy => enemy.ModelId), enemies.Take(original.Enemies.Count).Select(enemy => enemy.ModelId));
        Assert.True(Math.Min(11, original.Enemies.Count + requested) == enemies.Length, $"original slots={definition.UsesOriginalEnemySlots}, sources={definition.AddedEnemySources?.Count}, field rate={world.Field?.RandomEncounters?.Rate}, alive enemy allocations={enemies.Length}.");
        Assert.NotNull(battle.Presentation);
        if (requested == 0)
        {
            Assert.NotNull(definition.AddedEnemySources);
            Assert.Empty(definition.AddedEnemySources);

            return;
        }
        Assert.NotNull(definition.AddedEnemySources);
        Assert.True(definition.UsesOriginalEnemySlots);
        Assert.Equal(enemies.Length - original.Enemies.Count, definition.AddedEnemySources.Count);
        Assert.Equal(Enumerable.Range(8, definition.AddedEnemySources.Count), definition.AddedEnemySources.Keys);
        Assert.All(definition.AddedEnemySources, entry =>
        {
            BattleEnemyRosterSource placement = entry.Value;
            Assert.Contains((ushort)placement.Formation, world.Field!.RandomEncounters!.Formations);
            Assert.Equal(world.BattleResources.FormationSourceHash(placement.Formation), placement.FormationHash);
            BattleEnemyPlacement donor = Assert.Single(world.BattleResources.LoadEncounter(placement.Formation).Enemies, candidate => candidate.Slot == placement.SceneSlot);
            BattleCombatantDefinition added = Assert.Single(enemies, enemy => enemy.SceneSlot == entry.Key);
            Assert.Equal(donor.ModelId, added.ModelId);
            Assert.Equal(world.BattleResources.ReadEnemyAbilityPoints(donor.ModelId), added.AbilityPoints);
        });
        BattleSnapshot before = battle.Capture();
        BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(before));
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(restored.Capture()));
        BattleDefinition rebuilt = world.CreateEncounterBattle(formation, 4, restoredDefinition: before.Definition);
        Assert.Equal(definition.AddedEnemySources, rebuilt.AddedEnemySources);
        Assert.Equal(enemies.Select(enemy => enemy.ModelId), rebuilt.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive).Select(enemy => enemy.ModelId));
        if (sourceField is not null)
        {
            Assert.Equal(storedEnemies, sourceField.GameplayPreferences.AdditionalEnemies);
            GameState configuredField = sourceField with { GameplayPreferences = sourceField.GameplayPreferences with { AdditionalEnemies = 10 } };
            using GameWorld fieldWorld = new(new GameSession(configuredField), directory);
            fieldWorld.EnsureField();
            BattleDefinition rebuiltAgainstNewPreferences = fieldWorld.CreateEncounterBattle(formation, 4, restoredDefinition: definition);
            Assert.Equal(definition.AddedEnemySources, rebuiltAgainstNewPreferences.AddedEnemySources);
            BattleDefinition altered = ReplaceDonorWithNonlocal(definition, world.BattleResources, world.Field!.RandomEncounters!.Formations);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => fieldWorld.CreateEncounterBattle(formation, 4, restoredDefinition: altered));
            Assert.Contains("outside the suspended exploration encounter table", error.Message);
        }
        if (allies == 11 || sourceField is not null)
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
        ExpandedBattleInstalledEntry.CompleteDebugVictory(world);
        Assert.Empty(world.PreviewBattleRewards().Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorldRosterRebuildsAgainstTheFrozenTerrainEncounterTable(bool hasSuspendedField)
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
        GameState sourceWorld = session.PrepareSnapshot();
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        BattleDefinition definition = world.Battle!.ReadDefinition();
        Assert.True(definition.AddedEnemySources is { Count: 1 }, $"Terrain {scene.State.GroundType}, formations {string.Join(", ", terrain.Formations)}, encounter {definition.EncounterId}.");
        Assert.Contains((ushort)Assert.Single(definition.AddedEnemySources).Value.Formation, terrain.Formations);

        Assert.Equal(hasSuspendedField, sourceWorld.EventState.HasValue);
        if (!hasSuspendedField)
        {
            Assert.Null(sourceWorld.DepartingFieldState);
            Assert.NotNull(sourceWorld.WorldEventState);
        }
        using GameWorld restored = new(new GameSession(sourceWorld), directory, validateWorldPresentation: false);
        restored.EnsureField();
        ushort encounter = checked((ushort)definition.EncounterId!.Value);
        BattleDefinition rebuilt = restored.CreateEncounterBattle(encounter, restoredDefinition: definition);
        Assert.Equal(definition.AddedEnemySources, rebuilt.AddedEnemySources);
        BattleDefinition altered = ReplaceDonorWithNonlocal(definition, world.BattleResources, terrain.Formations);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => restored.CreateEncounterBattle(encounter, restoredDefinition: altered));
        Assert.Contains("outside the suspended exploration encounter table", error.Message);
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

    private static BattleDefinition ReplaceDonorWithNonlocal(BattleDefinition definition, BattleAssets assets, IReadOnlyCollection<ushort> localFormations)
    {
        var selected = Assert.Single(definition.AddedEnemySources!);
        int model = assets.LoadEncounter(selected.Value.Formation).Enemies.Single(enemy => enemy.Slot == selected.Value.SceneSlot).ModelId;
        (int Formation, BattleEnemyPlacement Placement) donor = Enumerable.Range(0, assets.FormationCount)
            .Where(formation => !localFormations.Contains((ushort)formation))
            .SelectMany(formation => assets.LoadEncounter(formation).Enemies.Select(enemy => (Formation: formation, Placement: enemy)))
            .First(entry => entry.Placement.ModelId == model && entry.Placement.IsEnemyModel && !entry.Placement.Hidden && !entry.Placement.Untargetable && !entry.Placement.InitiallySuspended);
        BattleEnemyRosterSource source = new(donor.Formation, donor.Placement.Slot, assets.FormationSourceHash(donor.Formation));
        return definition with { AddedEnemySources = definition.AddedEnemySources!.SetItem(selected.Key, source) };
    }
}
