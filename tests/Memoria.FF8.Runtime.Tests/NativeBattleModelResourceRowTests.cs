namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceRowTests
{
    [Theory]
    [InlineData(0, 0x1D99774u)]
    [InlineData(3, 0x1D99810u)]
    [InlineData(10, 0x1D9997Cu)]
    public void Actual_CPU_row_selects_its_physical_resource_record_without_mutating_model(int row, uint expected)
    {
        NativeBattleGraphicsModel model = new(6, 0) { ActiveResourceRow100 = row };
        Assert.Equal(expected, NativeBattleModelResourceRow.Address100(model));
        Assert.Equal(row, model.ActiveResourceRow100);
        Assert.Null(model.ActiveStagedResourceRow100);
    }

    [Fact]
    public void Current_staged_override_wins_without_fabricating_a_CPU_slot()
    {
        NativeBattleGraphicsModel model = new(0, 0) { ActiveResourceRow100 = 4, ActiveStagedResourceRow100 = 0x20E1000 };
        Assert.Equal(0x20E1000u, NativeBattleModelResourceRow.Address100(model));
        Assert.Equal(4, model.ActiveResourceRow100);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Unobserved_or_out_of_range_rows_fail_fast(int row)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceRow.Address100(new(0, 0) { ActiveResourceRow100 = row }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelResourceRow.Address100(new(0, 0)));
    }
}
