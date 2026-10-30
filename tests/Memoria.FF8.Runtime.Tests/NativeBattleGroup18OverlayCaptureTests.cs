using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup18OverlayCaptureTests
{
    [Theory]
    [InlineData(230, false)]
    [InlineData(230, true)]
    [InlineData(257, false)]
    [InlineData(257, true)]
    [InlineData(238, false)]
    [InlineData(238, true)]
    [InlineData(242, false)]
    [InlineData(242, true)]
    [InlineData(252, false)]
    [InlineData(252, true)]
    public void Selected_owner_keeps_native_color_gate_packet_order_and_zero_depth_discrete(ushort effect, bool zero)
    {
        NativeBattleCinematicOverlayStep native = Run(null, zero);
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string nativeBatch = NativeBattleTestSnapshots.Json(native.Packets);
        NativeBattlePilotVisualIdentity identity = new(effect, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000, 2, 73, 0);
        NativeBattleCinematicOverlayStep captured = Run(identity, zero);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(captured.Packets));
        Assert.Equal(zero ? 0 : 36, captured.Packets.AllocationBytes);
        Assert.Null(captured.Packets.Attempts[0].Primitive);
        if (zero)
        {
            Assert.Single(captured.Packets.Attempts);
            return;
        }
        NativeBattleGeometryPrimitive primitive = captured.Packets.Attempts[1].Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(identity, geometry.Identity);
        Assert.False(geometry.Continuous);
        Assert.True(geometry.ScreenSpace);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(Vector4.Zero, geometry.SourceDepths);
        Assert.Empty(primitive.VertexPayload4);
        Assert.Equal(new Vector3(320, 256, 0), geometry.D);
        Assert.Same(geometry, NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = Vector3.One }, 0.5f));
        Assert.Null(captured.Packets.Attempts[2].Primitive!.PilotGeometry);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
    }

    private static NativeBattleCinematicOverlayStep Run(NativeBattlePilotVisualIdentity? identity, bool zero)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x20);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, zero ? (ushort)0 : unchecked((ushort)-12));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, zero ? (ushort)0 : (ushort)256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, zero ? (ushort)0 : (ushort)100);
        return NativeBattleCinematicOverlay.Draw(frame, address => checked((int)(address / 4)), pilotIdentity: identity);
    }
}
