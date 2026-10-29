using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModuleInitializerResourcesTests
{
    [Fact]
    public void ThirteenOriginalModuleProfilesLoadBothAssetsIntoTheirPublishedArenaOffsets()
    {
        ushort[] effects = [75, 77, 202, 204, 205, 206, 208, 211, 219, 220, 221, 222, 223];
        using GameAssets assets = new(TestResources.GameDirectory);
        ImmutableArray<NativeBattleEffectBinding> catalog = assets.ReadNativeEffectCatalogForEffects(effects);
        Assert.Equal(effects, catalog.Where(binding => binding.InitializerAssets is not null).Select(binding => binding.Effect).ToArray());
        foreach (ushort effect in effects)
        {
            NativeBattleEffectBinding binding = catalog[effect - 1];
            ImmutableArray<NativeBattleEffectInitializerResource> resources = NativeBattleEffectInitializerPolicy.Resources(binding);
            ImmutableArray<NativeBattleEffectInitializerAsset> originals = NativeBattleEffectInitializerPolicy.Assets(binding);
            Assert.Equal(2, resources.Length);
            Assert.Equal(2, originals.Length);
            Assert.Null(binding.InitializerAsset);
            Assert.Null(binding.InitializerResource);
            Assert.Equal($"mag{effect - 1:D3}_b.00", resources[0].Name);
            Assert.Equal($"mag{effect - 1:D3}_b.01", resources[1].Name);
            Assert.NotEqual(resources[0].GlobalAddress, resources[1].GlobalAddress);
            Assert.All(originals, original => Assert.NotEmpty(original.Data));
            NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, catalog);
            var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
            Assert.Equal(originals.Sum(original => original.Data.Length), initialized.Memory.Cursor);
            Assert.Equal(resources.Select(resource => resource.GlobalAddress), initialized.Effect.ResourcePointers.Select(pointer => pointer.GlobalAddress));
            Assert.Equal(new[] { 0, originals[0].Data.Length }, initialized.Effect.ResourcePointers.Select(pointer => pointer.ArenaOffset));
            for (int index = 0; index < 2; index++)
            {
                Assert.True(originals[index].Data.AsSpan().SequenceEqual(initialized.Memory.Arena!.Read(initialized.Effect.ResourcePointers[index].ArenaOffset, originals[index].Data.Length)));
            }
            string json = NativeBattleSnapshotJson.Serialize(new Saved(initialized.Memory, initialized.Effect));
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(json)!;
            Assert.Equal(json, NativeBattleSnapshotJson.Serialize(restored));
            Assert.Equal(initialized.Memory.Cursor, restored.Memory.Cursor);
            Assert.Equal(initialized.Memory.Active, restored.Memory.Active);
            Assert.Same(restored.Effect, NativeBattleEffectResourceResolver.MaterializeSelected(restored.Effect, null));
            for (int index = 0; index < 2; index++)
            {
                Assert.True(originals[index].Data.AsSpan().SequenceEqual(restored.Memory.Arena!.Read(restored.Effect.ResourcePointers[index].ArenaOffset, originals[index].Data.Length)));
            }
        }
        NativeBattleEffectBinding water = catalog[221];
        Assert.Equal(new uint[] { 0x2796BDC, 0x2796BD8 }, NativeBattleEffectInitializerPolicy.Resources(water).Select(resource => resource.GlobalAddress));
    }

    private sealed record Saved(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
}
