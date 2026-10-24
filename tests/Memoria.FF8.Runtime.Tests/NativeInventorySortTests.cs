using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeInventorySortTests
{
    [Fact]
    public void SortUsesIdsCompactsHolesAndKeepsLastOccupiedDuplicateCount()
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        NativeItemStock[] prefix = [new(190, 1), new(7, 2), new(0, 9), new(1, 6), new(7, 4), new(33, 0), new(7, 0), new(198, 3), new(199, 5)];
        prefix.CopyTo(items, 0);
        NativeEconomy economy = new()
        {
            Version = 1,
            Rule = NativeEconomy.InitializationRule,
            InitOutSha256 = new string('A', 64),
            Items = items,
            ItemOrder = Enumerable.Range(0, 32).Reverse().Select(value => (byte)value).ToArray(),
            Gil = 123,
            PastGil = 456,
            EventBankGil72 = 789,
            EventBankPastGil76 = 987
        };
        string original = JsonSerializer.Serialize(economy);

        NativeEconomy sorted = economy.SortItems();

        Assert.Equal(new NativeItemStock[] { new(1, 6), new(7, 4), new(190, 1), new(198, 3) }, sorted.Items.Take(4));
        Assert.All(sorted.Items.Skip(4), item => Assert.Equal(new NativeItemStock(0, 0), item));
        Assert.Equal(original, JsonSerializer.Serialize(economy));
        Assert.Equal(economy.ItemOrder, sorted.ItemOrder);
        Assert.Equal(economy with { Items = sorted.Items, ItemOrder = sorted.ItemOrder }, sorted);
        NativeEconomy restored = JsonSerializer.Deserialize<NativeEconomy>(JsonSerializer.Serialize(sorted))!;
        restored.Validate();
        Assert.Equal(sorted.Items, restored.Items);
        Assert.Equal(JsonSerializer.Serialize(sorted), JsonSerializer.Serialize(restored.SortItems()));
        NativeEconomy unsupported = economy.DeepCopy();
        unsupported.Items[0] = new NativeItemStock(202, 1);
        Assert.Throws<NotSupportedException>(() => unsupported.SortItems());
    }
}
