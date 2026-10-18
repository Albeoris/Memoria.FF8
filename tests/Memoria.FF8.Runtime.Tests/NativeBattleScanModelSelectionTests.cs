using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanModelSelectionTests
{
    [Fact]
    public void Minimum_ring_uses_physical_pointer_order_but_null_tail_returns_original_source()
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(5, models[5].WriteDword(0x8C, Address(2))).SetItem(2, models[2].WriteDword(0x8C, Address(6))).SetItem(6, models[6].WriteDword(0x8C, Address(5)));
        Assert.Equal(Address(2), NativeBattleScanModelSelection.Minimum501FF0(models, Address(5)));
        models = models.SetItem(6, models[6].WriteDword(0x8C, 0));
        Assert.Equal(Address(5), NativeBattleScanModelSelection.Minimum501FF0(models, Address(5)));
        Assert.Equal(Address(0), NativeBattleScanModelSelection.Minimum501FF0(models, Address(0)));
    }

    [Fact]
    public void Ordinary_selection_replaces_all_flags_from_actual_ring_and_requires_only_reached_bytes()
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(5, models[5].WriteByte(0, 2).WriteDword(0x8C, Address(2))).SetItem(2, models[2].WriteByte(0, 4).WriteDword(0x8C, Address(6))).SetItem(6, models[6].WriteByte(0, 2).WriteDword(0x8C, Address(5)));
        string before = JsonSerializer.Serialize(models);
        NativeBattleScanModelSelectionResult selection = NativeBattleScanModelSelection.Select84D320(models, 5, _ => throw new InvalidDataException());
        Assert.Equal(Address(2), selection.SelectedPhysicalModel);
        Assert.Equal(new uint[] { 0, 0, 0, 0, 0, 1, 1 }, selection.SelectionFlags);
        Assert.Equal(before, JsonSerializer.Serialize(models));
        Assert.False(models[5].Known[0x38]);
        Assert.False(models[5].Known[1]);
        ImmutableArray<NativeBattleScanByteOwner> restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleScanByteOwner>>(before);
        Assert.Equal(JsonSerializer.Serialize(selection), JsonSerializer.Serialize(NativeBattleScanModelSelection.Select84D320(restored, 5, _ => throw new InvalidDataException())));
    }

    [Fact]
    public void Special_category_54_selects_54_60_61_in_physical_order_without_ring_flag_filter()
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(0, models[0].WriteByte(4, 0x60)).SetItem(2, models[2].WriteByte(4, 0x54)).SetItem(4, models[4].WriteByte(4, 0x61)).SetItem(6, models[6].WriteByte(4, 0x54));
        NativeBattleScanModelSelectionResult result = NativeBattleScanModelSelection.Select84D320(models, 2, _ => throw new InvalidDataException());
        Assert.Equal(new uint[] { 1, 0, 1, 0, 1, 0, 1 }, result.SelectionFlags);
        Assert.Equal(Address(2), result.SelectedPhysicalModel);
    }

    [Theory]
    [InlineData(0x24, 0xFFFFFFFFu, 0xA0)]
    [InlineData(0x25, 0xFFFFFFFFu, 0xA0)]
    [InlineData(0x24, 0xFFFFFFFEu, 0x24)]
    [InlineData(0x25, 0u, 0x25)]
    public void Category_24_and_25_use_exact_signed_minus_one_field(byte category, uint field, byte expected)
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(3, models[3].WriteByte(4, category).WriteDword(0x7C, field));
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(models, 3, _ => throw new InvalidDataException()));
    }

    [Theory]
    [InlineData(0x2B, 0, 0xA1)]
    [InlineData(0x2B, 1, 0x2B)]
    [InlineData(0x77, 2, 0xA2)]
    [InlineData(0x77, 1, 0x77)]
    [InlineData(0x4B, 255, 0xA5)]
    [InlineData(0x4B, 1, 0x4B)]
    public void Category_pointer_reads_actual_byte_at_74_pointee(byte category, byte value, byte expected)
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(3, models[3].WriteByte(4, category).WriteDword(0x74, 0x12345678));
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(models, 3, pointer =>
        {
            Assert.Equal(0x12345678u, pointer);
            return value;
        }));
    }

    [Theory]
    [InlineData(0x1800, 0xA3)]
    [InlineData(0x2000, 0xA4)]
    [InlineData(0x1801, 0x3B)]
    public void Category_3B_reads_actual_terminal_word(ushort value, byte expected)
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(3, models[3].WriteByte(4, 0x3B).WriteWord(0x9A, value));
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(models, 3, _ => throw new InvalidDataException()));
    }

    [Fact]
    public void Missing_physical_link_or_reached_unknown_byte_fails_instead_of_zero_replacement()
    {
        ImmutableArray<NativeBattleScanByteOwner> models = ScanPhysicalModelFixture.Models();
        models = models.SetItem(0, models[0].WriteDword(0x8C, Address(0) + 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleScanModelSelection.Minimum501FF0(models, Address(0)));
        models = models.SetItem(0, models[0].WriteByte(4, 0x24));
        Assert.Throws<NotSupportedException>(() => NativeBattleScanModelSelection.Category84D410(models, 0, _ => 0));
    }

    private static uint Address(int index) => NativeBattleScanModelSelection.ModelBase + (uint)(index * NativeBattleScanModelSelection.ModelStride);
}

internal static class ScanPhysicalModelFixture
{
    public static ImmutableArray<NativeBattleScanByteOwner> Models() => Enumerable.Range(0, 7).Select(_ => new NativeBattleScanByteOwner(new byte[156].ToImmutableArray(), Enumerable.Repeat(false, 156).ToImmutableArray()).WriteByte(0, 0).WriteByte(4, 0).WriteDword(0x8C, 0)).ToImmutableArray();
}
