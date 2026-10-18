using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzagaControllerTests
{
    [Fact]
    public void WholeCastUsesSixPhysicalBanksCapacitySkips2667RandomDrawsAnd100Callbacks()
    {
        var fixture = Fixture();
        NativeBattleBlizzagaFrame frame = fixture.Frame;
        List<(int Callback, NativeBattleBlizzagaOperation Operation)> operations = [];
        List<NativeBattleBlizzagaModelBandRequest> bands = [];
        int ribbonPackets = 0;
        for (int callback = 1; callback <= 100; callback++)
        {
            int current = callback;
            NativeBattleBlizzagaInputs inputs = Inputs(fixture.Packets, (value, operation) =>
            {
                operations.Add((current, operation));
                return value;
            }, request => bands.Add(request), count => ribbonPackets += count);
            frame = NativeBattleBlizzagaController.Advance6D6400(frame, inputs);
            Assert.Equal((callback & 1) != 0 ? 0x28928 : 0x18928, frame.Effect.DrawArenaOffset);
            if (callback == 1)
            {
                Assert.Single(frame.Effect.Pools);
            }
            if (callback == 2)
            {
                Assert.Equal(new ushort[] { 20, 0x6428, 0x478, 0x464, 0x2C, 0x28, 0x2C }, frame.Effect.Pools.Select(pool => pool.Stride));
                Assert.Equal(new[] { 1, 2, 6, 4, 96, 64, 96 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
                Assert.Equal((ushort)1, frame.Effect.Pools[1].Slots[0].ReadWord(12));
                Assert.Equal((ushort)123, frame.Effect.Pools[1].Slots[0].ReadWord(34));
                Assert.Equal(1u, Dword(frame.Effect.Pools[2].Slots[0], 24));
                Assert.Equal(1u, Dword(frame.Effect.Pools[3].Slots[0], 32));
                Assert.Equal((ushort)1, frame.Effect.Pools[2].Slots[0].ReadWord(12));
            }
            if (callback < 100)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
            }
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(2, Assert.Single(operations, item => item.Operation is NativeBattleBlizzagaSound).Callback);
        Assert.Equal(70, Assert.Single(operations, item => item.Operation is NativeBattleBlizzagaContact).Callback);
        Assert.Equal(70, operations.Count(item => item.Operation is NativeBattleBlizzagaMainDraw));
        Assert.Equal(25, operations.Count(item => item.Operation is NativeBattleBlizzagaAuxiliaryDraw { First: true }));
        Assert.Equal(10, operations.Count(item => item.Operation is NativeBattleBlizzagaAuxiliaryDraw { First: false }));
        Assert.Equal(102 * 10, operations.Count(item => item.Operation is NativeBattleBlizzagaSpark));
        Assert.Equal(200 * 10, operations.Count(item => item.Operation is NativeBattleBlizzagaSprite { Task.Callback: 0x6D95E0 }));
        Assert.Equal(112 * 14, operations.Count(item => item.Operation is NativeBattleBlizzagaSprite { Task.Callback: 0x6D96A0 }));
        Assert.Equal(61, bands.Count(request => request.Enabled));
        Assert.True(ribbonPackets > 0);
        Assert.Equal(new short[] { 768, 512, 256, 0 }, operations.Where(item => item.Callback >= 97 && item.Operation is NativeBattleBlizzagaStage).Select(item => ((NativeBattleBlizzagaStage)item.Operation).Value));
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 2667; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
    }

    [Fact]
    public void PauseBitsDoNotGateAndMidFractureSnapshotOwnsExactRibbonAndFaceContinuation()
    {
        var fixture = Fixture();
        NativeBattleBlizzagaFrame normal = fixture.Frame;
        NativeBattleBlizzagaFrame flagged = fixture.Frame;
        for (int callback = 1; callback <= 100; callback++)
        {
            normal = NativeBattleBlizzagaController.Advance6D6400(normal, Inputs(fixture.Packets));
            flagged = NativeBattleBlizzagaController.Advance6D6400(flagged, Inputs(fixture.Packets) with { GraphicsFlags = 0x201 });
            Assert.Equal(JsonSerializer.Serialize(normal), JsonSerializer.Serialize(flagged));
            if (callback is 34 or 55)
            {
                flagged = JsonSerializer.Deserialize<NativeBattleBlizzagaFrame>(JsonSerializer.Serialize(normal))!;
                Assert.NotSame(normal.Effect.Pools[1].Slots[0], flagged.Effect.Pools[1].Slots[0]);
            }
        }
    }

    [Fact]
    public void DuplicateTargetBlocksUntilPreviousPrimaryReleaseThenRetriesImmediately()
    {
        var fixture = Fixture(2);
        NativeBattleBlizzagaFrame frame = fixture.Frame;
        List<int> births = [];
        for (int callback = 1; callback <= 199; callback++)
        {
            byte before = frame.Effect.Pools[0].Slots[0].ReadByte(14);
            frame = NativeBattleBlizzagaController.Advance6D6400(frame, Inputs(fixture.Packets));
            if (before != frame.Effect.Pools[0].Slots[0].ReadByte(14))
            {
                births.Add(callback);
            }
            if (callback == 100)
            {
                Assert.NotEqual(0u, frame.Effect.ReturnedDescriptor);
                Assert.Equal((ushort)1, frame.Effect.Pools[0].Slots[0].ReadWord(12));
                Assert.Null(frame.Effect.Pools[1].Head);
            }
        }
        Assert.Equal(new[] { 2, 101 }, births);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void ActualWrapperCloneBoundsAndRetainedInactiveParentMatrixAreStrict()
    {
        var fixture = Fixture();
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaController.Initialize6D6320(fixture.Frame.Effect with { Binding = fixture.Frame.Effect.Binding with { Entry = 0x6D6320 } }, fixture.Packets));
        Assert.Throws<NotSupportedException>(() => Fixture(4));
        Assert.Equal(1, NativeBattleBlizzagaController.PrimarySlot(NativeBattleBlizzagaController.Arena + 0x6428));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzagaController.PrimarySlot(NativeBattleBlizzagaController.Arena + 2 * 0x6428));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleBlizzagaController.SecondDescriptor, NativeBattleBlizzagaController.SecondBank, 0x478, 6);
        NativeBattleModulePoolTask retained = pool.Slots[5].WriteWord(32, 4096).WriteWord(40, 4096).WriteWord(48, 4096);
        pool = pool with { Slots = pool.Slots.SetItem(5, retained) };
        Assert.Equal((short)4096, NativeBattleBlizzagaController.ReadSecondMatrix(pool, NativeBattleBlizzagaController.SecondBank + 5 * 0x478 + 32).Rotation[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzagaController.ReadSecondMatrix(pool, NativeBattleBlizzagaController.SecondBank + 5 * 0x478 + 33));
    }

    [Fact]
    public void ModelBandThresholdsUseReturnedPriorBoundsAndFullMacMatrixIsRetained()
    {
        var fixture = Fixture();
        NativeBattleBlizzagaFrame frame = fixture.Frame;
        List<NativeBattleBlizzagaModelBandRequest> requests = [];
        for (int callback = 1; callback <= 12; callback++)
        {
            frame = NativeBattleBlizzagaController.Advance6D6400(frame, Inputs(fixture.Packets, bands: request => requests.Add(request)) with { View = View with { Y = 50000 } });
        }
        Assert.Equal((short)200, requests.Single(request => request.Phase == 9).HighY);
        Assert.Equal((short)184, requests.Single(request => request.Phase == 9).LowY);
        NativeBattleActionMatrix retained = NativeBattleBlizzagaController.ReadSecondMatrix(frame.Effect.Pools[2], NativeBattleBlizzagaController.SecondBank + 32);
        Assert.Equal(50123, retained.Y);
        Assert.True(requests.Single(request => request.Phase == 8).BandEnabled);
        Assert.False(requests.Single(request => request.Phase == 9).BandEnabled);
    }

    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12, 4));
    private static NativeBattleBlizzagaInputs Inputs(NativeBattleActionPacketsState packets, Func<NativeBattleBlizzagaFrame, NativeBattleBlizzagaOperation, NativeBattleBlizzagaFrame>? execute = null, Action<NativeBattleBlizzagaModelBandRequest>? bands = null, Action<int>? ribbonPackets = null) => new(0, packets, 0, View, actor => new(unchecked((short)(100 + actor * 20)), -200, 300), _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), actor => new(10, 20, 30, (short)actor), (frame, request) =>
    {
        bands?.Invoke(request);
        return new(frame, -100, 200);
    }, (frame, task, second, delta, matrix) =>
    {
        int countOffset = second ? 24 : 32;
        int nodesOffset = second ? 64 : 44;
        NativeBattleBlizzagaRibbonResult result = NativeBattleBlizzagaRibbon.Emit6D9A40((int)Dword(task, countOffset), task.Payload.AsSpan().Slice(nodesOffset - 12, 30 * 36).ToArray().ToImmutableArray(), delta, matrix, new(null, 216, 160, 108));
        ribbonPackets?.Invoke(result.Packets.AllocationBytes);
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(countOffset - 12), (uint)result.Count);
        result.Nodes.AsSpan().CopyTo(payload.AsSpan(nodesOffset - 12));
        return new(frame, task with { Payload = payload.ToImmutableArray() });
    }, execute ?? ((frame, _) => frame));

    private static (NativeBattleBlizzagaFrame Frame, NativeBattleActionPacketsState Packets) Fixture(int headers = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        for (int index = 0; index < headers; index++)
        {
            (packets, _) = packets.Prepare(new(0, 2, 4, 0, 8, 104, null));
            (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
        }
        packets = packets.WritePlayerRepeatMode(0, (byte)headers);
        NativeBattleOriginalEffectState effect = new(104, 103, new(104, 0x6D6310, 0x6D62F0) { BlizzagaResources = NativeBattleBlizzagaFaceTests.Resources() }, new(0, 0, 8, 0, 0, 0), null);
        effect = NativeBattleBlizzagaController.Initialize6D6320(effect, packets);
        return (new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1)), packets);
    }
}
