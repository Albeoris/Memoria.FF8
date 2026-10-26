using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLevelResourcesTests
{
    [Theory]
    [InlineData(271, 18216, 0x4A0)]
    [InlineData(272, 18232, 0x4D0)]
    public void Original_programme_closes_all_five_meshes_and36_frames_with_its_actual_workspace_and_camera(int effect, int length, int workspaceBytes)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleLevelResources resources = game.ReadNativeLevelResources(effect);
        Assert.Same(resources, game.ReadNativeLevelResources(effect));
        Assert.Equal(effect, resources.Effect);
        Assert.Equal(length, resources.Animation.Data.Length);
        Assert.Equal(2252, resources.Camera.Data.Length);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan()));
        Assert.Equal(8, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(2)));
        Assert.Equal(324, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4)));
        Assert.NotEqual(0u, resources.Sound);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[workspaceBytes].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Animation, workspaceBytes);
        HashSet<int> meshes = [];
        for (int frame = 0; frame < 36; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Animation, 0, (current, node) =>
            {
                meshes.Add(node.GeometryIndex);
                Assert.InRange(node.GeometryIndex, (ushort)0, (ushort)4);
                Assert.Equal((short)0, node.Word40);
                Assert.Equal((short)0, node.Word42);
                return current;
            });
            Assert.Equal(35 - frame, step.Remaining);
            Assert.InRange(step.EmittedNodes, 1, 14);
            workspace = step.Workspace;
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, meshes.Order());
        Assert.Equal(0, workspace.Advance701970(resources.Animation, 0, (current, _) => current).EmittedNodes);
    }
}
