using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectInitializerPolicyTests
{
    [Fact]
    public void OrderedAssetsUseOneRetainedArenaAndPreserveIndependentRestore()
    {
        NativeBattleEffectBinding binding = Binding() with { InitializerAssets = AssetList(new("first.bin", [11, 12, 13]), new("second.bin", [21, 22])) };
        NativeBattleOriginalEffectState selected = Select(binding);
        var loaded = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        Assert.Equal(5, loaded.Memory.Cursor);
        Assert.Equal(new byte[] { 11, 12, 13, 21, 22 }, loaded.Memory.Arena!.Read(0, 5));
        Assert.Equal(new[] { new NativeBattleEffectResourcePointer(0x1000, 0), new NativeBattleEffectResourcePointer(0x2000, 3) }, loaded.Effect.ResourcePointers);
        string json = JsonSerializer.Serialize(new Saved(loaded.Memory, loaded.Effect));
        Saved restored = JsonSerializer.Deserialize<Saved>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(new byte[] { 21, 22 }, restored.Memory.Arena!.Read(restored.Effect.ResourcePointers[1].ArenaOffset, 2));
        Assert.Equal(loaded.Memory.Cursor, restored.Memory.Cursor);
        Assert.Equal(loaded.Memory.Active, restored.Memory.Active);
    }

    [Fact]
    public void OldSingularBindingsKeepTheirExactSerializedShape()
    {
        NativeBattleEffectBinding binding = new(1, 0x1234, 0x5678) { InitializerResource = new("single.bin", 0x1000), InitializerAsset = new("single.bin", [31]) };
        string json = JsonSerializer.Serialize(binding);
        Assert.DoesNotContain("InitializerResources", json);
        Assert.DoesNotContain("InitializerAssets", json);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleEffectBinding>(json)));
        var loaded = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), Select(binding));
        Assert.Equal(1, loaded.Memory.Cursor);
        Assert.Single(loaded.Effect.ResourcePointers);
    }

    [Fact]
    public void ExplicitListsRejectMissingMixedDefaultEmptyDuplicateAndCrossedOwners()
    {
        NativeBattleEffectBinding binding = Binding();
        Assert.Throws<NotSupportedException>(() => Load(binding));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerResources = default(ImmutableArray<NativeBattleEffectInitializerResource>) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerResources = ResourceList() }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerResource = new("single.bin", 0x3000) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerAsset = new("single.bin", [1]) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerResources = ResourceList(new("first.bin", 0x1000), new("second.bin", 0x1000)) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerAssets = default(ImmutableArray<NativeBattleEffectInitializerAsset>) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerAssets = AssetList() }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerAssets = AssetList(new NativeBattleEffectInitializerAsset("first.bin", [1])) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerAssets = AssetList(new("second.bin", [1]), new("first.bin", [2])) }));
        Assert.Throws<InvalidDataException>(() => Load(binding with { InitializerResources = null, InitializerAssets = AssetList(new NativeBattleEffectInitializerAsset("first.bin", [1])) }));
        Assert.Throws<InvalidDataException>(() => Select(binding with { InitializerResources = ResourceList() }).Validate());
        Assert.Throws<InvalidDataException>(() => Load(binding with { Initializer = 0x6C3560, InitializerAssets = AssetList(new("first.bin", [1]), new("second.bin", [2])) }));
    }

    [Fact]
    public void ResolverAcceptsEquivalentMetadataArraysAndRejectsReorderedGlobals()
    {
        NativeBattleOriginalEffectState selected = Select(Binding());
        NativeBattleOriginalEffectState materialized = NativeBattleEffectResourceResolver.MaterializeSelected(selected, _ => Binding() with { InitializerAssets = AssetList(new("first.bin", [1]), new("second.bin", [2])) });
        Assert.Equal(2, NativeBattleEffectInitializerPolicy.Assets(materialized.Binding).Length);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectResourceResolver.MaterializeSelected(selected, _ => Binding() with { InitializerResources = ResourceList(new("first.bin", 0x2000), new("second.bin", 0x1000)) }));
    }

    private static NativeBattleEffectBinding Binding() => new(1, 0x1234, 0x5678) { InitializerIsRet = false, InitializerResources = ResourceList(new("first.bin", 0x1000), new("second.bin", 0x2000)) };

    private static ImmutableArray<NativeBattleEffectInitializerResource> ResourceList(params NativeBattleEffectInitializerResource[] resources) => resources.ToImmutableArray();

    private static ImmutableArray<NativeBattleEffectInitializerAsset> AssetList(params NativeBattleEffectInitializerAsset[] assets) => assets.ToImmutableArray();

    private static NativeBattleOriginalEffectState Select(NativeBattleEffectBinding binding) => NativeBattleOriginalEffects.Select(1, Enumerable.Range(1, 400).Select(index => binding with { Effect = (ushort)index }).ToImmutableArray());

    private static void Load(NativeBattleEffectBinding binding)
    {
        _ = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), Select(binding));
    }

    private sealed record Saved(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
}
