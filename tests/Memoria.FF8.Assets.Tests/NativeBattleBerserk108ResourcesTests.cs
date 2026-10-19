using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBerserk108ResourcesTests
{
    [Fact]
    public void Original_burst_keeps_seven_nodes_and_all36_frames_with_three_sprite_flipbooks()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBerserk108Resources resources = game.ReadNativeBerserk108Resources();
        Assert.Equal(0x3E14, resources.Burst.Data.Length);
        Assert.Equal(7, resources.PartFlags.Length);
        Assert.Equal(4, resources.RayColors.Length);
        Assert.Equal(16, resources.TextureWindows.Length);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x29C].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Burst, 0x29C);
        for (int frame = 0; frame < 36; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Burst, 0, (current, _) => current);
            Assert.Equal(35 - frame, step.Remaining);
            Assert.Equal(7, step.EmittedNodes);
            workspace = step.Workspace;
        }
        foreach (var item in new[] { (0, 6), (1, 14), (2, 15) })
        {
            for (int frame = 0; frame < item.Item2; frame++)
            {
                Assert.NotNull(resources.Sprites[item.Item1].ProjectedFrame571C80(frame));
            }
        }
    }
}
