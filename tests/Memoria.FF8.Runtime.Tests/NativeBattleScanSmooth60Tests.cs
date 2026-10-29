using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanSmooth60Tests
{
    [Fact]
    public void Reticle_metadata_preserves_native_packets_and_samples_authored_screen_motion()
    {
        NativeBattlePilotVisualIdentity identity = new(40, 3, 2, 1, 0);
        NativeBattleScanReticleRing native = NativeBattleScanReticleLines.Ring84D6D0(0, 0, 8, 0, 32);
        NativeBattleScanReticleRing earlier = NativeBattleScanReticleLines.Ring84D6D0(0, 0, 8, 0, 32, identity);
        NativeBattleScanReticleRing later = NativeBattleScanReticleLines.Ring84D6D0(10, 20, 16, 0, 32, identity);
        Assert.Equal(native.Packets.AllocationBytes, earlier.Packets.AllocationBytes);
        Assert.Equal(native.FinalPoint, earlier.FinalPoint);
        for (int index = 0; index < 32; index++)
        {
            NativeBattleGeometryPrimitive packet = earlier.Packets.Primitives[index].Primitive;
            Assert.Equal(native.Packets.Primitives[index].Primitive.Words, packet.Words);
            NativeBattlePilotVisualGeometry a = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
            NativeBattlePilotVisualGeometry b = Assert.IsType<NativeBattlePilotVisualGeometry>(later.Packets.Primitives[index].Primitive.PilotGeometry);
            Assert.True(a.ScreenSpace);
            Assert.Equal((byte)2, a.CornerCount);
            Assert.Equal((Vector3)(a.A + b.A) / 2, NativeBattlePilotVisualGeometry.SampleCorner(a, b, 0, 0.5f));
        }
    }

    [Fact]
    public void Band_scroll_preserves_packets_and_stops_matching_at_native_wrap()
    {
        NativeBattlePilotVisualIdentity identity = new(40, 4, 1, 2, 0);
        NativeBattleModulePacketBatch native = NativeBattleScanNormalBand.Draw84F0B0(62, 80);
        NativeBattleModulePacketBatch earlier = NativeBattleScanNormalBand.Draw84F0B0(62, 80, identity);
        NativeBattleModulePacketBatch later = NativeBattleScanNormalBand.Draw84F0B0(63, 88, identity);
        NativeBattleModulePacketBatch wrapped = NativeBattleScanNormalBand.Draw84F0B0(64, 96, identity);
        Assert.Equal(600, earlier.AllocationBytes);
        for (int index = 0; index < 15; index++)
        {
            Assert.Equal(native.Primitives[index].Primitive.Words, earlier.Primitives[index].Primitive.Words);
            NativeBattlePilotVisualGeometry a = Assert.IsType<NativeBattlePilotVisualGeometry>(earlier.Primitives[index].Primitive.PilotGeometry);
            NativeBattlePilotVisualGeometry b = Assert.IsType<NativeBattlePilotVisualGeometry>(later.Primitives[index].Primitive.PilotGeometry);
            NativeBattlePilotVisualGeometry c = Assert.IsType<NativeBattlePilotVisualGeometry>(wrapped.Primitives[index].Primitive.PilotGeometry);
            Assert.True(a.ScreenSpace);
            Assert.Equal(a.A + new Vector3(0, 0.5f, 0), NativeBattlePilotVisualGeometry.SampleCorner(a, b, 0, 0.5f));
            Assert.NotEqual(b.Identity, c.Identity);
        }
    }
}
