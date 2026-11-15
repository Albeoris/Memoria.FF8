using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanModelSelectionTests
{
    private static readonly BattleModelOwner First = OriginalOwner(0);
    private static readonly BattleModelOwner Second = OriginalOwner(2);
    private static readonly BattleModelOwner Source = OriginalOwner(5);
    private static readonly BattleModelOwner Tail = OriginalOwner(6);

    [Fact]
    public void Minimum_ring_uses_original_model_order_but_null_tail_returns_original_source()
    {
        BattleActorModels models = Models();
        models = models.Set(Source, models.Get(Source).WriteLinkedModel(Second)).Set(Second, models.Get(Second).WriteLinkedModel(Tail)).Set(Tail, models.Get(Tail).WriteLinkedModel(Source));
        Assert.Equal(Second, NativeBattleScanModelSelection.Minimum501FF0(models, Source));
        models = models.Set(Tail, models.Get(Tail).WriteLinkedModel(null));
        Assert.Equal(Source, NativeBattleScanModelSelection.Minimum501FF0(models, Source));
        Assert.Equal(First, NativeBattleScanModelSelection.Minimum501FF0(models, First));
    }

    [Fact]
    public void Ordinary_selection_replaces_all_flags_from_actual_ring_and_requires_only_reached_fields()
    {
        BattleActorModels models = Models().Set(Source, new NativeBattleGraphicsModel(2) { ModelId4 = 0 }.WriteLinkedModel(Second))
            .Set(Second, new NativeBattleGraphicsModel(4) { ModelId4 = 0 }.WriteLinkedModel(Tail))
            .Set(Tail, new NativeBattleGraphicsModel(2) { ModelId4 = 0 }.WriteLinkedModel(Source));
        NativeBattleScanByteOwner before = NativeBattleScanPhysicalModels.Capture(models.Get(Source));
        NativeBattleScanModelSelectionResult selection = NativeBattleScanModelSelection.Select84D320(models, Source, _ => throw new InvalidDataException());
        Assert.Equal(Second, selection.Primary);
        Assert.Equal(BattleModelTargets.From([Source, Tail]), selection.Targets);
        Assert.Equal(before.Bytes.ToArray(), NativeBattleScanPhysicalModels.Capture(models.Get(Source)).Bytes.ToArray());
        Assert.False(before.Known[0x38]);
        Assert.False(before.Known[0x7C]);

        BattleActorModels restored = NativeBattleTestSnapshots.Independent(models);
        Assert.Equal(selection, NativeBattleScanModelSelection.Select84D320(restored, Source, _ => throw new InvalidDataException()));
    }

    [Fact]
    public void Special_category_54_selects_54_60_61_in_original_model_order_without_ring_flag_filter()
    {
        BattleActorModels models = Models();
        BattleModelOwner fourth = OriginalOwner(4);
        models = models.Set(First, models.Get(First) with { ModelId4 = 0x60 })
            .Set(Second, models.Get(Second) with { ModelId4 = 0x54 })
            .Set(fourth, models.Get(fourth) with { ModelId4 = 0x61 })
            .Set(Tail, models.Get(Tail) with { ModelId4 = 0x54 });
        NativeBattleScanModelSelectionResult result = NativeBattleScanModelSelection.Select84D320(models, Second, _ => throw new InvalidDataException());
        Assert.Equal(BattleModelTargets.From([First, Second, fourth, Tail]), result.Targets);
        Assert.Equal(Second, result.Primary);
    }

    [Theory]
    [InlineData(0x24, 0xFFFFFFFFu, 0xA0)]
    [InlineData(0x25, 0xFFFFFFFFu, 0xA0)]
    [InlineData(0x24, 0xFFFFFFFEu, 0x24)]
    [InlineData(0x25, 0u, 0x25)]
    public void Category_24_and_25_use_exact_signed_minus_one_field(byte category, uint field, byte expected)
    {
        BattleModelOwner owner = OriginalOwner(3);
        BattleActorModels models = Models().Set(owner, new(0) { ModelId4 = category, SequenceState = new() { MeshMask124 = field } });
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(models, owner, _ => throw new InvalidDataException()));
    }

    [Theory]
    [InlineData(0x2B, 0, 0xA1)]
    [InlineData(0x2B, 1, 0x2B)]
    [InlineData(0x77, 2, 0xA2)]
    [InlineData(0x77, 1, 0x77)]
    [InlineData(0x4B, 255, 0xA5)]
    [InlineData(0x4B, 1, 0x4B)]
    public void Pointer_categories_read_the_selected_model_resource_sequence(byte category, byte value, byte expected)
    {
        BattleModelOwner owner = OriginalOwner(3);
        BattleActorModels models = Models().Set(owner, new(0) { ModelId4 = category });
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(models, owner, reached =>
        {
            Assert.Equal(owner, reached);
            return value;
        }));
    }

    [Theory]
    [InlineData(0x1800, 0xA3)]
    [InlineData(0x2000, 0xA4)]
    [InlineData(0x1801, 0x3B)]
    public void Category_3B_reads_actual_terminal_word(ushort value, byte expected)
    {
        BattleModelOwner owner = OriginalOwner(3);
        BattleActorModels models = Models().Set(owner, new(0) { ModelId4 = 0x3B, Transform = new(default, default, 0, 0, 0) { Scale154 = unchecked((short)value) } });
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(models, owner, _ => throw new InvalidDataException()));
    }

    [Fact]
    public void Missing_model_link_or_reached_unknown_field_fails_instead_of_zero_replacement()
    {
        BattleActorModels models = Models().Set(First, new(0));
        Assert.Throws<NotSupportedException>(() => NativeBattleScanModelSelection.Minimum501FF0(models, First));
        models = models.Set(First, new NativeBattleGraphicsModel(0).WriteLinkedModel(new BattlePartyActorModel(new("retired"))));
        Assert.Throws<KeyNotFoundException>(() => NativeBattleScanModelSelection.Minimum501FF0(models, First));
        models = models.Set(First, new(0) { ModelId4 = 0x24 });
        Assert.Throws<NotSupportedException>(() => NativeBattleScanModelSelection.Category84D410(models, First, _ => 0));
    }

    private static BattleActorModels Models() => new(OriginalRoster.Owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new(0) { ModelId4 = 0, HasLinkedModelRing140 = false })));
}

internal static class ScanPhysicalModelFixture
{
    public static ImmutableArray<NativeBattleScanByteOwner> Models() => Enumerable.Range(0, 7).Select(_ => new NativeBattleScanByteOwner(new byte[156].ToImmutableArray(), Enumerable.Repeat(false, 156).ToImmutableArray()).WriteByte(0, 0).WriteByte(4, 0).WriteDword(0x8C, 0)).ToImmutableArray();
}
