using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFireInitializerTests
{
    [Fact]
    public void InstalledFireInitializerLoadsTheOriginalMagicArchiveResourceWithoutInventedIoTiming()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleEffectBinding fire = assets.ReadNativeEffectCatalog()[1];
        Assert.Equal(0x629880u, fire.Initializer);
        Assert.Equal(0x6298A0u, fire.Entry);
        Assert.Equal("mag001.tim", fire.InitializerAsset!.Name);
        Assert.Equal(132640, fire.InitializerAsset.Data.Length);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(2, assets.ReadNativeEffectCatalog());
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(assets.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
        Assert.Equal(fire.InitializerAsset.Data.Length, memory.Cursor);
        Assert.Equal(fire.InitializerAsset.Data.ToArray(), memory.Arena!.Read(0, memory.Cursor));
        Assert.Equal(new NativeBattleEffectResourcePointer(0x24CBD68, 0), Assert.Single(effect.ResourcePointers));
        Assert.Null(effect.ReturnedDescriptor);
    }
}
