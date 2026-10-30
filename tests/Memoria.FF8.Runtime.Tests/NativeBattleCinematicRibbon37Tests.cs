using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicRibbon37Tests
{
    [Fact]
    public void InitialRingProjectsQuarterDepthThenRawDepthAndEmitsEightOrderedFaces()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        int projections = 0;
        int inputs = 0;
        int packets = 0;
        NativeBattleCinematicRibbon37Services services = Services((current, address) =>
        {
            inputs++;
            Assert.Equal(current.TemporaryFrame.Address + 0xF0, address);
            return current;
        }, (current, _, _) => new(current, unchecked((uint)(0x10000 + projections)), unchecked((uint)(4000 + projections++ * 4)), 0x12345678), (current, output, ordering, batch) =>
        {
            Assert.Equal(Address(0x5000 + packets * 44), output);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            Assert.Equal(44, batch.AllocationBytes);
            Assert.Equal(41, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            Assert.Equal(0xE1000000u, primitive.Words[1]);
            Assert.Equal(0u, primitive.Words[2]);
            if (packets == 0)
            {
                Assert.Equal(0x1274u, ordering);
                Assert.Equal(0x10001u, primitive.Words[4]);
                Assert.Equal(0x10003u, primitive.Words[6]);
                Assert.Equal(0x1000Au, primitive.Words[8]);
                Assert.Equal(0x1000Cu, primitive.Words[10]);
            }
            packets++;
            return current;
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicRibbon37.Draw(frame, services);
        Assert.Equal(2, inputs);
        Assert.Equal(18, projections);
        Assert.Equal(8, packets);
        Assert.Equal(Address(0x40B0), WaterSchedulerFixture.ReadCoreDword(result, 0x2797704));
        Assert.Equal(Address(0x5160), WaterSchedulerFixture.ReadCoreDword(result, 0x279770C));
        Assert.Equal((ushort)1, ArenaWord(result, 0x4000));
        Assert.Equal(1000u, ArenaDword(result, 0x40B4));
        Assert.Equal((ushort)4036, ArenaWord(result, 0x4134));
        Assert.Equal(0x12345678u, result.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void NegativeSpeedAgesWithoutAdvancingRingAndFinishesBeforeRenderingAtCapacity()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        int projections = 0;
        int packets = 0;
        NativeBattleCinematicRibbon37Services services = Services((current, _) => current, (current, _, _) =>
        {
            projections++;
            return new(current, 0, 4000, 0);
        }, (current, _, _, _) =>
        {
            packets++;
            return current;
        });
        frame = NativeBattleCinematicRibbon37.Draw(frame, services);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, unchecked((ushort)-1));
        frame = NativeBattleCinematicRibbon37.Draw(frame, services);
        Assert.Equal(27, projections);
        Assert.Equal(8, packets);
        Assert.Equal((ushort)1, ArenaWord(frame, 0x4000));
        Assert.Equal((ushort)1, ArenaWord(frame, 0x400E));
        frame = NativeBattleCinematicRibbon37.Draw(frame, services);
        Assert.Equal(27, projections);
        Assert.Equal(8, packets);
        Assert.Equal((ushort)2, ArenaWord(frame, 0x400E));
    }

    [Fact]
    public void Selected219_ribbon_keeps_parent_and_both_physical_segment_births_without_native_changes()
    {
        (NativeBattleWaterExecutionFrame Frame, List<NativeBattleGfGeometryBatch> Batches) Run(bool capture)
        {
            List<NativeBattleGfGeometryBatch> batches = [];
            int count = 0;
            NativeBattleCinematicRibbon37Services services = Services((current, _) => current, (current, matrix, address) =>
            {
                NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(current.Arena.Read(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), 6));
                return new(current, unchecked((uint)(0x10000 + count)), unchecked((uint)(4000 + count++ * 4)), 0x12345678) { PilotWorld = NativeBattlePilotVisualGeometry.Transform(matrix, point) };
            }, (current, _, _, batch) =>
            {
                batches.Add(batch);
                return current;
            });
            return (NativeBattleCinematicRibbon37.Draw(Fixture(), services, capture ? new(219, 1, 2, 3, 4) : null), batches);
        }
        var native = Run(false);
        string frozen = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(true);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batches), NativeBattleTestSnapshots.Json(captured.Batches));
        Assert.Equal(8, captured.Batches.Count);
        foreach (NativeBattleGfGeometryBatch batch in captured.Batches)
        {
            NativeBattlePilotVisualGeometry visual = Assert.Single(batch.Attempts).Primitive!.PilotGeometry!;
            Assert.Equal((ushort)219, visual.Identity.Effect);
            Assert.Equal(1u, visual.Identity.Bank);
            Assert.Equal(2, visual.Identity.Slot);
            Assert.Equal(3L, visual.Identity.Generation);
            Assert.NotEqual(0, visual.Identity.ResourceGeneration);
            Assert.True(visual.UseSourceDepths);
            Assert.Equal(System.Numerics.Vector4.Zero, visual.SourceDepths);
        }
    }

    private static NativeBattleCinematicRibbon37Services Services(Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> publish, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, uint, NativeBattleCinematicRibbonProjection> project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> append)
        => new((_, _) => Address(0x2100), publish, (current, first, second, output) => NativeBattleCinematicRibbonWork.BuildColorRamp(current, NativeBattleScriptedColorRegisters.Unknown(), first, second, output).Frame, current => new(current, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096)), project, append);

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Address(0x4000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279770C, Address(0x5000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Address(0x3000));
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 16);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 4);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 100);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 200);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 300);
        byte[] payload = new byte[18];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), 2);
        return frame with { Arena = frame.Arena.Write(0x3000, payload) };
    }

    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static ushort ArenaWord(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
    private static uint ArenaDword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
}
