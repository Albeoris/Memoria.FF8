using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AddedBattleEnemyLayoutTests
{
    [Theory]
    [InlineData(45)] // Ochu
    [InlineData(48)] // Malboro
    [InlineData(52)] // Ruby Dragon
    public void InstalledLargeModelsKeepClearOfTheAuthoredFrontAndOtherDonors(int model)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(221);
        BattleEnemyPlacement original = Assert.Single(encounter.Enemies);
        const int ordinaryModel = 9; // Mesmerize
        var resources = new[] { original.ModelId, model, ordinaryModel }.Distinct().ToDictionary(id => id, assets.ReadEnemyNativeGraphicsResource);
        var sizes = resources.ToDictionary(pair => pair.Key, pair => AddedBattleEnemyLayout.Measure(pair.Value));
        Assert.True(sizes[model].Large, $"Model {model} has footprint {sizes[model]}.");
        Assert.False(sizes[ordinaryModel].Large, $"Mesmerize and model {model} have footprints {sizes[ordinaryModel]} and {sizes[model]}.");

        BattleCombatantDefinition[] combatants =
        [
            Actor(original.Slot, original.ModelId), Actor(8, model), Actor(9, ordinaryModel), Actor(10, ordinaryModel), Actor(11, ordinaryModel), Actor(12, ordinaryModel)
        ];
        var sources = combatants.Skip(1).ToImmutableSortedDictionary(actor => actor.SceneSlot!.Value, _ => new BattleEnemyRosterSource(87, 0, new string('A', 64)));
        BattleDefinition definition = new() { UsesOriginalEnemySlots = true, AddedEnemySources = sources, Combatants = combatants };
        Dictionary<int, NativeBattleVector> positions = AddedBattleEnemyLayout.Place(definition, encounter, sources, id => resources[id], null);
        NativeBattleVector front = original.NativePosition!.Value;
        Assert.All(positions.Values, point => Assert.True(point.Z <= front.Z));
        Assert.Contains(positions.Values, point => point.X < front.X);
        Assert.Contains(positions.Values, point => point.X > front.X);
        Assert.Contains(positions.Values, point => point.Z < front.Z);

        positions.Add(original.Slot, front);
        for (int i = 0; i < combatants.Length; i++)
        {
            for (int j = i + 1; j < combatants.Length; j++)
            {
                var first = sizes[combatants[i].ModelId];
                var second = sizes[combatants[j].ModelId];
                NativeBattleVector a = positions[combatants[i].SceneSlot!.Value];
                NativeBattleVector b = positions[combatants[j].SceneSlot!.Value];
                int gap = first.Large || second.Large ? 600 : 300;
                Assert.True(Math.Abs(a.X - b.X) >= first.HalfWidth + second.HalfWidth + gap || Math.Abs(a.Z - b.Z) >= first.HalfDepth + second.HalfDepth + gap,
                    $"Models {combatants[i].ModelId} ({first}) and {combatants[j].ModelId} ({second}) at {a} and {b} lack {gap} units of clearance.");
            }
        }

        Dictionary<string, NativeBattleVector> retained = new() { ["enemy-8"] = new(front.X, 0, front.Z) };
        var restored = AddedBattleEnemyLayout.Place(definition, encounter, sources, id => resources[id], retained);
        Assert.Equal(retained["enemy-8"], restored[8]);
    }

    private static BattleCombatantDefinition Actor(int slot, int model)
    {
        return new() { Id = $"enemy-{slot}", SceneSlot = slot, ModelId = model };
    }
}
