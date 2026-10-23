using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90InitializerMetadataTests
{
    [Fact]
    public void Original_ordered_metadata_survives_json_and_loads_each_TIM_once()
    {
        NativeBattleOriginalEffectState selected = Effect();
        selected = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(selected))!;
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess().BeginLoad(0).LoadIntoArena(new byte[] { 255 }), selected);

        Assert.Equal(6, initialized.Memory.Cursor);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, initialized.Memory.Arena!.Read(0, 6));
        Assert.Equal(new[] { 0, 1, 3 }, initialized.Effect.ResourcePointers.Select(pointer => pointer.ArenaOffset));
        Assert.Equal(selected.Binding.InitializerResources!.Value.Select(resource => resource.GlobalAddress), initialized.Effect.ResourcePointers.Select(pointer => pointer.GlobalAddress));
        foreach (NativeBattleEffectResourcePointer pointer in initialized.Effect.ResourcePointers)
        {
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + (uint)pointer.ArenaOffset, initialized.Memory.Tonberry90!.ReadDword(pointer.GlobalAddress));
        }
        Assert.Equal(JsonSerializer.Serialize(selected.Binding), JsonSerializer.Serialize(initialized.Effect.Binding));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Conflicting_or_incomplete_metadata_is_rejected_before_loading(int mutation)
    {
        NativeBattleOriginalEffectState selected = Effect();
        ImmutableArray<NativeBattleEffectInitializerResource> descriptors = selected.Binding.InitializerResources!.Value;
        NativeBattleEffectBinding binding = mutation switch
        {
            0 => selected.Binding with { InitializerResources = [] },
            1 => selected.Binding with { InitializerResources = descriptors.Reverse().ToImmutableArray() },
            2 => selected.Binding with { InitializerResources = descriptors.SetItem(1, descriptors[1] with { GlobalAddress = 0x259EEB0 }) },
            3 => selected.Binding with { InitializerResources = descriptors.SetItem(1, descriptors[1] with { Name = "other.tim" }) },
            4 => selected.Binding with { InitializerResource = descriptors[0] },
            5 => selected.Binding with { InitializerAssets = selected.Binding.Tonberry90Resources!.InitializerTims.SetItem(1, new("mag089_1.tim", [255])) },
            6 => selected.Binding with { InitializerResources = null, InitializerAssets = selected.Binding.Tonberry90Resources!.InitializerTims },
            _ => throw new InvalidOperationException()
        };

        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected with { Binding = binding }));
    }

    private static NativeBattleOriginalEffectState Effect()
    {
        NativeBattleTonberry90Resources resources = new(0, new("unused", []), null!, [])
        {
            InitializerTims = [new("mag089_0.tim", [1]), new("mag089_1.tim", [2, 3]), new("mag089_2.tim", [4, 5, 6])]
        };
        return new(90, 89, new(90, 0x762360, 0x762320)
        {
            InitializerIsRet = false,
            Tonberry90Resources = resources,
            InitializerResources = [new("mag089_0.tim", 0x25A4C20), new("mag089_1.tim", 0x259EEAC), new("mag089_2.tim", 0x259EEB0)]
        }, null, null);
    }
}
