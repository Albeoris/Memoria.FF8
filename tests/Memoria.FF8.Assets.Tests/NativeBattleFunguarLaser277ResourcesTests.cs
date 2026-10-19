using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFunguarLaser277ResourcesTests
{
    [Fact]
    public void Original_resources_preserve_kind_seven_glow_and_all_sixteen_projected_frames()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleFunguarLaser277Resources resources = game.ReadNativeFunguarLaser277Resources();
        Assert.Equal(0, resources.Spark.Kind);
        Assert.Equal(7, resources.Glow.Kind);
        Assert.Equal(0x1AC, resources.Spark.Bytes.Length);
        Assert.Equal(0x210, resources.Glow.Bytes.Length);
        for (int frame = 0; frame < 16; frame++)
        {
            Assert.Equal(20, resources.Spark.ProjectedFrame571C80(frame).Quads.Length);
            Assert.Equal(frame < 5 ? 40 : 20, resources.Glow.ProjectedFrame571C80(frame).Quads.Length);
        }
        Assert.Equal(-1, resources.Glow.ProjectedFrame571C80(15).NextOffset);
        foreach (var item in new[] { (resources.Muzzle, 0x3B0), (resources.Ray, 0xE8), (resources.Impact, 0x1B0), (resources.Hit, 0x7C) })
        {
            NativeBattleGfAnimationWorkspace workspace = new(new byte[item.Item2].ToImmutableArray());
            _ = workspace.Initialize7016B0(item.Item1, item.Item2);
        }
    }
}
