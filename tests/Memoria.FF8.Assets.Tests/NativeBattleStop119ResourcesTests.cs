using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStop119ResourcesTests
{
    [Fact]
    public void Original_clock_has_seventeen_records_sixty_frames_and_all514_textured_fragments()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleStop119Resources resources = game.ReadNativeStop119Resources();
        Assert.Equal(0x127DB5Cu, resources.Clock.Address);
        Assert.Equal(0x92A8, resources.Clock.Data.Length);
        Assert.Equal(0xE30, resources.Camera.Data.Length);
        Assert.Equal(0x49457u, resources.Sound);
        int table = Read(4);
        Assert.Equal(17, Read(table));
        Assert.Equal(60, Read(table + 4));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int[] expected = [29, 76, 232, 52, 30, 37, 58];
        for (int group = 0; group < 7; group++)
        {
            int geometry = Read(8 + NativeBattleStop119Geometry.GeometryIndex(group) * 4);
            int cursor = geometry + Read(geometry);
            int total = 0;
            for (int kind = 0; kind < 8; kind++)
            {
                int count = Read(cursor);
                if (kind is 0 or 1 or 4 or 5)
                {
                    Assert.Equal(0, count);
                }
                total += count;
                cursor += 4 + count * strides[kind];
            }
            Assert.Equal(expected[group], total);
        }
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x4E8].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Clock, 0x4E8);
        HashSet<ushort> piecesAtBreak = [];
        for (int frame = 0; frame < 60; frame++)
        {
            int currentFrame = frame;
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Clock, 0, (current, node) =>
            {
                int group = NativeBattleStop119Geometry.Group(node.NodeIndex);
                if (currentFrame == 53 && group >= 0)
                {
                    Assert.Equal(NativeBattleStop119Geometry.GeometryIndex(group), (int)node.GeometryIndex);
                    Assert.Equal(0u, node.Header & 0x200);
                    Assert.NotEqual(new NativeBattleVector(0, 0, 0), node.Vector24);
                    piecesAtBreak.Add(node.NodeIndex);
                }
                return current;
            });
            Assert.Equal(59 - frame, step.Remaining);
            workspace = step.Workspace;
        }
        Assert.Equal(new ushort[] { 3, 5, 7, 9, 11, 13, 15 }, piecesAtBreak.Order().ToArray());
        Assert.Equal(0, workspace.Advance701970(resources.Clock, 0, (current, _) => current).EmittedNodes);

        int Read(int offset) => BinaryPrimitives.ReadInt32LittleEndian(resources.Clock.Data.AsSpan().Slice(offset));
    }
}
