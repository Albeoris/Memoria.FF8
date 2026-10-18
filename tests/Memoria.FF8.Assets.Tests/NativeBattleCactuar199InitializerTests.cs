using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCactuar199InitializerTests
{
    [Fact]
    public void AuthenticatedCatalogMaterializesHeapInitializerWithoutAdvertisingAnArenaLoaderOrEntryDispatch()
    {
        using GameAssets game = Installed();
        NativeBattleEffectBinding raw = game.ReadNativeEffectCatalog()[198];
        Assert.Equal((ushort)199, raw.Effect);
        Assert.Equal(0x5A8750u, raw.Entry);
        Assert.Equal(0x5A8730u, raw.Initializer);
        Assert.False(raw.InitializerIsRet);
        Assert.Null(raw.InitializerResource);
        Assert.Null(raw.InitializerResources);
        Assert.Null(raw.InitializerAsset);
        Assert.Null(raw.Cactuar199Resources);
        ImmutableArray<NativeBattleEffectBinding> catalog = game.ReadNativeEffectCatalogForEffects([199]);
        NativeBattleEffectBinding binding = catalog[198];
        Assert.NotNull(binding.Cactuar199Resources);
        Assert.Null(binding.InitializerResource);
        Assert.Null(binding.InitializerAssets);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(199, catalog);
        NativeBattleEffectHeapLoad previous = NativeBattleEffectMemory.FromColdProcess().BeginLoad(1).LoadIntoArena([0xA5, 0xB6]).LoadHeap5718E0(new("old.bin", [7, 8]));
        var initialized = NativeBattleOriginalEffects.Initialize(previous.Memory, selected);
        Assert.Equal(198, initialized.Memory.SelectedIndex);
        Assert.Equal(1, initialized.Memory.AllocationCount);
        Assert.Equal(0, initialized.Memory.Cursor);
        Assert.Equal(new byte[16], initialized.Memory.Arena!.Read(0, 16));
        Assert.Empty(initialized.Effect.ResourcePointers);
        NativeBattleEffectHeapResourcePointer pointer = Assert.Single(initialized.Effect.HeapResourcePointers!.Value);
        Assert.Equal(0x225A82Cu, pointer.GlobalAddress);
        Assert.Equal(0, pointer.AllocationIndex);
        Assert.Equal(binding.Cactuar199Resources!.InitializerHeapTim.Data.ToArray(), initialized.Effect.ReadHeapResource(initialized.Memory, 0x225A82C).Data.ToArray());
        Assert.Equal("mag198.tim", initialized.Memory.Allocations[0]!.Identity);
        Assert.Equal(0, initialized.Memory.Textures!.Count);
        Assert.Null(initialized.Memory.Textures.Animation);
        Assert.Null(initialized.Effect.Parameters);
        Assert.Null(initialized.Effect.ReturnedDescriptor);
        Saved saved = new(initialized.Memory, initialized.Effect);
        string json = JsonSerializer.Serialize(saved);
        Saved restored = JsonSerializer.Deserialize<Saved>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        (NativeBattleEffectTextureState queued, _) = restored.Memory.Textures!.EnqueueTim505E30(restored.Effect.ReadHeapResource(restored.Memory, 0x225A82C));
        Assert.NotEmpty(queued.Drain505D20(restored.Memory.Arena).Uploads);
        var nextLoad = NativeBattleOriginalEffects.Initialize(restored.Memory, selected);
        Assert.Equal(1, nextLoad.Memory.AllocationCount);
        Assert.Equal(0, nextLoad.Memory.Cursor);
        Assert.Equal(binding.Cactuar199Resources.InitializerHeapTim.Data.ToArray(), nextLoad.Memory.Allocations[0]!.Data.ToArray());
    }

    [Fact]
    public void SpecializedHeapContractRejectsCrossedTypedResourceAndGenericArenaMetadata()
    {
        using GameAssets game = Installed();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([199])[198];
        NativeBattleOriginalEffectState selected = new(199, 198, binding, null, null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(memory, selected with { Binding = binding with { Effect = 198 } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(memory, selected with { Binding = binding with { Initializer = 0x5A8731 } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(memory, selected with { Binding = binding with { InitializerResources = [new("mag198.tim", 0x225A82C)], InitializerAssets = [new("mag198.tim", binding.Cactuar199Resources!.InitializerHeapTim.Data)] } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(memory, selected with { Binding = binding with { Cactuar199Resources = binding.Cactuar199Resources! with { InitializerHeapTim = new("other.tim", binding.Cactuar199Resources.InitializerHeapTim.Data) } } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(memory, selected with { Binding = binding with { Cactuar199Resources = binding.Cactuar199Resources! with { InitializerHeapTim = new("mag198.tim", []) } } }));
    }
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Saved(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
}
