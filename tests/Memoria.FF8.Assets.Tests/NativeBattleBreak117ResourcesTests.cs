using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBreak117ResourcesTests
{
    [Fact]
    public void Original_shell_workspace_covers54_records82_frames_and_original_cut_groups()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBreak117Resources resources = game.ReadNativeBreak117Resources();
        Assert.Equal(0x9658, resources.Shell.Data.Length);
        Assert.Equal(0x1D8, resources.Debris.Length);
        Assert.Equal(0xFE0, resources.Camera.Data.Length);
        Assert.Equal(0x49412u, resources.Sound);
        int table = Read(4);
        Assert.Equal(54, Read(table));
        Assert.Equal(82, Read(table + 4));
        for (int model = 0; model < 28; model++)
        {
            int geometry = Read(8 + model * 4);
            int cursor = geometry + Read(geometry);
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                int count = Read(cursor);
                if (group is not (2 or 6))
                {
                    Assert.Equal(0, count);
                }
                cursor += 4 + count * strides[group];
            }
        }
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x1554].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Shell, 0x1554);
        for (int frame = 0; frame < 82; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Shell, 0, (current, _) => current);
            Assert.Equal(81 - frame, step.Remaining);
            workspace = step.Workspace;
        }
        Assert.Equal((ushort)14, BinaryPrimitives.ReadUInt16LittleEndian(resources.Spark.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0, workspace.Advance701970(resources.Shell, 0, (current, _) => current).EmittedNodes);

        int Read(int offset) => BinaryPrimitives.ReadInt32LittleEndian(resources.Shell.Data.AsSpan().Slice(offset));
    }
}
