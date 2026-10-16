using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeSolomonRingTests
{
    [Fact]
    public void OriginalRingChecksAllMaterialsBeforeAcquisitionAndConsumesExactlyOnce()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeSolomonRingDefinition definition = assets.ReadNativeSolomonRingDefinition();
        Assert.Equal(167, definition.ItemId);
        Assert.Equal(new NativeItemStock[] { new(66, 6), new(136, 6), new(17, 6) }, definition.Ingredients);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(definition.ItemId, 2, out _);
        foreach (NativeItemStock material in definition.Ingredients)
        {
            economy = economy.AddItem(material.ItemId, material.Count - 1, out _);
            var refused = definition.Use(vitals, economy, definition.ItemId);
            Assert.False(refused.Changed);
            Assert.Same(economy, refused.Economy);
            Assert.Same(vitals, refused.Vitals);
            economy = economy.AddItem(material.ItemId, 1, out _);
        }
        string before = JsonSerializer.Serialize(new { vitals, economy });
        var result = definition.Use(vitals, economy, definition.ItemId);
        Assert.True(result.Changed);
        Assert.Equal(1, result.Vitals.GuardianForces[11].Flags & 1);
        Assert.All(definition.Ingredients, material => Assert.DoesNotContain(result.Economy.Items, stock => stock.ItemId == material.ItemId));
        Assert.Equal(1, result.Economy.Items.Single(stock => stock.ItemId == definition.ItemId).Count);
        Assert.Equal(before, JsonSerializer.Serialize(new { vitals, economy }));
        var again = definition.Use(result.Vitals, result.Economy, definition.ItemId);
        Assert.False(again.Changed);
        Assert.Same(result.Economy, again.Economy);
        Assert.Equal(economy.Gil, result.Economy.Gil);
    }
}
