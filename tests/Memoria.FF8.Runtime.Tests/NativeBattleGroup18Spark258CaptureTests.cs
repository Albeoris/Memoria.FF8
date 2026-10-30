using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup18Spark258CaptureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Native_ring_births_packet_depth_and_random_survive_capture_and_sampling(ushort mode)
    {
        var native = Run(mode, null);
        var captured = Run(mode, 258);
        Assert.Equal(native.Frames, captured.Frames);
        Assert.Equal(native.Batches.SelectMany(draw => draw).Select(batch => NativeBattleTestSnapshots.Json(batch)), captured.Batches.SelectMany(draw => draw).Select(batch => NativeBattleTestSnapshots.Json(batch)));
        Assert.Equal(new[] { 1, 2, 2 }, captured.Batches.Select(draw => draw.Count));
        NativeBattlePilotVisualGeometry first = Geometry(captured.Batches[0][0]);
        NativeBattlePilotVisualGeometry retained = Geometry(captured.Batches[1][0]);
        NativeBattlePilotVisualGeometry recycled = Geometry(captured.Batches[2][0]);
        Assert.Equal(first.Identity, retained.Identity);
        Assert.NotEqual(first.Identity.Generation, recycled.Identity.Generation);
        Assert.Same(first, NativeBattlePilotVisualGeometry.Sample(first, recycled, 0.5f));
        foreach (NativeBattleGfGeometryBatch batch in captured.Batches.SelectMany(draw => draw))
        {
            NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
            Assert.Empty(primitive.VertexPayload4);
            NativeBattlePilotVisualGeometry geometry = Geometry(batch);
            Assert.Equal((ushort)258, geometry.Identity.Effect);
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x4010, geometry.Identity.Bank);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(Vector4.Zero, geometry.SourceDepths);
            Assert.Equal(4, geometry.ScreenOffsets.Length);
            Assert.Equal(new Vector2(8, 8), geometry.ScreenOffsets[3]);
        }
        string baseline = NativeBattleTestSnapshots.Json(captured.Frame);
        _ = NativeBattlePilotVisualGeometry.Sample(first, retained, 0.5f);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Frame));
        var otherCaller = Run(mode, 77);
        Assert.Equal(native.Frames, otherCaller.Frames);
        Assert.All(otherCaller.Batches.SelectMany(draw => draw), batch =>
        {
            Assert.False(Geometry(batch).UseSourceDepths);
            Assert.Null(Geometry(batch).SourceDepths);
        });
    }

    private static NativeBattlePilotVisualGeometry Geometry(NativeBattleGfGeometryBatch batch) => Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batch.Attempts).Primitive!.PilotGeometry);

    private static (NativeBattleWaterExecutionFrame Frame, List<string> Frames, List<List<NativeBattleGfGeometryBatch>> Batches) Run(ushort mode, ushort? captureEffect)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, Address(0x6000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 200);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 300);
        byte[] payload = new byte[34];
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0x100);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), mode);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), 2);
        payload[8] = 3;
        frame = frame with { Arena = frame.Arena.Write(0x3000, payload).Write(0x2494, BitConverter.GetBytes((short)1)) };
        for (int index = 0; index < 4; index++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x3100 + index * 4, BitConverter.GetBytes(0x00102030u + (uint)index)) };
        }
        List<string> snapshots = [];
        List<List<NativeBattleGfGeometryBatch>> draws = [];
        for (int draw = 0; draw < 3; draw++)
        {
            List<NativeBattleGfGeometryBatch> batches = [];
            NativeBattleCinematicSpark53Services services = new((_, _) => Address(0x2400), current => current, current =>
            {
                short x = unchecked((short)current.ReadTemporaryWord(0xF0));
                short y = unchecked((short)current.ReadTemporaryWord(0xF2));
                short z = unchecked((short)current.ReadTemporaryWord(0xF4));
                return new(current, unchecked((ushort)x | ((uint)(ushort)y << 16)), z / 4) { PilotWorld = captureEffect.HasValue ? new Vector3(x, y, z) : null };
            }, (current, _, _, batch) =>
            {
                batches.Add(batch);
                return current;
            });
            NativeBattlePilotVisualIdentity? identity = captureEffect is ushort effect ? NativeBattlePilotVisualLifetimes.Identity(effect, frame.Arena, Address(0x2000), 0) : null;
            frame = NativeBattleCinematicSpark53.Draw(frame, services, pilotIdentity: identity);
            snapshots.Add(NativeBattleTestSnapshots.Json(frame));
            draws.Add(batches);
        }
        return (frame, snapshots, draws);
    }

    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + checked((uint)offset);
}
