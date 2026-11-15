using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanCategoryOwnerTests
{
    private static readonly BattleModelOwner Minimum = OriginalOwner(0);
    private static readonly BattleModelOwner Source = OriginalOwner(3);

    [Theory]
    [InlineData(0x2B, 1, 0x2B)]
    [InlineData(0x2B, 0, 0xA1)]
    [InlineData(0x77, 1, 0x77)]
    [InlineData(0x77, 2, 0xA2)]
    [InlineData(0x4B, 1, 0x4B)]
    [InlineData(0x4B, 255, 0xA5)]
    public void Resource_sequence_belongs_to_original_actor_even_when_ring_minimum_is_another_model(byte category, byte sequence, byte expected)
    {
        BattleActorModels canonical = Models();
        canonical = canonical.Set(Minimum, canonical.Get(Minimum).WriteLinkedModel(Source) with { Flags0 = 2, ModelId4 = category, Sequence = sequence == 1 ? (byte)0 : (byte)1 });
        canonical = canonical.Set(Source, canonical.Get(Source).WriteLinkedModel(Minimum) with { Flags0 = 2, ModelId4 = category, Sequence = sequence });
        NativeBattleScanByteOwner physical = NativeBattleScanPhysicalModels.Capture(canonical.Get(Source));
        Assert.Throws<NotSupportedException>(() => physical.ReadDword(0x74));
        byte[] before = physical.Bytes.ToArray();
        List<BattleModelOwner> queried = [];
        NativeBattleScanModelSelectionResult result = NativeBattleScanModelSelection.Select84D320(canonical, Source, actor =>
        {
            queried.Add(actor);
            return canonical.Get(actor).Sequence ?? throw new InvalidOperationException();
        });
        Assert.Equal(Minimum, result.Primary);
        Assert.Equal(expected, result.Category);
        Assert.Equal(new[] { Source }, queried);
        Assert.Equal(BattleModelTargets.From([Minimum, Source]), result.Targets);
        Assert.Equal(before, NativeBattleScanPhysicalModels.Capture(canonical.Get(Source)).Bytes.ToArray());
        Assert.Equal(sequence, canonical.Get(Source).Sequence);
    }

    [Theory]
    [InlineData(0x24, uint.MaxValue, 0xA0)]
    [InlineData(0x25, uint.MaxValue, 0xA0)]
    [InlineData(0x24, 0xFFFFFFFEu, 0x24)]
    [InlineData(0x25, 0u, 0x25)]
    public void Physical_mesh_mask_is_captured_from_current_sequence_state_for_categories_24_and_25(byte category, uint mask, byte expected)
    {
        BattleActorModels canonical = Models();
        canonical = canonical.Set(Source, canonical.Get(Source) with { ModelId4 = category, SequenceState = new() { MeshMask124 = mask } });
        NativeBattleScanByteOwner physical = NativeBattleScanPhysicalModels.Capture(canonical.Get(Source));
        Assert.Equal(mask, physical.ReadDword(0x7C));
        Assert.Throws<NotSupportedException>(() => physical.ReadDword(0x74));
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(canonical, Source, _ => throw new InvalidOperationException("This category uses its mesh mask.")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x24)]
    [InlineData(0x25)]
    [InlineData(0x3B)]
    [InlineData(0x4D)]
    [InlineData(0x54)]
    [InlineData(0x60)]
    [InlineData(0x61)]
    [InlineData(0xA5)]
    public void Other_categories_do_not_query_an_unconsumed_missing_resource_sequence(byte category)
    {
        BattleActorModels canonical = Models();
        canonical = canonical.Set(Source, canonical.Get(Source) with
        {
            ModelId4 = category,
            Sequence = null,
            SequenceState = new() { MeshMask124 = 0 },
            Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096)
        });
        Assert.Equal(category, NativeBattleScanModelSelection.Category84D410(canonical, Source, _ => throw new InvalidOperationException("This category must not query resource sequence.")));
    }

    private static BattleActorModels Models() => new(OriginalRoster.Owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new NativeBattleGraphicsModel(0) { ModelId4 = 0 }.WriteLinkedModel(null))));
}
