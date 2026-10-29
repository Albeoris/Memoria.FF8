using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRinoa87ResourcesTests
{
    [Fact]
    public void Original_model_override_IO_and_node_owners_have_exact_extents()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa87Resources resources = game.ReadNativeRinoa87Resources();
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(0x4F10, resources.Model.Bytes.Length);
        Assert.Equal(0x11F4, NativeBattleStorageSnapshots.Capture(resources.ActorOverride).Data.Length);
        Assert.Equal(new[] { 18524, 33312, 16928 }, resources.Io.Files.Select(file => NativeBattleStorageSnapshots.Capture(file).Data.Length));
        resources.Nodes.Validate();
        Assert.Equal(NativeBattleRinoa87NodeResources.Length, 0x12B0 + 0x158);
        Assert.Equal(new byte[] { 0, 4, 4 }, new[] { 0x1000, 0x1158, 0x12B0 }.Select(offset => NativeBattleStorageSnapshots.Capture(resources.Nodes).Data[offset + 22]));
        Assert.Equal(new short[] { 5, 4, 3, 3, 2, 1 }, resources.FirstImpactCounts);
        Assert.Equal(new short[] { 12, 10, 8, 6, 4, 2 }, resources.SecondImpactCounts);
        NativeBattleRinoa87IoState io = NativeBattleRinoa87Io.Initialize773D40(NativeBattleStorageSnapshots.Capture(resources.Io.Script).Data, 0x20FFAB8);
        Assert.Equal(0x19EB018u, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(276)));
        Assert.Equal(0x19CF018u, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(300)));
    }

    [Theory]
    [InlineData(0, 176)]
    [InlineData(1, 428)]
    public void Original_programs_execute_all_reached_nodes_shapes_and_packets_with_independent_next_restore(int program, int clearBytes)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa87Resources resources = game.ReadNativeRinoa87Resources();
        NativeBattleGfAnimationSource source = program == 0 ? resources.Program : new(0x19CF018, NativeBattleStorageSnapshots.Capture(resources.Io.Files[0]).Data);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, 456).ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, clearBytes);
        ImmutableArray<byte> morph = Enumerable.Repeat((byte)0xA5, 1120).ToImmutableArray();
        NativeBattleModulePoolTask task = new(0, 0, null, 0x7764B0, new byte[604 - 12].ToImmutableArray());
        int nodes = 0;
        int packets = 0;
        int remaining = -1;
        for (int call = 0; call < 256 && remaining != 0; call++)
        {
            NativeBattleGfAnimationWorkspace clone = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
            ImmutableArray<byte> before = morph;
            List<NativeBattleGfGeometryBatch> first = [];
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleRinoa87NodeDraw drawn = NativeBattleRinoa87ProgramDrawing.Draw7766B0(source, new(view, node, task, 0x25B46A8), morph, projection, colors);
                morph = drawn.MorphScratch;
                first.Add(drawn.Packets);
                nodes++;
                packets += drawn.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
                return current;
            });
            List<NativeBattleGfGeometryBatch> second = [];
            ImmutableArray<byte> restoredMorph = before;
            NativeBattleGfAnimationAdvance restored = clone.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleRinoa87NodeDraw drawn = NativeBattleRinoa87ProgramDrawing.Draw7766B0(source, new(view, node, task, 0x25B46A8), restoredMorph, projection, colors);
                restoredMorph = drawn.MorphScratch;
                second.Add(drawn.Packets);
                return current;
            });
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(restored));
            Assert.Equal(NativeBattleSnapshotJson.Serialize(first), NativeBattleSnapshotJson.Serialize(second));
            Assert.Equal(morph.ToArray(), restoredMorph.ToArray());
            workspace = next.Workspace;
            remaining = next.Remaining;
        }
        Assert.Equal(0, remaining);
        Assert.True(nodes > 0);
        Assert.True(packets > 0);
    }
}
