using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAngelWing84ProgramResourcesTests
{
    [Theory]
    [InlineData(0, 1184, 0)]
    [InlineData(1, 344, 1)]
    [InlineData(2, 1928, 2)]
    public void Every_original_program_node_reaches_its_native_mesh_and_shape(int index, int workspaceBytes, ushort mode)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleAngelWing84ProgramResources resources = game.ReadNativeAngelWing84ProgramResources();
        NativeBattleGfAnimationSource source = resources.Sources[index];
        NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaceBytes].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        NativeBattleActionMatrix context = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8192, 4096);
        NativeBattleCameraProjection projection = new(context, 216, 160, 108);
        int nodes = 0;
        int allocation = 0;
        bool completed = false;
        for (int tick = 0; tick < 1024; tick++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleGfGeometryBatch batch = NativeBattleAngelWing84Programs.DrawNode(source, node, context, 0, mode, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
                nodes++;
                allocation += batch.AllocationBytes;
                return current;
            });
            workspace = step.Workspace;
            if (step.Remaining == 0)
            {
                completed = true;
                break;
            }
        }
        Assert.True(completed);
        Assert.True(nodes > 0);
        Assert.True(allocation > 0);
    }
}
