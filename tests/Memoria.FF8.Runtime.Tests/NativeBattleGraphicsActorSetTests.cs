using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsActorSetTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData((1u << 24) | (1u << 7) | 1u)]
    public void JsonRoundTripContainsOnlyTheAuthoritativeBits(uint bits)
    {
        NativeBattleGraphicsActorSet set = new(bits);
        string json = JsonSerializer.Serialize(set);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(nameof(NativeBattleGraphicsActorSet.Bits), Assert.Single(document.RootElement.EnumerateObject()).Name);
        Assert.Equal(set, JsonSerializer.Deserialize<NativeBattleGraphicsActorSet>(json));
    }

    [Fact]
    public void JsonRejectsBitsBeyondTheGraphicsBank()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<NativeBattleGraphicsActorSet>("{\"Bits\":33554432}"));
    }

    [Fact]
    public void EveryRowHasItsOwnBitAndEnumeratesInAscendingOrder()
    {
        NativeBattleGraphicsActorSet set = default;
        Assert.True(set.IsEmpty);
        Assert.Equal(0, set.Count);
        Assert.Empty(set.EnumerateRows());
        Assert.Throws<InvalidOperationException>(() => (object)set.HighestRow);

        for (int row = 24; row >= 0; row--)
        {
            NativeBattleGraphicsActorSet single = default(NativeBattleGraphicsActorSet).Add(row);
            Assert.Equal(1u << row, single.Bits);
            Assert.True(single.IsSingle(row));
            Assert.False(single.IsSingle((row + 1) % 25));
            Assert.Equal(row, single.HighestRow);
            Assert.Equal(single, single.Add(row));
            Assert.True(single.Remove(row).IsEmpty);
            set = set.Add(row);
        }
        Assert.False(set.IsEmpty);
        Assert.Equal(25, set.Count);
        Assert.Equal(24, set.HighestRow);
        Assert.Equal(0x1FFFFFFu, set.Bits);
        Assert.Equal(Enumerable.Range(0, 25), set.EnumerateRows());
        Assert.False(set.IsSingle(24));
        Assert.Equal(Enumerable.Range(0, 25).Where(row => row != 10), set.Remove(10).EnumerateRows());
    }

    [Fact]
    public void NativeProjectionKeepsExactlyTheLowSevenBits()
    {
        for (ushort native = 0; native <= 0x7F; native++)
        {
            NativeBattleGraphicsActorSet set = NativeBattleGraphicsActorSet.FromNative(native);
            Assert.Equal((uint)native, set.Bits);
            Assert.Equal(native, set.NativePhysicalBits);
            Assert.Equal(native, set.Add(7).Add(24).NativePhysicalBits);
            set.ValidateCapacity(7);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsActorSet.FromNative(0x80));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsActorSet.FromNative(0x8000));
    }

    [Theory]
    [InlineData(-32)]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(32)]
    [InlineData(56)]
    [InlineData(int.MaxValue)]
    public void InvalidRowsCannotAliasSelectedBits(int row)
    {
        NativeBattleGraphicsActorSet set = new(1u | (1u << 24));
        Assert.False(set.Contains(row));
        Assert.False(set.IsSingle(row));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(row));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Remove(row));
    }

    [Fact]
    public void CapacityAndConstructorRejectUnownedBits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeBattleGraphicsActorSet(1u << 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NativeBattleGraphicsActorSet(uint.MaxValue));
        NativeBattleGraphicsActorSet set = new(1u << 24);
        set.ValidateCapacity(25);
        Assert.Throws<InvalidDataException>(() => set.ValidateCapacity(24));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.ValidateCapacity(26));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.ValidateCapacity(0));
        default(NativeBattleGraphicsActorSet).ValidateCapacity(1);
    }
}
