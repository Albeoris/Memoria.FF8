using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleControl110ResourcesTests
{
    [Fact]
    public void Original_stream_static_frame_and_flipbooks_preserve_loader_and_resource_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleControl110Resources resources = game.ReadNativeControl110Resources();
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleAiDefinition biggs = battle.ReadEnemyAi(74, 10);
        NativeBattleAiDefinition wedge = battle.ReadEnemyAi(73, 10);
        Assert.Equal(new byte[] { 4, 205, 30, 110, 29, 0, 29, 1, 31, 2 }, biggs.Scripts[3].AsSpan(32, 10).ToArray());
        Assert.Equal(new byte[] { 4, 205, 30, 110, 29, 0, 29, 1, 31, 2 }, wedge.Scripts[3].AsSpan(11, 10).ToArray());
        Assert.False(string.IsNullOrWhiteSpace(biggs.Texts[10]));
        Assert.False(string.IsNullOrWhiteSpace(wedge.Texts[1]));
        Assert.Equal(0x2588, resources.Stream.Length);
        Assert.Equal(308, BinaryPrimitives.ReadInt32LittleEndian(resources.Stream.AsSpan().Slice(4)));
        Assert.Equal(20, resources.StaticFrame.Length);
        Assert.All(resources.StaticFrame, value => Assert.Equal((byte)0, value));
        Assert.Equal(308 * 8, NativeBattleControl110Motion.Normalize(resources.Stream.Slice(8, 308 * 8)).Length);
        foreach (var item in new[] { (0, 8), (1, 13) })
        {
            for (int frame = 0; frame < item.Item2; frame++)
            {
                Assert.NotNull(resources.Sprites[item.Item1].ProjectedFrame571C80(frame));
            }
        }
    }
}
