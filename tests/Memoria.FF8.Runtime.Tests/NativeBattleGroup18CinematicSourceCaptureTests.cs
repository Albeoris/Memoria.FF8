using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup18CinematicSourceCaptureTests
{
    [Theory]
    [InlineData(241, 8, 0xA041A0u, 0xB28110u)]
    [InlineData(241, 18, 0xA044D0u, 0xB28DD0u)]
    [InlineData(242, 6, 0x9FA190u, 0xAEB5B0u)]
    [InlineData(242, 7, 0x9FA3D0u, 0xB27E90u)]
    [InlineData(242, 9, 0x9FA660u, 0xB283B0u)]
    [InlineData(242, 17, 0x9FA990u, 0xB28B00u)]
    [InlineData(242, 18, 0x9FAC60u, 0xB28DD0u)]
    [InlineData(242, 19, 0x9FAF40u, 0xB290B0u)]
    [InlineData(252, 7, 0x9A4BF0u, 0xB27E90u)]
    [InlineData(252, 16, 0x9A4EF0u, 0xB105B0u)]
    [InlineData(252, 17, 0x9A5160u, 0xB28B00u)]
    [InlineData(258, 7, 0x9679E0u, 0xB27E90u)]
    [InlineData(258, 17, 0x967CF0u, 0xB28B00u)]
    public void Actual_selected_primitive_families_preserve_native_packet_state_and_authored_face_identity(ushort effect, short type, uint selectedHandler, uint canonicalHandler)
    {
        Assert.Equal(canonicalHandler, NativeBattleCinematicDispatch.RequireSlot(effect, "prim_table", type, selectedHandler));
        Assert.Equal(canonicalHandler, NativeBattleCinematicDispatch.Canonicalize(effect, selectedHandler));
        var native = Run(effect, type, canonicalHandler, false, false);
        string frameBaseline = NativeBattleTestSnapshots.Json(native.Frame);
        string batchBaseline = NativeBattleTestSnapshots.Json(native.Packets);
        string lightingBaseline = JsonSerializer.Serialize(native.Lighting);
        string registerBaseline = JsonSerializer.Serialize(native.Registers);
        string geometryBaseline = JsonSerializer.Serialize(native.Geometry);
        var captured = Run(effect, type, canonicalHandler, true, false);
        Assert.Equal(frameBaseline, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(batchBaseline, NativeBattleTestSnapshots.Json(captured.Packets));
        Assert.Equal(lightingBaseline, JsonSerializer.Serialize(captured.Lighting));
        Assert.Equal(registerBaseline, JsonSerializer.Serialize(captured.Registers));
        Assert.Equal(geometryBaseline, JsonSerializer.Serialize(captured.Geometry));
        Assert.Equal(2, captured.Packets.Attempts.Length);
        var first = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.Packets.Attempts[0].Primitive!.PilotGeometry);
        var second = Assert.IsType<NativeBattlePilotVisualGeometry>(captured.Packets.Attempts[1].Primitive!.PilotGeometry);
        Assert.Equal(effect, first.Identity.Effect);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, first.Identity.Bank);
        Assert.Equal(3, first.Identity.Slot);
        Assert.True(first.Identity.Generation > 0);
        Assert.NotEqual(first.Identity.Primitive, second.Identity.Primitive);
        Assert.Equal(new Vector3(100, 200, 300), first.A);
        Assert.Equal(new Vector3(101, 201, 301), first.B);
        Assert.Equal(type is 6 or 7 or 8 or 9 ? (byte)3 : (byte)4, first.CornerCount);
        Assert.Equal(0.5f, first.DepthScale);
        string samplingBaseline = NativeBattleTestSnapshots.Json(captured.Frame);
        _ = NativeBattlePilotVisualGeometry.Sample(first, first with { A = first.A + Vector3.One }, 0.5f);
        Assert.Equal(samplingBaseline, NativeBattleTestSnapshots.Json(captured.Frame));
        var rejectedNative = Run(effect, type, canonicalHandler, false, true);
        string rejectedFrame = NativeBattleTestSnapshots.Json(rejectedNative.Frame);
        string rejectedPackets = NativeBattleTestSnapshots.Json(rejectedNative.Packets);
        var rejectedCaptured = Run(effect, type, canonicalHandler, true, true);
        Assert.Equal(rejectedFrame, NativeBattleTestSnapshots.Json(rejectedCaptured.Frame));
        Assert.Equal(rejectedPackets, NativeBattleTestSnapshots.Json(rejectedCaptured.Packets));
        Assert.All(rejectedCaptured.Packets.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Theory]
    [InlineData(241)]
    [InlineData(242)]
    [InlineData(252)]
    [InlineData(258)]
    public void Real_slot_bank_birth_reuse_and_source_domain_separate_visual_lifetimes(ushort effect)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleWaterSlotBank bank = new(0x1000, 0x4000, 4, 0x2000);
        NativeBattleCinematicVisualIdentity.Allocated(frame.Arena, bank, 3);
        NativeBattlePilotVisualIdentity first = NativeBattleCinematicVisualIdentity.Identity(effect, frame, bank, 3);
        NativeBattlePilotVisualIdentity stable = NativeBattleCinematicVisualIdentity.Identity(effect, frame, bank, 3);
        Assert.Equal(first, stable);
        NativeBattleWaterSlotBank otherBank = bank with { SlotBaseOffset = 0x3000 };
        Assert.NotEqual(first, NativeBattleCinematicVisualIdentity.Identity(effect, frame, otherBank, 3));
        NativeBattleCinematicVisualIdentity.Allocated(frame.Arena, bank, 3);
        NativeBattlePilotVisualIdentity recycled = NativeBattleCinematicVisualIdentity.Identity(effect, frame, bank, 3);
        Assert.NotEqual(first.Generation, recycled.Generation);
        NativeBattlePilotVisualGeometry earlier = new(first, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3);
        NativeBattlePilotVisualGeometry later = earlier with { Identity = recycled, A = new(99) };
        Assert.Same(earlier, NativeBattlePilotVisualGeometry.Sample(earlier, later, 0.5f));
        Assert.NotEqual(first, first with { ResourceGeneration = 1 });
    }

    private static NativeBattleScriptedMeshSubmission Run(ushort effect, short type, uint handler, bool capture, bool reject)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame().WriteTemporaryDword(0x98, 1);
        NativeBattlePilotVisualIdentity? identity = null;
        if (capture)
        {
            uint bank = NativeBattleEffectNodeMemory.ArenaAddress + 0x2000;
            NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, bank, 3);
            identity = NativeBattlePilotVisualLifetimes.Identity(effect, frame.Arena, bank, 3, 17);
        }
        int corners = type is 6 or 7 or 8 or 9 ? 3 : 4;
        int stride = type is 6 or 16 ? 12 : type is 7 or 8 ? 20 : type == 9 ? 28 : type == 19 ? 36 : 24;
        int indices = type is 6 or 16 ? 4 : type == 7 ? 12 : type == 8 ? 10 : type == 9 ? 18 : type == 17 ? 16 : type == 18 ? 12 : 24;
        byte[] face = new byte[stride];
        int colors = type is 7 or 9 or 17 or 19 ? corners : 1;
        for (int corner = 0; corner < colors; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(corner * 4), 0x00808080);
        }
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(indices + corner * 2), checked((ushort)(corner * 8)));
        }
        byte[] records = new byte[32];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(corner * 8), checked((uint)(corner * 10)));
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(corner * 8 + 4), reject ? 0x00460064u : 100u);
        }
        Vector3[] world = [new(100, 200, 300), new(101, 201, 301), new(102, 202, 302), new(103, 203, 303)];
        return NativeBattleScriptedMeshPackets.EmitSelected(frame, new(type, 2, face.Concat(face).ToImmutableArray()), handler, 0, records.ToImmutableArray(), [11, 23, 35, 47], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => checked((int)(pointer / 4)), NativeBattleGeometryRegisters.Unknown(), 0.5f, capture ? world : null, identity);
    }
}
