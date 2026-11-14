using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedResourceRowAddressTests
{
    [Fact]
    public void OriginalAliasesKeepAllElevenAddresses()
    {
        for (int row = 0; row < 11; row++)
        {
            uint expected = 0x1D99774u + (uint)(52 * row);
            Assert.Equal(expected, NativeBattleModelResourceRowAddress.Encode(row));
            Assert.True(NativeBattleModelResourceRowAddress.TryDecode(expected, out int decoded));
            Assert.Equal(row, decoded);
        }
    }

    [Fact]
    public void ExtensionHandlesRequireTheirActualCapacity()
    {
        Assert.Equal(51, NativeBattleModelResourceRowAddress.MaximumRowCount);
        for (int row = 11; row <= 50; row++)
        {
            uint expected = 0xF3000000u + (uint)(52 * (row - 11));
            Assert.Equal(expected, NativeBattleModelResourceRowAddress.Encode(row, row + 1));
            Assert.True(NativeBattleModelResourceRowAddress.TryDecode(expected, out int decoded, row + 1));
            Assert.Equal(row, decoded);
            Assert.False(NativeBattleModelResourceRowAddress.TryDecode(expected, out decoded, row));
            Assert.Equal(-1, decoded);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelResourceRowAddress.Encode(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelResourceRowAddress.Encode(51, 51));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelResourceRowAddress.Encode(-1, 51));
    }

    [Theory]
    [InlineData(0x1D99773u)]
    [InlineData(0x1D999B0u)]
    [InlineData(0x1D999E4u)]
    [InlineData(0x1D99A78u)]
    [InlineData(0x1D972C0u)]
    [InlineData(0x1D28344u)]
    [InlineData(0x80000000u)]
    [InlineData(0xF1000000u)]
    [InlineData(0xF2000000u)]
    [InlineData(0xF3000820u)]
    [InlineData(uint.MaxValue)]
    public void AdjacentAndUnrelatedAddressesNeverResolve(uint address)
    {
        Assert.False(NativeBattleModelResourceRowAddress.TryDecode(address, out int row, 51));
        Assert.Equal(-1, row);
        Assert.False(NativeBattleModelResourceRowAddress.TryResolveField(address, 0, out row, out int offset, 51));
        Assert.Equal(-1, row);
        Assert.Equal(-1, offset);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(50)]
    public void FieldsMustRemainWithinOneOwnedRow(int resourceRow)
    {
        uint start = NativeBattleModelResourceRowAddress.Encode(resourceRow, 51);
        Assert.True(NativeBattleModelResourceRowAddress.TryResolveField(start, 52, out int row, out int offset, 51));
        Assert.Equal(resourceRow, row);
        Assert.Equal(0, offset);
        Assert.True(NativeBattleModelResourceRowAddress.TryResolveField(start + 51, 1, out row, out offset, 51));
        Assert.Equal(resourceRow, row);
        Assert.Equal(51, offset);
        Assert.True(NativeBattleModelResourceRowAddress.TryResolveField(start + 51, 0, out _, out _, 51));
        Assert.False(NativeBattleModelResourceRowAddress.TryDecode(start + 1, out _, 51));
        Assert.False(NativeBattleModelResourceRowAddress.TryResolveField(start + 51, 2, out row, out offset, 51));
        Assert.Equal(-1, row);
        Assert.Equal(-1, offset);
        Assert.False(NativeBattleModelResourceRowAddress.TryResolveField(start, -1, out _, out _, 51));
        Assert.False(NativeBattleModelResourceRowAddress.TryResolveField(start, int.MaxValue, out _, out _, 51));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(52)]
    public void InvalidCapacitiesAreRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelResourceRowAddress.Encode(0, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelResourceRowAddress.TryDecode(0, out _, capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelResourceRowAddress.TryResolveField(0, 0, out _, out _, capacity));
    }

    [Fact]
    public void SparseRosterCapacityCountsBoundSidesAndOneSpare()
    {
        ImmutableArray<NativeBattleGraphicsActor?> rows = new NativeBattleGraphicsActor?[25].ToImmutableArray().SetItem(24, new("enemy", false));
        Assert.Equal(11, NativeBattleModelResourceRowAddress.CapacityForRoster(new(rows)));
        foreach (int row in new[] { 0, 3, 7, 11, 16, 20 })
        {
            rows = rows.SetItem(row, new($"ally-{row}", true));
        }
        Assert.Equal(14, NativeBattleModelResourceRowAddress.CapacityForRoster(new(rows)));
        ImmutableArray<NativeBattleGraphicsActor?> full = Enumerable.Range(0, 25).Select(row => (NativeBattleGraphicsActor?)new NativeBattleGraphicsActor($"actor-{row}", row < 11)).ToImmutableArray();
        Assert.Equal(37, NativeBattleModelResourceRowAddress.CapacityForRoster(new(full)));
        ImmutableArray<NativeBattleGraphicsActor?> allParty = full.Select(row => (NativeBattleGraphicsActor?)(row! with { IsParty = true })).ToImmutableArray();
        Assert.Equal(51, NativeBattleModelResourceRowAddress.CapacityForRoster(new(allParty)));
    }

    [Fact]
    public void BodyAddressUsesExplicitCapacityAndPrefersTheStagedAlias()
    {
        NativeBattleGraphicsModel model = new(24, 0) { ActiveResourceRow100 = 11 };
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceRow.Address100(model));
        Assert.Equal(0xF3000000u, NativeBattleModelResourceRow.Address100(model, 51));
        Assert.Equal(0xF30007ECu, NativeBattleModelResourceRow.Address100(model with { ActiveResourceRow100 = 50 }, 51));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceRow.Address100(model with { ActiveResourceRow100 = 51 }, 51));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceRow.Address100(model with { ActiveResourceRow100 = null }, 51));
        Assert.Equal(0x20E1000u, NativeBattleModelResourceRow.Address100(model with { ActiveStagedResourceRow100 = 0x20E1000 }));
        Assert.Equal(0x20E1000u, NativeBattleModelResourceRow.Address100(model with { ActiveResourceRow100 = null, ActiveStagedResourceRow100 = 0x20E1000 }, 51));
    }
}
