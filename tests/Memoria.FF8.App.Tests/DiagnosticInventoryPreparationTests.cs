using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticInventoryPreparationTests
{
    [Fact]
    public void Initial_inventory_acquisition_uses_exact_native_counts_and_preserves_the_source()
    {
        NativeEconomy source = Economy();
        source.Items[0] = new(1, 10);
        JsonElement before = JsonSerializer.SerializeToElement(source);
        DiagnosticBattlePreparation fixture = new([], [], false) { Items = [new(1, 2), new(32, 100)] };
        DiagnosticInventoryPreparationResult result = GameWorld.PrepareDiagnosticInventory(source, fixture);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(source)));
        Assert.Equal(2, result.Economy.Items.Single(item => item.ItemId == 1).Count);
        Assert.Equal(100, result.Economy.Items.Single(item => item.ItemId == 32).Count);
        DiagnosticPreparationChange change = Assert.Single(result.Changes);
        Assert.Equal("economy", change.Owner);
        Assert.True(JsonElement.DeepEquals(before, change.Before));
        JsonElement frozen = change.After;
        result.Economy.Items[0] = new(0, 0);
        Assert.True(JsonElement.DeepEquals(frozen, change.After));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(199, 2)]
    [InlineData(1, 1)]
    [InlineData(1, 101)]
    public void Invalid_inventory_identity_or_two_execution_quantity_is_rejected(int id, int count)
    {
        DiagnosticBattlePreparation fixture = new([], [], false) { Items = [new(id, count)] };
        Assert.Throws<InvalidDataException>(fixture.Validate);
    }

    [Fact]
    public void Ambiguous_stock_and_capacity_failure_are_explicit_without_source_mutation()
    {
        NativeEconomy source = Economy();
        source.Items[0] = new(1, 2);
        source.Items[1] = new(1, 2);
        DiagnosticBattlePreparation fixture = new([], [], false) { Items = [new(1, 4)] };
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticInventory(source, fixture));
        Assert.Equal(2, source.Items[0].Count);
        for (int index = 0; index < source.Items.Length; index++)
        {
            source.Items[index] = new(1, 2);
        }
        Assert.Throws<InvalidOperationException>(() => GameWorld.PrepareDiagnosticInventory(source, fixture with { Items = [new(2, 2)] }));
    }

    private static NativeEconomy Economy()
    {
        return new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), ItemOrder = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
    }
}
