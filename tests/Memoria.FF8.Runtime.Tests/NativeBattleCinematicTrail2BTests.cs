using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicTrail2BTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Samples_with_three_literal_CRT_draws_and_emits_native_flat_or_Gouraud_ribbon_masks(bool reverse, bool flat)
    {
        NativeBattleWaterExecutionFrame initial = Fixture(reverse);
        int projected = 0;
        int packets = 0;
        int setup = 0;
        NativeBattleCinematicTrail2BServices services = new((frame, texture) =>
        {
            setup++;
            Assert.Equal(-2, texture);
            return frame.WriteTemporaryDword(0xFC, 0x1234);
        }, (_, texture) =>
        {
            setup++;
            Assert.Equal(-2, texture);
            return 0xABCD;
        }, (_, shape) =>
        {
            setup++;
            Assert.True(shape is 1 or 2);
            return Address(shape == 1 ? 0x3100 : 0x3300);
        }, frame => frame, (frame, point, output) =>
        {
            int constructionIndex = 5 - projected;
            int sourceIndex = reverse ? projected : constructionIndex;
            Assert.Equal(sourceIndex * 10, Word(frame, point));
            Assert.Equal(sourceIndex * 10 + 144 + constructionIndex / 2, Word(frame, point + 2));
            Assert.Equal(sourceIndex * 10 + 49, Word(frame, point + 4));
            Assert.Equal(32, Word(frame, point + 6));
            frame = Dword(frame, output, unchecked((uint)(projected * 10)));
            frame = SetWord(frame, output + 4, 20);
            frame = SetWord(frame, output + 0x10, 3);
            projected++;
            return frame;
        }, (frame, command, factor) => new(frame, flat ? 0x111111u : (uint)factor * 0x010101u), (frame, output, ordering, batch) =>
        {
            Assert.Equal(6, projected);
            int size = flat ? 40 : 52;
            Assert.Equal(Address(0x6000 + packets * size), output);
            Assert.Equal(0x1014u, ordering);
            Assert.Equal(size, batch.AllocationBytes);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            Assert.Equal(flat ? 33 : 45, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            Assert.Equal(new int[] { 0, 0, 0, 0 }, attempt.Primitive!.VertexPayload4);
            Assert.Equal(flat ? 9 : 12, attempt.Data[3]);
            Assert.Equal(0xABCDu, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(14)));
            Assert.Equal(0x1234u, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(flat ? 22 : 26)));
            if (packets == 0)
            {
                Assert.Equal(0x00030000u, attempt.Primitive.Words[2]);
                Assert.Equal(0xFFFD0000u, attempt.Primitive.Words[flat ? 4 : 5]);
                Assert.Equal(192, attempt.Data[13]);
                Assert.Equal(255, attempt.Data[flat ? 29 : 37]);
            }
            if (packets == 1)
            {
                Assert.Equal(0, attempt.Data[13]);
                Assert.Equal(64, attempt.Data[flat ? 29 : 37]);
            }
            packets++;
            return frame;
        });
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(initial))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicTrail2B.Draw(initial, services);
        Assert.Equal(4, setup);
        Assert.Equal(5, packets);
        Assert.Equal(415139642u, result.Random.Seed);
        Assert.Equal(Address(0x4000), WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        Assert.Equal(Address(0x4050), WaterSchedulerFixture.ReadCoreDword(result, 0x2797690 + 0x74));
        Assert.Equal(5u, result.ReadTemporaryDword(0x70));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x4056, 2));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x3146, 2));
        projected = 0;
        packets = 0;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicTrail2B.Draw(restored, services)));
    }

    [Theory]
    [InlineData(0, 2048, 1024, 1024)]
    [InlineData(4090, 10, 0, 2)]
    [InlineData(100, 300, 200, 200)]
    [InlineData(100, 300, 2248, 2248)]
    public void Joint_angle_uses_short_arc_and_preserves_previous_edge_side(int first, int second, int previous, int expected)
    {
        Assert.Equal(expected, NativeBattleCinematicTrail2B.JoinAngle(first, second, previous));
    }

    [Fact]
    public void Effect258_capture_preserves_native_centerline_packets_random_and_zero_payload_during_sampling()
    {
        var native = CaptureRun(false);
        string baseline = NativeBattleTestSnapshots.Json(native.Frame);
        string[] nativePackets = native.Batches.Select(batch => NativeBattleTestSnapshots.Json(batch)).ToArray();
        var captured = CaptureRun(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativePackets, captured.Batches.Select(batch => NativeBattleTestSnapshots.Json(batch)));
        Assert.Equal(native.Frame.Random, captured.Frame.Random);
        Assert.Equal(5, captured.Batches.Count);
        foreach (NativeBattleGfGeometryBatch batch in captured.Batches)
        {
            NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
            Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal((ushort)258, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
            Assert.Equal(4, geometry.ScreenOffsets.Length);
            Assert.Equal(geometry.A, geometry.B);
            Assert.Equal(geometry.C, geometry.D);
            string beforeSampling = NativeBattleTestSnapshots.Json(captured.Frame);
            NativeBattlePilotVisualGeometry sampled = NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = geometry.A + System.Numerics.Vector3.One }, 0.5f);
            Assert.Equal(System.Numerics.Vector4.Zero, sampled.SourceDepths);
            Assert.True(sampled.UseSourceDepths);
            Assert.Equal(beforeSampling, NativeBattleTestSnapshots.Json(captured.Frame));
        }
        Assert.Equal(captured.Batches.Count, captured.Batches.Select(batch => Assert.Single(batch.Attempts).Primitive!.PilotGeometry!.Identity.Primitive).Distinct().Count());
        var otherCaller = CaptureRun(true, 77);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(otherCaller.Frame));
        Assert.All(otherCaller.Batches, batch =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.Single(batch.Attempts).Primitive!.PilotGeometry!;
            Assert.False(geometry.UseSourceDepths);
            Assert.Null(geometry.SourceDepths);
        });
    }

    private static (NativeBattleWaterExecutionFrame Frame, List<NativeBattleGfGeometryBatch> Batches) CaptureRun(bool capture, ushort effect = 258)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(false);
        List<NativeBattleGfGeometryBatch> batches = [];
        Dictionary<uint, NativeBattleCinematicTrail2BSample> samples = [];
        int projected = 0;
        NativeBattleCinematicTrail2BServices services = new((current, _) => current.WriteTemporaryDword(0xFC, 0x1234), (_, _) => 0xABCD, (_, shape) => Address(shape == 1 ? 0x3100 : 0x3300), current => current, (current, point, output) =>
        {
            if (capture)
            {
                samples.Add(output, new(new(Word(current, point), Word(current, point + 2), Word(current, point + 4)), point));
            }
            current = Dword(current, output, unchecked((uint)(projected * 10)));
            current = SetWord(current, output + 4, 20);
            current = SetWord(current, output + 0x10, 3);
            projected++;
            return current;
        }, (current, _, _) => new(current, 0x111111), (current, _, _, batch) =>
        {
            batches.Add(batch);
            return current;
        });
        if (capture)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, Address(0x2000), 0);
            services = services with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(effect, frame.Arena, Address(0x2000), 0), PilotSamples = samples };
        }
        return (NativeBattleCinematicTrail2B.Draw(frame, services), batches);
    }

    private static NativeBattleWaterExecutionFrame Fixture(bool reverse)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000).WriteTemporaryDword(0x70, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, Address(0x6000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x808080);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 5);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 3);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 3);
        short[] payload = [-2, 6, 1, 2, 32, 64, 64, reverse ? (short)1 : (short)0];
        for (int index = 0; index < payload.Length; index++)
        {
            frame = SetWord(frame, Address(0x3000 + index * 2), payload[index]);
        }
        for (int shape = 0; shape < 2; shape++)
        {
            uint basis = Address(0x3100 + shape * 0x200);
            frame = Dword(frame, basis + 0x14, 0x40);
            frame = SetWord(frame, basis + 0x18, 6);
            for (int index = 0; index < 6; index++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    frame = SetWord(frame, basis + 0x40 + (uint)(index * 8 + lane * 2), (short)(index * 10 + shape * 256));
                }
            }
        }
        return frame with { Arena = frame.Arena.MarkUnknown(0x4056, 2).MarkUnknown(0x3146, 2) };
    }

    private static short Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(checked((int)(address - Address(0))), 2));
    private static NativeBattleWaterExecutionFrame SetWord(NativeBattleWaterExecutionFrame frame, uint address, short value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Address(0))), BitConverter.GetBytes(value)) };
    private static NativeBattleWaterExecutionFrame Dword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Address(0))), BitConverter.GetBytes(value)) };
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
}
