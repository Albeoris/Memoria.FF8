using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgamesh329InitializerTests
{
    [Fact]
    public void Selected_329_catalog_keeps_generic_ordered_TIM_assets_and_executes_original_initializer_before_entry()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        var catalog = game.ReadNativeEffectCatalogForEffects([329]);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(329, catalog);
        Assert.Equal(0x58DB10u, selected.Binding.Entry);
        Assert.Equal(0x58D720u, selected.Binding.Initializer);
        Assert.NotNull(selected.Binding.GilgameshResources);
        var resources = NativeBattleEffectInitializerPolicy.Resources(selected.Binding);
        var assets = NativeBattleEffectInitializerPolicy.Assets(selected.Binding);
        Assert.Equal("mag326-329.tim", Assert.Single(resources).Name);
        Assert.Equal(0x2200F9Cu, resources[0].GlobalAddress);
        Assert.Equal(resources.Select(resource => resource.Name), assets.Select(asset => asset.Name));
        Assert.Equal(167136, NativeBattleStorageSnapshots.Capture(Assert.Single(assets)).Data.Length);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(selected.Binding.GilgameshResources.Models.InitializerTim).Data.ToArray(), NativeBattleStorageSnapshots.Capture(assets[0]).Data.ToArray());
        NativeBattleEffectMemory cold = game.ReadColdProcessNativeBattleMemory().EffectMemory ?? throw new InvalidDataException("Original initializer requires its explicit cold process.");
        cold = cold with { Gilgamesh = cold.Gilgamesh! with { Variant = unchecked((int)0xA5A51234) } };
        var initialized = NativeBattleOriginalEffects.Initialize(cold, selected);
        Assert.Equal(167136, initialized.Memory.Cursor);
        Assert.Equal(328, initialized.Memory.SelectedIndex);
        Assert.Equal(1, initialized.Memory.Active);
        Assert.Equal(0, initialized.Memory.AllocationCount);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(assets[0]).Data.ToArray(), initialized.Memory.Arena!.Read(0, NativeBattleStorageSnapshots.Capture(assets[0]).Data.Length));
        Assert.Equal(new NativeBattleEffectResourcePointer(0x2200F9C, 0), Assert.Single(initialized.Effect.ResourcePointers));
        Assert.Equal(unchecked((int)0xA5A51234), initialized.Memory.Gilgamesh!.Variant);
        Assert.Null(initialized.Effect.ReturnedDescriptor);
        Assert.Empty(initialized.Effect.Pools);
        Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(new Saved(initialized.Memory, initialized.Effect)))!;
        Assert.Equal(NativeBattleSnapshotJson.Serialize(initialized.Memory), NativeBattleSnapshotJson.Serialize(restored.Memory));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(initialized.Effect), NativeBattleSnapshotJson.Serialize(restored.Effect));
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(cold, selected with { Binding = selected.Binding with { InitializerAsset = null } }));
    }

    private sealed record Saved(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
}
