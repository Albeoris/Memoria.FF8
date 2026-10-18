using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanCategoryOwnerTests
{
    [Theory]
    [InlineData(0x2B, 1, 0x2B)]
    [InlineData(0x2B, 0, 0xA1)]
    [InlineData(0x77, 1, 0x77)]
    [InlineData(0x77, 2, 0xA2)]
    [InlineData(0x4B, 1, 0x4B)]
    [InlineData(0x4B, 255, 0xA5)]
    public void Resource_sequence_belongs_to_original_actor_even_when_ring_minimum_is_another_model(byte category, byte sequence, byte expected)
    {
        ImmutableArray<NativeBattleGraphicsModel> canonical = Models();
        canonical = canonical.SetItem(0, canonical[0].WriteLinkedModelIndex140(3) with { Flags0 = 2, ModelId4 = category, Sequence = sequence == 1 ? (byte)0 : (byte)1 });
        canonical = canonical.SetItem(3, canonical[3].WriteLinkedModelIndex140(0) with { Flags0 = 2, ModelId4 = category, Sequence = sequence });
        ImmutableArray<NativeBattleScanByteOwner> physical = NativeBattleScanPhysicalModels.Capture(canonical);
        Assert.Throws<NotSupportedException>(() => physical[3].ReadDword(0x74));
        string before = JsonSerializer.Serialize(physical);
        List<int> queried = [];
        NativeBattleScanModelSelectionResult result = NativeBattleScanModelSelection.Select84D320(physical, 3, _ => throw new InvalidOperationException("The canonical resource sequence does not need a fabricated pointer."), actor =>
        {
            queried.Add(actor);
            return canonical[actor].Sequence ?? throw new InvalidOperationException();
        });
        Assert.Equal(NativeBattleScanModelSelection.ModelBase, result.SelectedPhysicalModel);
        Assert.Equal(expected, result.Category);
        Assert.Equal(new int[] { 3 }, queried);
        Assert.Equal(new uint[] { 1, 0, 0, 1, 0, 0, 0 }, result.SelectionFlags);
        Assert.Equal(before, JsonSerializer.Serialize(physical));
        Assert.Equal(sequence, canonical[3].Sequence);
    }

    [Theory]
    [InlineData(0x24, uint.MaxValue, 0xA0)]
    [InlineData(0x25, uint.MaxValue, 0xA0)]
    [InlineData(0x24, 0xFFFFFFFEu, 0x24)]
    [InlineData(0x25, 0u, 0x25)]
    public void Physical_mesh_mask_is_captured_from_current_sequence_state_for_categories_24_and_25(byte category, uint mask, byte expected)
    {
        ImmutableArray<NativeBattleGraphicsModel> canonical = Models();
        canonical = canonical.SetItem(3, canonical[3] with { ModelId4 = category, SequenceState = new() { MeshMask124 = mask } });
        ImmutableArray<NativeBattleScanByteOwner> physical = NativeBattleScanPhysicalModels.Capture(canonical);
        Assert.Equal(mask, physical[3].ReadDword(0x7C));
        Assert.Throws<NotSupportedException>(() => physical[3].ReadDword(0x74));
        Assert.Equal(expected, NativeBattleScanModelSelection.Category84D410(physical, 3, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException("This category uses its mesh mask, not resource sequence.")));
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
        ImmutableArray<NativeBattleGraphicsModel> canonical = Models();
        canonical = canonical.SetItem(3, canonical[3] with
        {
            ModelId4 = category,
            Sequence = null,
            SequenceState = new() { MeshMask124 = 0 },
            Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096)
        });
        ImmutableArray<NativeBattleScanByteOwner> physical = NativeBattleScanPhysicalModels.Capture(canonical);
        Assert.Equal(category, NativeBattleScanModelSelection.Category84D410(physical, 3, _ => throw new InvalidOperationException("This category must not read a resource pointer."), _ => throw new InvalidOperationException("This category must not query resource sequence.")));
    }

    private static ImmutableArray<NativeBattleGraphicsModel> Models()
    {
        return Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0) { ModelId4 = 0 }.WriteLinkedModelIndex140(null)).ToImmutableArray();
    }
}
