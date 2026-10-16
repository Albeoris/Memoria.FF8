using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGraphicsRulesTests
{
    [Fact]
    public void OriginalExecutableMapsSavedAndBattleStatusesToTheSeparateGraphicsBits()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleGraphicsRules rules = assets.ReadNativeBattleGraphicsRules();
        Assert.Equal(2u, rules.Project(1, 0));
        Assert.Equal(0x1000u, rules.Project(0, 1));
        Assert.Equal(0x10u, rules.Project(4, 0));
        Assert.Equal(0x15u, rules.Project(0x104, 0x800000));
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsRulesReader.Read(new byte[128]));
    }

    [Fact]
    public void EnemyGraphicsResourceRetainsItsActualDeathAndEntryPrograms()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(61);
        Assert.True(resource.Contains(3));
        Assert.True(resource.Contains(8));
        Assert.False(resource.Contains(0));
        Assert.False(resource.Contains(255));
        Assert.Equal(assets.LoadEnemy(61).Sequences!.NativeResource.SequenceSection, resource.SequenceSection);
    }
}
