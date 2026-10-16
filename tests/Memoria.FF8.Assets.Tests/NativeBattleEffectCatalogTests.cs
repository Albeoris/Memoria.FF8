using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectCatalogTests
{
    [Fact]
    public void OriginalExecutableSuppliesAll400EffectBindingsAndActualAutomaticGuardianModules()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        var catalog = assets.ReadNativeEffectCatalog();
        Assert.Equal(400, catalog.Length);
        Assert.Equal(Enumerable.Range(1, 400).Select(value => (ushort)value), catalog.Select(binding => binding.Effect));
        Assert.Equal(new NativeBattleEffectBinding(187, 0x6472E0, 0x575570) { InitializerIsRet = true }, catalog[186]);
        Assert.Equal(new NativeBattleEffectBinding(327, 0x58D760, 0x58D6E0) { InitializerIsRet = false }, catalog[326]);
        Assert.Equal(new NativeBattleEffectBinding(328, 0x58D930, 0x58D700) { InitializerIsRet = false }, catalog[327]);
        Assert.Equal(new NativeBattleEffectBinding(329, 0x58DB10, 0x58D720) { InitializerIsRet = false }, catalog[328]);
        Assert.Equal(new NativeBattleEffectBinding(330, 0x58DCF0, 0x58D740) { InitializerIsRet = false }, catalog[329]);
        Assert.Equal(new NativeBattleEffectBinding(89, 0x6F9CF0, 0x5755E0) { InitializerIsRet = true }, catalog[88]);
        Assert.Equal(catalog, assets.ReadNativeEffectCatalog());
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectCatalogReader.Read(new byte[128]));
    }
}
