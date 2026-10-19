using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSlow123ResourcesTests
{
    [Fact]
    public void Original_clock_and_lower_programs_keep_all_nodes_and_complete_their_actual_workspaces()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleSlow123Resources resources = game.ReadNativeSlow123Resources();
        Assert.Equal(0x3A8C, resources.Clock.Data.Length);
        Assert.Equal(0x49F4, resources.Lower.Data.Length);
        foreach (var item in new[] { (resources.Clock, 0x1D8, 5), (resources.Lower, 0x568, 18) })
        {
            int table = BinaryPrimitives.ReadInt32LittleEndian(item.Item1.Data.AsSpan().Slice(4));
            int count = BinaryPrimitives.ReadInt32LittleEndian(item.Item1.Data.AsSpan().Slice(table));
            int frames = BinaryPrimitives.ReadInt32LittleEndian(item.Item1.Data.AsSpan().Slice(table + 4));
            Assert.Equal(item.Item3, count);
            NativeBattleGfAnimationWorkspace workspace = new(new byte[item.Item2].ToImmutableArray());
            workspace = workspace.Initialize7016B0(item.Item1, item.Item2);
            for (int frame = 0; frame < frames; frame++)
            {
                NativeBattleGfAnimationAdvance step = workspace.Advance701970(item.Item1, 0, (current, _) => current);
                Assert.Equal(frames - frame - 1, step.Remaining);
                workspace = step.Workspace;
            }
        }
    }
}
