using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup18AliasPresentationTests
{
    [Fact]
    public void Ribbon229_preserves_native_packets_scratch_rng_and_exact_zero_payload_depth()
    {
        var nativeFrame = Fixture();
        var capturedFrame = Fixture();
        var nativePackets = new List<NativeBattleGfGeometryBatch>();
        var capturedPackets = new List<NativeBattleGfGeometryBatch>();
        var native = NativeBattleCinematicRibbon37.Draw(nativeFrame, CaptureServices(nativePackets));
        string baseline = NativeBattleTestSnapshots.Json(native);
        var identity = NativeBattlePilotVisualLifetimes.Identity(229, capturedFrame.Arena, Address(0x2000), 0);
        var captured = NativeBattleCinematicRibbon37.Draw(capturedFrame, CaptureServices(capturedPackets), identity);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativePackets), NativeBattleTestSnapshots.Json(capturedPackets));
        Assert.Equal(8, capturedPackets.Count);
        Assert.All(nativePackets, batch => Assert.Null(Assert.Single(batch.Attempts).Primitive!.PilotGeometry));
        foreach (var batch in capturedPackets)
        {
            var primitive = Assert.Single(batch.Attempts).Primitive!;
            var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(identity.Bank, geometry.Identity.Bank);
            Assert.Equal(identity.Slot, geometry.Identity.Slot);
            Assert.Equal((ushort)229, geometry.Identity.Effect);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(Vector4.Zero, geometry.SourceDepths!.Value);
            Assert.Equal(0, geometry.SampleDepth(0));
            Assert.NotEqual(Vector3.Zero, geometry.A);
            Assert.Equal(4, primitive.VertexPayload4.Length);
            Assert.All(primitive.VertexPayload4, depth => Assert.Equal(0, depth));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Ribbon229_either_actual_endpoint_rebirth_breaks_identity_without_changing_native_state(int endpoint)
    {
        var frame = Fixture();
        var batches = new List<NativeBattleGfGeometryBatch>();
        var identity = NativeBattlePilotVisualLifetimes.Identity(229, frame.Arena, Address(0x2000), 0);
        frame = NativeBattleCinematicRibbon37.Draw(frame, CaptureServices(batches), identity);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = frame.WriteTemporaryByte(0x145, 1);
        batches.Clear();
        frame = NativeBattleCinematicRibbon37.Draw(frame, CaptureServices(batches), identity);
        var before = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batches[0].Attempts).Primitive!.PilotGeometry);
        string nativeBefore = NativeBattleTestSnapshots.Json(frame);
        NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, Address(0x4020), endpoint);
        Assert.Equal(nativeBefore, NativeBattleTestSnapshots.Json(frame));
        batches.Clear();
        frame = NativeBattleCinematicRibbon37.Draw(frame, CaptureServices(batches), identity);
        var after = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batches[0].Attempts).Primitive!.PilotGeometry);
        Assert.NotEqual(before.Identity, after.Identity);
        Assert.Equal(before.A, after.A);
        Assert.Equal(before.B, after.B);
        Assert.Equal(before.C, after.C);
        Assert.Equal(before.D, after.D);
        Assert.Equal(before.Identity.Generation, after.Identity.Generation);
    }

    [Fact]
    public void Ribbon229_owner_recycle_is_distinct_and_other_effect_default_capture_is_unchanged()
    {
        var frame = Fixture();
        var batches = new List<NativeBattleGfGeometryBatch>();
        var identity = NativeBattlePilotVisualLifetimes.Identity(229, frame.Arena, Address(0x2000), 0);
        frame = NativeBattleCinematicRibbon37.Draw(frame, CaptureServices(batches), identity);
        var first = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batches[0].Attempts).Primitive!.PilotGeometry);
        NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, identity.Bank, identity.Slot);
        Assert.NotEqual(first.Identity.Generation, NativeBattlePilotVisualLifetimes.Identity(229, frame.Arena, identity.Bank, identity.Slot).Generation);
        var otherFrame = Fixture();
        batches.Clear();
        NativeBattleCinematicRibbon37.Draw(otherFrame, CaptureServices(batches), new(228, Address(0x2000), 0, 99, 0));
        var other = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(batches[0].Attempts).Primitive!.PilotGeometry);
        Assert.False(other.UseSourceDepths);
        Assert.Null(other.SourceDepths);
        Assert.Equal(Address(0x4020), other.Identity.Bank);
    }

    private static NativeBattleCinematicRibbon37Services CaptureServices(List<NativeBattleGfGeometryBatch> batches)
    {
        return Services((current, _) => current, (current, _, address) => new(current, 0, 4000, 0) { PilotWorld = new Vector3(address & 255, 10, 3000) }, (current, _, _, batch) =>
        {
            batches.Add(batch);
            return current;
        });
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
