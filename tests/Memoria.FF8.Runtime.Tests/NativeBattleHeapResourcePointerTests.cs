using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHeapResourcePointerTests
{
    [Fact]
    public void HeapGlobalResolvesItsTrackedIndexAcrossRestoreAndNeverBecomesAnArenaOffset()
    {
        NativeBattleEffectHeapLoad loaded = NativeBattleEffectMemory.FromColdProcess().BeginLoad(198).LoadIntoArena([0xEE]).LoadHeap5718E0(new("file.tim", [1, 2, 3]));
        NativeBattleOriginalEffectState effect = Effect() with { HeapResourcePointers = [new(0x225A82C, loaded.AllocationIndex)] };
        Saved restored = JsonSerializer.Deserialize<Saved>(NativeBattleTestSnapshots.Json(new Saved(loaded.Memory, effect)))!;
        Assert.Equal("file.tim", restored.Effect.ReadHeapResource(restored.Memory, 0x225A82C).Identity);
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Effect.ReadHeapResource(restored.Memory, 0x225A82C).Data.ToArray());
        Assert.Empty(restored.Effect.ResourcePointers);
        Assert.Equal(1, restored.Memory.Cursor);
        Assert.Throws<NotSupportedException>(() => restored.Effect.ReadHeapResource(restored.Memory.BeginLoad(1), 0x225A82C));
        Assert.Throws<NotSupportedException>(() => restored.Effect.ReadHeapResource(restored.Memory, 0x225A830));
        Assert.Throws<InvalidDataException>(() => (effect with { HeapResourcePointers = ImmutableArray<NativeBattleEffectHeapResourcePointer>.Empty }).Validate());
        Assert.Throws<InvalidDataException>(() => (effect with { HeapResourcePointers = default(ImmutableArray<NativeBattleEffectHeapResourcePointer>) }).Validate());
        Assert.Throws<InvalidDataException>(() => (effect with { HeapResourcePointers = [new(0x225A82C, 256)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (effect with { HeapResourcePointers = [new(0x225A82C, 0), new(0x225A82C, 1)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (effect with { ResourcePointers = [new(0x225A82C, 0)] }).Validate());
    }

    [Fact]
    public void OldJsonOmitsNewHeapAndCactuarFieldsAndHeapInitializerRejectsArenaMetadataOrMissingResource()
    {
        NativeBattleOriginalEffectState effect = Effect();
        string json = NativeBattleTestSnapshots.Json(effect);
        Assert.DoesNotContain("HeapResourcePointers", json);
        Assert.DoesNotContain("Cactuar199Resources", json);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleOriginalEffectState>(json)));
        NativeBattleOriginalEffectState cactuar = effect with { RequestedEffect = 199, SelectedIndex = 198, Binding = new(199, 0x5A8750, 0x5A8730) };
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), cactuar));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), cactuar with { Binding = cactuar.Binding with { Entry = 0x5A8751 } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), cactuar with { Binding = cactuar.Binding with { InitializerIsRet = true } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), cactuar with { Binding = cactuar.Binding with { InitializerResource = new("mag198.tim", 0x225A82C), InitializerAsset = new("mag198.tim", [1]) } }));
    }
    private static NativeBattleOriginalEffectState Effect() => new(1, 0, new(1, 0x1000, 0), null, null);
    private sealed record Saved(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
}
