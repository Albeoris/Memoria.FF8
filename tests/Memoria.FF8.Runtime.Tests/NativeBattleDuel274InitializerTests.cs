using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274InitializerTests
{
    [Fact]
    public void InitializerSetsActualFileCallbackAndLoadsItsSingleTextureIntoTheNewArena()
    {
        NativeBattleEffectBinding binding = new(274, 0x6897E0, 0x6897F0)
        {
            InitializerIsRet = false,
            InitializerResource = new("mag273.tim", 0x2508410),
            InitializerAsset = new("mag273.tim", ImmutableArray.Create<byte>(1, 2, 3, 4))
        };
        NativeBattleOriginalEffectState selected = new(274, 273, binding, new(0, 0, 8, 0, 0, 0), 0);
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        Assert.Equal(0x689810u, memory.EffectIo!.Callback);
        Assert.Equal(4, memory.Cursor);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, memory.Arena!.Read(0, 4));
        Assert.Equal(new NativeBattleEffectResourcePointer(0x2508410, 0), Assert.Single(effect.ResourcePointers));
        Assert.False(memory.Duel274!.ColdGlobalsPublished);
        Assert.False(memory.Duel274.MutableImagePublished);
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected with { Binding = binding with { Entry = 0x6897E1 } }));
    }
}
