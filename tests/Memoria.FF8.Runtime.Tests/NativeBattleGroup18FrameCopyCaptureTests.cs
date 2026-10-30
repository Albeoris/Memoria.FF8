using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup18FrameCopyCaptureTests
{
    [Theory]
    [InlineData(256, 0, false)]
    [InlineData(256, 1, false)]
    [InlineData(256, 0, true)]
    [InlineData(256, 1, true)]
    [InlineData(258, 0, false)]
    [InlineData(258, 1, false)]
    [InlineData(258, 0, true)]
    [InlineData(258, 1, true)]
    public void Capture_and_overlay_preserve_native_packets_masks_cursor_and_frame(ushort effect, byte buffer, bool firstFrame)
    {
        var native = Run(buffer, firstFrame, null);
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string[] nativeBatches = native.Batches.Select(batch => NativeBattleTestSnapshots.Json(batch)).ToArray();
        NativeBattlePilotVisualIdentity identity = new(effect, 0x80402000, 7, 19, 31) { ResourceGeneration = 23 };
        var captured = Run(buffer, firstFrame, identity);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeBatches, captured.Batches.Select(batch => NativeBattleTestSnapshots.Json(batch)));
        Assert.Equal(native.Addresses, captured.Addresses);
        Assert.Equal(native.Orderings, captured.Orderings);
        Assert.Equal(firstFrame ? 7 : 6, captured.Batches.Count);
        Assert.Equal(firstFrame ? 144 : 132, captured.Batches.Sum(batch => batch.AllocationBytes));
        Assert.Equal(firstFrame ? uint.MaxValue : 1u, WaterSchedulerFixture.ReadSlotDword(captured.Frame, 0, 0xC4));
        Assert.Equal(firstFrame ? 1 : 0, captured.BufferReads);
        if (firstFrame)
        {
            Assert.All(captured.Batches, batch => Assert.Null(Assert.Single(batch.Attempts).Primitive!.PilotGeometry));
            return;
        }
        for (int strip = 0; strip < 5; strip++)
        {
            NativeBattleGfGeometryAttempt packet = Assert.Single(captured.Batches[strip].Attempts);
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal(identity with { Primitive = unchecked(identity.Primitive * 397 + strip) }, geometry.Identity);
            Assert.Equal(new Vector3(-12 + strip * 64, 9, 0), geometry.A);
            Assert.Equal(new Vector3(52 + strip * 64, 233, 0), geometry.D);
            Assert.False(geometry.Continuous);
            Assert.True(geometry.ScreenSpace);
            Assert.Equal(8, geometry.ScreenScale);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(Vector4.Zero, geometry.SourceDepths);
            Assert.Empty(primitive.VertexPayload4);
            Assert.Equal(0, primitive.Bucket);
            Assert.Equal(new Vector3(-12 + strip * 64, 9, 0), NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = geometry.A + Vector3.One }, 0.5f).A);
        }
        Assert.Null(Assert.Single(captured.Batches[5].Attempts).Primitive!.PilotGeometry);
        Assert.All(native.Batches.Take(5), batch => Assert.Equal(new NativeBattlePilotVisualIdentity(0, 0, 0, 0, native.Batches.IndexOf(batch)), Assert.Single(batch.Attempts).Primitive!.PilotGeometry!.Identity));
        string samplingBaseline = NativeBattleTestSnapshots.Json(captured.Frame);
        foreach (NativeBattleGfGeometryBatch batch in captured.Batches.Take(5))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.Single(batch.Attempts).Primitive!.PilotGeometry!;
            _ = NativeBattlePilotVisualGeometry.Sample(geometry, geometry, 0.75f);
        }
        Assert.Equal(samplingBaseline, NativeBattleTestSnapshots.Json(captured.Frame));
    }

    [Fact]
    public void Non_grayscale_native_gate_emits_nothing_and_keeps_metadata_out_of_snapshots()
    {
        var native = Run(0, false, null, 0x123456);
        string baseline = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(0, false, new(258, 3, 4, 5, 6), 0x123456);
        Assert.Empty(native.Batches);
        Assert.Empty(captured.Batches);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Frame));
    }

    [Fact]
    public void Recycled_node_and_distinct_effect_owners_do_not_blend_screen_copies()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        uint bank = NativeBattleEffectNodeMemory.ArenaAddress + 0x2000;
        NativeBattlePilotVisualLifetimes.Allocated(arena, bank, 7);
        NativeBattlePilotVisualIdentity first = NativeBattlePilotVisualLifetimes.Identity(258, arena, bank, 7);
        var earlier = Run(0, false, first);
        NativeBattlePilotVisualLifetimes.Allocated(arena, bank, 7);
        NativeBattlePilotVisualIdentity recycled = NativeBattlePilotVisualLifetimes.Identity(258, arena, bank, 7);
        Assert.NotEqual(first.Generation, recycled.Generation);
        var later = Run(0, false, recycled);
        NativeBattlePilotVisualGeometry a = Assert.Single(earlier.Batches[0].Attempts).Primitive!.PilotGeometry!;
        NativeBattlePilotVisualGeometry b = Assert.Single(later.Batches[0].Attempts).Primitive!.PilotGeometry!;
        Assert.NotEqual(a.Identity, b.Identity);
        Assert.Same(a, NativeBattlePilotVisualGeometry.Sample(a, b, 0.5f));
    }

    [Theory]
    [InlineData((ushort)208, (byte)0, false)]
    [InlineData((ushort)208, (byte)1, false)]
    [InlineData((ushort)211, (byte)0, false)]
    [InlineData((ushort)211, (byte)1, false)]
    [InlineData((ushort)208, (byte)0, true)]
    [InlineData((ushort)211, (byte)1, true)]
    public void Selected208_211_frame_copy_keeps_native_capture_IO_and_discrete_actual_source(ushort effect, byte buffer, bool first)
    {
        var native = Run(buffer, first, null);
        string frozen = NativeBattleTestSnapshots.Json(native.Frame);
        var captured = Run(buffer, first, new(effect, 1, 2, 3, 4));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Batches), NativeBattleTestSnapshots.Json(captured.Batches));
        Assert.Equal(native.Addresses, captured.Addresses);
        Assert.Equal(native.Orderings, captured.Orderings);
        Assert.Equal(native.BufferReads, captured.BufferReads);
        if (!first)
        {
            foreach (NativeBattleGfGeometryBatch strip in captured.Batches.Take(5))
            {
                NativeBattlePilotVisualGeometry visual = Assert.Single(strip.Attempts).Primitive!.PilotGeometry!;
                Assert.Equal(effect, visual.Identity.Effect);
                Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x3000, visual.Identity.ResourceGeneration);
                Assert.True(visual.ScreenSpace);
                Assert.False(visual.Continuous);
            }
        }
    }

    private static (NativeBattleWaterExecutionFrame Frame, List<NativeBattleGfGeometryBatch> Batches, List<uint> Addresses, List<uint> Orderings, int BufferReads) Run(byte buffer, bool firstFrame, NativeBattlePilotVisualIdentity? identity, uint color = 0x808080)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x120);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x3000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC4, firstFrame ? 0u : 1u);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, color);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-12));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 9);
        frame = frame with { Arena = frame.Arena.Write(0x3000, new byte[] { 32, 0, 0, 1 }) };
        List<NativeBattleGfGeometryBatch> batches = [];
        List<uint> addresses = [];
        List<uint> orderings = [];
        int reads = 0;
        NativeBattleCinematicCapture96Services capture = new(_ =>
        {
            reads++;
            return buffer;
        }, (_, _) => [0, 0, buffer == 0 ? (byte)0 : (byte)1, 0], Append);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicFrameCopy26.Draw(frame, new(capture, Append), pilotIdentity: identity);
        return (result, batches, addresses, orderings, reads);

        NativeBattleWaterExecutionFrame Append(NativeBattleWaterExecutionFrame current, uint output, uint ordering, NativeBattleGfGeometryBatch batch)
        {
            batches.Add(batch);
            addresses.Add(output);
            orderings.Add(ordering);
            return current;
        }
    }
}
