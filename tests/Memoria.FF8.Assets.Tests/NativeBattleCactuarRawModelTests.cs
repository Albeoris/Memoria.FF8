using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCactuarRawModelTests
{
    [Fact]
    public void AllFiveOriginalCompressedPosesPreserveExactCursorsAndIndependentNextStepThroughHoldAndLoopCompletion()
    {
        using GameAssets game = Installed();
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeCactuar199Resources().Model;
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan());
        Assert.Equal(5, counts.Length);
        NativeBattleCactuarRawModelState state = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), resource, 0x20DFAB8, 1);
        for (short animation = 0; animation < counts.Length; animation++)
        {
            state = NativeBattleCactuarRawModel.Select5A8FB0(state, resource, animation);
            byte[] hold = state.Model.ToArray();
            hold[0] |= 1;
            state = state with { Model = hold.ToImmutableArray() };
            for (int frame = 1; frame <= counts[animation]; frame++)
            {
                Assert.Equal(frame, (int)state.Model[0x72]);
                Assert.Equal(JsonSerializer.Serialize(resource.PoseSource.Sample((byte)animation, (byte)frame, 0)), JsonSerializer.Serialize(state.DecodedPose));
                NativeBattleCactuarRawModelState restored = JsonSerializer.Deserialize<NativeBattleCactuarRawModelState>(JsonSerializer.Serialize(state))!;
                NativeBattleCactuarRawModelStep next = NativeBattleCactuarRawModel.Advance5A8FD0(state, resource);
                Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeBattleCactuarRawModel.Advance5A8FD0(restored, resource)));
                Assert.Equal(frame == counts[animation], next.Completed);
                if (next.Completed)
                {
                    Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(next.State));
                }
                state = next.State;
            }
            byte[] loop = state.Model.ToArray();
            loop[0] &= 0xFE;
            NativeBattleCactuarRawModelStep restarted = NativeBattleCactuarRawModel.Advance5A8FD0(state with { Model = loop.ToImmutableArray() }, resource);
            Assert.True(restarted.Completed);
            Assert.Equal((byte)1, restarted.State.Model[0x72]);
            Assert.Equal(JsonSerializer.Serialize(resource.PoseSource.Sample((byte)animation, 1, 0)), JsonSerializer.Serialize(restarted.State.DecodedPose));
            state = restarted.State;
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleCactuarRawModel.Select5A8FB0(state, resource, -1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCactuarRawModel.Select5A8FB0(state, resource, 5));
    }

    [Fact]
    public void Original259VertexBodyWritesExact818ScratchAndCommonPacketsWithoutChangingRawModelOrControl()
    {
        using GameAssets game = Installed();
        NativeBattleEffectOwnedModelResource resource = game.ReadNativeCactuar199Resources().Model;
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        Assert.Equal(259, resource.Geometry[0].Vertices.Length);
        NativeBattleCactuarRawModelState state = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), resource, 0x20DFAB8, 1);
        state = NativeBattleCactuarRawModel.Select5A8FB0(state, resource, 0);
        byte[] model = state.Model.ToArray();
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(model.AsSpan(0x40 + index * 2), index % 4 == 0 ? (short)4096 : (short)0);
        }
        state = state with { Model = model.ToImmutableArray() };
        NativeBattleActionMatrix parent = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8192, 4096);
        NativeBattleCameraProjection projection = new(parent, 216, 160, 108);
        NativeBattleCactuarRawModelDraw draw = NativeBattleCactuarRawModel.Draw5A9010(state, resource, parent, projection, colors);
        Assert.Equal(0x818, draw.ProjectedBytesWritten);
        Assert.Equal(0x818, draw.State.ProjectedScratch!.Value.Length);
        Assert.True(draw.Packets.AllocationBytes > 0);
        Assert.Equal(state.Model.ToArray(), draw.State.Model.ToArray());
        Assert.Equal(state.Control.ToArray(), draw.State.Control.ToArray());
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), draw.Packets);
        Assert.Equal(draw.Packets.AllocationBytes, written.Process.ByteCursor);
        Assert.NotEmpty(written.Ordering.Proxies);
        NativeBattleCactuarRawModelState restored = JsonSerializer.Deserialize<NativeBattleCactuarRawModelState>(JsonSerializer.Serialize(draw.State))!;
        Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(NativeBattleCactuarRawModel.Draw5A9010(restored, resource, parent, projection, colors)));
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(0x7C), 0);
        NativeBattleCactuarRawModelDraw masked = NativeBattleCactuarRawModel.Draw5A9010(draw.State with { Model = model.ToImmutableArray() }, resource, parent, projection, colors);
        Assert.Equal(0, masked.ProjectedBytesWritten);
        Assert.Empty(masked.Packets.Attempts);
        Assert.Equal(draw.State.ProjectedScratch.Value.ToArray(), masked.State.ProjectedScratch!.Value.ToArray());
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
}
