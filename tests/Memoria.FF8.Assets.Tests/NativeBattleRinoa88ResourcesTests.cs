using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRinoa88ResourcesTests
{
    [Fact]
    public void Original_model_override_IO_and_node_owners_have_exact_extents()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa88Resources resources = game.ReadNativeRinoa88Resources();
        Assert.Equal(34, resources.PrimaryModel.PoseSource.BoneCount);
        Assert.Equal(2, resources.SecondaryModel.PoseSource.BoneCount);
        Assert.Equal(23840, resources.PrimaryModel.Bytes.Length);
        Assert.Equal(1828, resources.SecondaryModel.Bytes.Length);
        Assert.Equal(7148, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 8356, 10912, 7984, 12944, 33824, 33824 }, resources.Io.Files.Select(file => file.Data.Length));
        resources.Nodes.Validate();
        resources.Camera.Validate();
        Assert.Equal(new byte[] { 0, 0, 4, 4 }, new[] { 0x1B44, 0x1C9C, 0x1DF4, 0x1F4C }.Select(offset => resources.Nodes.Data[offset + 22]));
        NativeBattleRinoa88IoState io = NativeBattleRinoa88Io.Initialize7689C0(resources.Io.Script.Data, 0x20FFAB8);
        Assert.Equal(0x19F3A8Cu, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(296)));
        Assert.Equal(0x19EF0BCu, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(324)));
        Assert.Equal(0x19F1B5Cu, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(328)));
        Assert.Equal(0xFF000000u, resources.ScreenColors[^1]);
    }

    [Theory]
    [InlineData(0, 176)]
    [InlineData(1, 964)]
    public void Original_programs_execute_all_reached_nodes_shapes_and_packets_with_independent_next_restore(int program, int clearBytes)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa88Resources resources = game.ReadNativeRinoa88Resources();
        NativeBattleGfAnimationSource source = program == 0 ? resources.Program : new(0x19F3A8C, resources.Io.Files[3].Data);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, 992).ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, clearBytes);
        ImmutableArray<byte> morph = Enumerable.Repeat((byte)0xA5, 1440).ToImmutableArray();
        NativeBattleModulePoolTask task = new(0, 0, null, 0x772C50, new byte[1140 - 12].ToImmutableArray());
        task = task.WriteWord(128, program == 0 ? (ushort)0 : (ushort)1).WriteWord(142, 126);
        int nodes = 0;
        int packets = 0;
        int remaining = -1;
        for (int call = 0; call < 256 && remaining != 0; call++)
        {
            NativeBattleGfAnimationWorkspace clone = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            ImmutableArray<byte> before = morph;
            List<NativeBattleGfGeometryBatch> first = [];
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleRinoa88NodeDraw drawn = NativeBattleRinoa88ProgramDrawing.Draw76A380(source, new(view, node, task, 0x25AC750), morph, projection, colors);
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
                NativeBattleRinoa88NodeDraw drawn = NativeBattleRinoa88ProgramDrawing.Draw76A380(source, new(view, node, task, 0x25AC750), restoredMorph, projection, colors);
                restoredMorph = drawn.MorphScratch;
                second.Add(drawn.Packets);
                return current;
            });
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored));
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.Equal(morph.ToArray(), restoredMorph.ToArray());
            workspace = next.Workspace;
            remaining = next.Remaining;
        }
        Assert.Equal(0, remaining);
        Assert.True(nodes > 0);
        Assert.True(packets > 0);
    }
}
