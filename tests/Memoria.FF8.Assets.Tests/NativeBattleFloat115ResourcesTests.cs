using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleFloat115ResourcesTests
{
    [Fact]
    public void Original_clock_and_lower_programs_keep_all_nodes_and_complete_their_actual_workspaces()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleFloat115Resources resources = game.ReadNativeFloat115Resources();
        Assert.Equal(0x1FE8, resources.Glyph.Data.Length);
        Assert.Equal(0x21C0, resources.Upper.Data.Length);
        foreach (var item in new[] { (resources.Glyph, 0x2E8, 8), (resources.Upper, 0x26C, 7) })
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
