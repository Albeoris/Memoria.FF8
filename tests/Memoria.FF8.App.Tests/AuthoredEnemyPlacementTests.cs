using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AuthoredEnemyPlacementTests
{
    [Fact]
    public void InstalledReserveChainsKeepTheirSceneSlotsIncludingReferencedModelZero()
    {
        using GameWorld world = OpenInstalledWorld();
        BattleEncounter encounter = world.BattleResources.LoadEncounter(164);
        Assert.Equal(new[] { 1, 2, 3 }, Invoke<HashSet<int>>(world, "ReferencedReserveSlots", encounter).Order());
        Assert.Equal(new[] { (0, 101), (1, 71), (2, 72), (3, 71) }, Placements(world, encounter).Select(slot => (slot.Slot, slot.ModelId)));

        BattleEncounter chain = world.BattleResources.LoadEncounter(508);
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, Invoke<HashSet<int>>(world, "ReferencedReserveSlots", chain).Order());
        BattleEnemyPlacement referencedZero = Assert.Single(Placements(world, chain), slot => slot.Slot == 7);
        Assert.Equal(0, referencedZero.ModelId);
        Assert.False(referencedZero.Enabled);
    }

    [Fact]
    public void UnusedDormantPrototypeDoesNotRequireOriginalSlotsAndSurvivesInAnOlderDefinition()
    {
        using GameWorld world = OpenInstalledWorld();
        BattleEncounter encounter = world.BattleResources.LoadEncounter(605);
        BattleEnemyPlacement dormant = Assert.Single(encounter.Slots, slot => slot.Slot == 2);
        Assert.Equal(71, dormant.ModelId);
        Assert.False(dormant.Enabled);
        Assert.Empty(Invoke<HashSet<int>>(world, "ReferencedReserveSlots", encounter));
        Assert.False(Invoke<bool>(world, "NeedsOriginalEnemySlots", encounter));
        Assert.Equal(new[] { 0, 1 }, Placements(world, encounter).Select(slot => slot.Slot));

        BattleDefinition legacy = new()
        {
            UsesOriginalEnemySlots = true,
            Combatants = encounter.Slots.Take(3).Select(slot => new BattleCombatantDefinition { Id = $"enemy-{slot.Slot}", Name = "Enemy", SceneSlot = slot.Slot, ModelId = slot.ModelId, InitialActive = slot.Enabled }).ToArray()
        };
        Assert.Equal(new[] { (0, 33), (1, 33), (2, 71) }, Placements(world, encounter, legacy).Select(slot => (slot.Slot, slot.ModelId)));
        TargetInvocationException missing = Assert.Throws<TargetInvocationException>(() => Placements(world, encounter, legacy with { Combatants = legacy.Combatants[1..] }));
        Assert.IsType<InvalidDataException>(missing.InnerException);
    }

    [Theory]
    [InlineData(true, -1, "missing reserve slot 1")]
    [InlineData(false, -1, "nonenemy model 15")]
    [InlineData(false, 239, "nonenemy model 255")]
    public void ReachedPrototypeMustBeAnInstalledEnemyPlacement(bool omit, int model, string message)
    {
        using GameWorld world = OpenInstalledWorld();
        BattleEncounter encounter = world.BattleResources.LoadEncounter(164);
        encounter = encounter with
        {
            Slots = encounter.Slots.Where(slot => !omit || slot.Slot != 1).Select(slot => slot.Slot == 1 ? slot with { ModelId = model } : slot).ToArray()
        };
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Placements(world, encounter));
        Assert.Contains(message, Assert.IsType<InvalidDataException>(error.InnerException).Message);
    }

    [Fact]
    public void DonorPlacementEligibilityIsIndependentOfAddressDependentSiblings()
    {
        using GameWorld world = OpenInstalledWorld();
        BattleEnemyPlacement independent = Assert.Single(world.BattleResources.LoadEncounter(136).Enemies);
        BattleEnemyPlacement physical = Assert.Single(world.BattleResources.LoadEncounter(105).Enemies);
        BattleEnemyPlacement exactEnemy = Assert.Single(world.BattleResources.LoadEncounter(508).Enemies, slot => slot.ModelId == 123);
        BattleEnemyPlacement[] siblings = [independent with { Slot = 2 }, physical with { Slot = 4 }, exactEnemy with { Slot = 7, Hidden = false, Untargetable = false }];
        BattleEncounter pool = new(136, 0, 0, 0, 0, siblings);
        BattleEnemyPlacement donor = Assert.Single(Invoke<BattleEnemyPlacement[]>(world, "EligibleAdditionalEnemyPlacements", pool, 136));
        Assert.Equal(siblings[0], donor);
    }

    private static BattleEnemyPlacement[] Placements(GameWorld world, BattleEncounter encounter, BattleDefinition? saved = null)
    {
        return Invoke<BattleEnemyPlacement[]>(world, "OriginalExpandedEnemyPlacements", encounter, saved);
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
}
