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
        //505850 index0 tests the actual nonzero count WORD itself.
        Assert.True(resource.Contains(0));
        Assert.True(resource.ContainsNativeIndex505850(0));
        Assert.False(resource.Contains(255));
        Assert.Equal(assets.LoadEnemy(61).Sequences!.NativeResource.SequenceSection.ToArray(), resource.SequenceSection.ToArray());
        NativeBattleModelDatSource source = assets.ReadNativeModelDatSource(77, false, 0);
        Assert.Equal(source.Section(5) != source.Section(6), resource.HasActionCamera);
        if (resource.HasActionCamera == true)
        {
            Assert.Equal(source.Bytes.Slice(source.Section(5), source.Section(6) - source.Section(5)).ToArray(), resource.ActionCameraBank!.Data.ToArray());
        }
    }
}
