using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStatusMagicResourcesTests
{
    [Theory]
    [InlineData(122, 0, 0x498, 16, 60, 5, 0x49451u)]
    [InlineData(120, 0, 0x140, 6, 16, 3, 0x61AC6u)]
    [InlineData(120, 1, 0x4F4, 21, 32, 5, 0x61AC6u)]
    public void Original_status_layouts_complete_every_native_part_and_restore_the_same_draw(ushort effect, int layout, int workspaceBytes, int nodes, int duration, int geometries, uint sound)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleStatusMagicResources resources = game.ReadNativeStatusMagicResources(effect);
        Assert.Same(resources, game.ReadNativeStatusMagicResources(effect));
        Assert.Equal(sound, resources.Sound);
        NativeBattleGfAnimationSource source = resources.Layouts[layout];
        int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        Assert.Equal(geometries, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()));
        Assert.Equal(nodes, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table)));
        Assert.Equal(duration, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)));
        NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[workspaceBytes]));
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        NativeBattleStatusMagicFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(effect, effect - 1, new(effect, resources.Entry, 1), new(0, 0, 8, 0, 0, 0), null));
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        int allocations = 0;
        int callbacks = 0;
        bool reachedAlternateRotation = false;
        NativeBattleStatusMagicInputs inputs = new(NativeBattleRetainedMemory.FromColdProcess().ActionPackets, projection, NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException("Drawing has its captured anchor."), _ => throw new InvalidOperationException("Drawing has its captured anchor."), (_, _) => throw new InvalidOperationException("Part callback does not change entity flags."), (current, operation) =>
        {
            NativeBattleStatusMagicPackets packets = Assert.IsType<NativeBattleStatusMagicPackets>(operation);
            allocations += packets.Batch.AllocationBytes;
            return current;
        });
        for (int index = 0; index < duration; index++)
        {
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
            {
                callbacks++;
                reachedAlternateRotation |= (node.Header & 0x400) != 0;
                frame = NativeBattleStatusMagicDrawing.Draw(frame, resources, source, node, new(0, -100, 0), -192, inputs);
                return current;
            });
            workspace = advanced.Workspace;
            Assert.Equal(duration - index - 1, advanced.Remaining);
            if (index == duration / 2)
            {
                workspace = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
                frame = NativeBattleSnapshotJson.Deserialize<NativeBattleStatusMagicFrame>(NativeBattleSnapshotJson.Serialize(frame))!;
            }
        }
        Assert.Equal(nodes * duration, callbacks);
        Assert.True(allocations > 0);
        Assert.Equal(effect == 120 && layout == 1, reachedAlternateRotation);
        Assert.Equal(duration, workspace.Frame);
        NativeBattleGfAnimationAdvance end = workspace.Advance701970(source, 0, (_, _) => throw new InvalidOperationException("Completed native layout must not redraw."));
        Assert.Equal(0, end.EmittedNodes);
        Assert.Equal(0, end.Remaining);
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusMagicResourcesReader.Read(new byte[128], effect));
    }
}
