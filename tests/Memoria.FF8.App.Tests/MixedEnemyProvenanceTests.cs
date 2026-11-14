using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MixedEnemyProvenanceTests
{
    [Fact]
    public void InstalledMixedDefinitionReconstructsSparseOriginalsAndOrdinaryDonors()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("story-battle-164")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        BattleAssets assets = world.BattleResources;
        BattleEncounter encounter = assets.LoadEncounter(164);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources = Sources(assets, 8);
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        BattleCombatantDefinition[] originals = encounter.Slots.Take(4).Select(slot => new BattleCombatantDefinition
        {
            Id = $"enemy-{slot.Slot}", Name = "Original", SceneSlot = slot.Slot, ModelId = slot.ModelId, InitialActive = slot.Enabled
        }).ToArray();
        BattleCombatantDefinition[] donors = sources.Select(pair => new BattleCombatantDefinition
        {
            Id = $"enemy-{pair.Key}", Name = "Added", SceneSlot = pair.Key, ExpandedEnemyBaseStatistics = ImmutableArray.Create(0, 0, 0, 0, 0, 0),
            NativeAi = assets.ReadEnemyAi(assets.LoadEncounter(pair.Value.Formation).Enemies.Single().ModelId, 0)
        }).ToArray();
        BattleDefinition frozen = new()
        {
            UsesExpandedRoster = true, UsesOriginalEnemySlots = true, AddedEnemySources = sources, MagicVitals = vitals,
            Combatants = [.. world.CreateBattleParty(), .. originals, .. donors]
        };
        BattleDefinition current = world.CreateEncounterBattle(164, restoredDefinition: frozen);
        Assert.Equal(sources, current.AddedEnemySources);
        Assert.True(current.UsesOriginalEnemySlots);
        Assert.Equal(new int?[] { 0, 1, 2, 3, 8, 9 }, current.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.SceneSlot));
        foreach (BattleCombatantDefinition actor in current.Combatants.Where(actor => !actor.IsPlayer))
        {
            int slot = actor.SceneSlot!.Value;
            Assert.Equal(slot, actor.RewardSlot);
            Assert.Equal(assets.ReadEnemyNativeEntryFlags(actor.ModelId), actor.Information254);
            if (sources.TryGetValue(slot, out BattleEnemyRosterSource? source))
            {
                BattleEnemyPlacement donor = Assert.Single(assets.LoadEncounter(source.Formation).Enemies, enemy => enemy.Slot == source.SceneSlot);
                Assert.Equal(donor.ModelId, actor.ModelId);
                BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(donor.ModelId).Project(actor.Level);
                Assert.Equal(stats.MaximumHp, actor.MaximumHp);
                Assert.Equal(stats.BaseStatistics, actor.ExpandedEnemyBaseStatistics!.Value.ToArray());
                Assert.Null(actor.NativeTemplate);
                Assert.Null(actor.NativeAi!.DeathSequenceReleasesAllocation);
                Assert.Equal(JsonSerializer.Serialize(assets.ReadEnemyAi(actor.ModelId, actor.Level)), JsonSerializer.Serialize(actor.NativeAi));
            }
            else
            {
                Assert.Equal(encounter.Slots[slot].ModelId, actor.ModelId);
                Assert.NotNull(actor.NativeTemplate);
                Assert.Null(actor.ExpandedEnemyBaseStatistics);
            }
        }

        BattleDefinition roundTripped = JsonSerializer.Deserialize<BattleDefinition>(JsonSerializer.Serialize(current))!;
        BattleDefinition rebound = Invoke<BattleDefinition>(world, "BindOriginalEnemyAiResources", roundTripped);
        GameWorld.ValidateBattleDefinition(current, rebound);
        GameWorld.ValidateBattleDefinition(current, world.CreateEncounterBattle(164, restoredDefinition: rebound));

        BattleScenePresentation scene = new(assets, rebound, true);
        var positions = (Dictionary<string, Vector3>)typeof(BattleScenePresentation).GetField("positions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
        Assert.Equal(encounter.Slots[3].Position, positions["enemy-3"]);
        Assert.Equal(new Vector3(-12, 0, 27), positions["enemy-8"]);
        Assert.Equal(new Vector3(0, 0, 27), positions["enemy-9"]);
        BattleStageCamera camera = GameWorld.ExpandedBattleCamera(current.Combatants.Count(actor => actor.InitialActive));
        Assert.Equal(2 * MathF.Atan(108f / camera.FocalLength), scene.Camera.FieldOfView);
    }

    [Fact]
    public void ResolverValidatesSeparateKeyNamespacesAndInstalledDonorFacts()
    {
        using GameWorld world = OpenInstalledWorld();
        BattleAssets assets = world.BattleResources;
        BattleEncounter encounter = assets.LoadEncounter(164);
        BattleEnemyPlacement[] originals = [encounter.Slots[0], encounter.Slots[3]];
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> mixed = Sources(assets, 8);
        Assert.Equal(new[] { 0, 3, 8, 9 }, Resolve(world, encounter, mixed, true, originals).Select(slot => slot.Slot));
        Assert.Equal(Enumerable.Range(0, 10), Resolve(world, encounter, mixed, true).Select(slot => slot.Slot));
        Assert.Equal(new[] { 0, 1, 2 }, Resolve(world, encounter, Sources(assets, 1), false).Select(slot => slot.Slot));
        AssertInvalid(mixed.Remove(8), originals, "contiguous scene slots");
        AssertInvalid(mixed, [originals[0], originals[0]], "unique formation prototype indices");
        AssertInvalid(mixed, [originals[0] with { Slot = 8 }], "unique formation prototype indices");
        AssertInvalid(mixed.SetItem(8, mixed[8] with { FormationHash = new string('A', 64) }), originals, "installed source formation");
        AssertInvalid(mixed.SetItem(8, Source(assets, 105)), originals, "eligible independent installed placement");

        void AssertInvalid(ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources, BattleEnemyPlacement[] input, string message)
        {
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Resolve(world, encounter, sources, true, input));
            Assert.Contains(message, Assert.IsType<InvalidDataException>(error.InnerException).Message);
        }
    }

    [Fact]
    public void MixedResultUsesPrototypeAndDonorRewardsAndConfirmsOnce()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        NativeEconomy economy = game.ReadNewGameEconomy();
        const int formation = 605;
        BattleEncounter encounter = assets.LoadEncounter(formation);
        ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources = Sources(assets, 8);
        BattleEncounterOccurrence occurrence = new("mixed-component-result", formation, assets.FormationSourceHash(formation), SceneKind.Field, 74, 0, new string('A', 64), null);
        byte[] rows = vitals.GetBattlePartyRows(3);
        BattlePartyResult[] party = rows.Select(row =>
        {
            NativeCharacterVital member = vitals.Records[row];
            return new BattlePartyResult(row, member.CharacterId, member.HitPoints.Minimum, member.HitPoints.Minimum, member.Status, member.Status, []);
        }).ToArray();
        BattleEnemyDefeat[] defeats = new[] { 2, 2, 8, 9 }.Select((slot, index) =>
            new BattleEnemyDefeat(slot, 8, BattleDefeatCredit.Character, rows[0], null, false, -1) { AllocationId = index < 2 ? $"native-allocation-{index + 1}" : null }).ToArray();
        BattleResult result = new(occurrence, BattleOutcome.Victory, party, [], [2, 8, 9], BattleResultProjection.EligibleGuardianForces(vitals), false, BattleRewardPolicy.NativeCombat, defeats)
        {
            UsesExpandedRoster = true, UsesOriginalEnemySlots = true, AddedEnemySources = sources, ExpandedEnemyCount = 5
        };
        result = JsonSerializer.Deserialize<BattleResult>(JsonSerializer.Serialize(result))!;
        Assert.Equal(10, result.EffectiveEnemySlotCount);
        Dictionary<int, int> models = new() { [2] = encounter.Slots[2].ModelId };
        foreach ((int slot, BattleEnemyRosterSource donor) in sources)
        {
            models.Add(slot, Assert.Single(assets.LoadEncounter(donor.Formation).Enemies, enemy => enemy.Slot == donor.SceneSlot).ModelId);
        }
        BattleResultOwners expected = BattleResultProjection.Apply(result, vitals, economy, models.ToDictionary(pair => pair.Key, pair => assets.ReadEnemyAbilityPoints(pair.Value)),
            models.ToDictionary(pair => pair.Key, pair => assets.ReadEnemyRewards(pair.Value)), game.ReadNativeBattleExperienceDefinition(), null, NativeBattleRewardDistribution.NoExperience(0, encounter.Flags));
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = vitals, Economy = economy });
        session.SetPendingBattleEncounter(occurrence);
        using GameWorld world = new(session, directory);
        world.ApplyBattleResult(occurrence, result);
        Assert.Equal(JsonSerializer.Serialize(expected.Rewards), JsonSerializer.Serialize(world.LastBattleRewards));
        Assert.Equal(defeats.Sum(defeat => assets.ReadEnemyAbilityPoints(models[defeat.Slot])), world.LastBattleRewards!.GuardianForceAbilityPoints[0]);
        string committed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(occurrence, result));
        Assert.Equal(committed, JsonSerializer.Serialize(session.State));
    }

    private static BattleEnemyPlacement[] Resolve(GameWorld world, BattleEncounter encounter, ImmutableSortedDictionary<int, BattleEnemyRosterSource> sources, bool originalSlots, BattleEnemyPlacement[]? originals = null)
    {
        return Invoke<BattleEnemyPlacement[]>(world, "ResolveExpandedEnemyPlacements", encounter, sources, originalSlots, originals);
    }

    private static T Invoke<T>(GameWorld world, string method, params object?[] arguments)
    {
        return (T)typeof(GameWorld).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, arguments)!;
    }

    private static GameWorld OpenInstalledWorld()
    {
        string directory = TestResources.GameDirectory;
        return new GameWorld(GameWorld.CreateColdProcess(directory), directory);
    }

    private static ImmutableSortedDictionary<int, BattleEnemyRosterSource> Sources(BattleAssets assets, int firstSlot)
    {
        return ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty.Add(firstSlot, Source(assets, 87)).Add(firstSlot + 1, Source(assets, 136));
    }

    private static BattleEnemyRosterSource Source(BattleAssets assets, int formation)
    {
        return new(formation, Assert.Single(assets.LoadEncounter(formation).Enemies).Slot, assets.FormationSourceHash(formation));
    }
}
