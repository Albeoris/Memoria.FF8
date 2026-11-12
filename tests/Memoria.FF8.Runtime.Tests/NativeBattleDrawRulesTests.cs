using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrawRulesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cast_bypasses_full_stock_without_consuming_or_granting_magic(bool shared)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 21, 100);
        vitals = MagicSystemFixture.Modes(vitals, shared: shared);
        string before = JsonSerializer.Serialize(vitals);
        NativeCharacterVitals after = NativeBattleDrawRules.Resolve(vitals, 0, 21, 9, true, out MagicStockMutation? receipt);
        Assert.Same(vitals, after);
        Assert.Null(receipt);
        Assert.Equal(before, JsonSerializer.Serialize(after));
        Assert.Equal(100, after.GetMagicCount(0, 21));
    }

    [Fact]
    public void Cast_bypasses_full_personal_type_bank_and_does_not_acquire_the_spell()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        for (int spell = 1; spell <= 32; spell++)
        {
            vitals = MagicSystemFixture.Stock(vitals, 0, spell, 1);
        }
        Assert.Equal(2, vitals.GetMagicCapacity(0, 40));
        Assert.Same(vitals, NativeBattleDrawRules.Resolve(vitals, 0, 40, 4, true, out _));
        Assert.Equal(0, vitals.GetMagicCount(0, 40));
    }

    [Fact]
    public void Stock_accepts_only_remaining_capacity_and_survives_JSON()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 0, 21, 98);
        NativeCharacterVitals after = NativeBattleDrawRules.Resolve(vitals, 0, 21, 9, false, out MagicStockMutation? receipt);
        Assert.Equal(2, receipt!.Accepted);
        Assert.Equal(98, vitals.GetMagicCount(0, 21));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(after))!;
        Assert.Equal(100, restored.GetMagicCount(0, 21));
        Assert.Same(restored, NativeBattleDrawRules.Resolve(restored, 0, 21, 9, false, out MagicStockMutation? full));
        Assert.Equal(0, full!.Accepted);
    }

    [Fact]
    public void Spell_and_slot_resistances_remain_separate_and_failure_is_zero()
    {
        Assert.Equal(9, NativeBattleDrawRules.Quantity(50, 50, 100, 0, 0, 0));
        Assert.Equal(0, NativeBattleDrawRules.Quantity(1, 100, 1, 44, 0, 0));
        Assert.Equal(0, NativeBattleDrawRules.Quantity(50, 50, 10, 0, 9, 0));
        Assert.Equal(1, NativeBattleDrawRules.Quantity(50, 50, 10, 10, 0, 0));
    }
}
