using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect239PrimitiveDepthTests
{
    [Theory]
    [InlineData(0.5f, false)]
    [InlineData(0.5f, true)]
    [InlineData(-0.5f, false)]
    public void Gouraud_quad_truncates_scaled_depth_and_retains_rejected_partial_writes(float scale, bool reject)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame();
        byte[] face = new byte[36];
        byte[] projected = new byte[32];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(corner * 4), 0x00808080);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(24 + corner * 2), (ushort)(corner * 8));
            BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(corner * 8), (uint)(corner * 10));
            BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(corner * 8 + 4), 100);
        }
        if (reject)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(4), 0x00460064);
        }
        NativeBattleWaterMeshGroup group = new(19, 1, face.ToImmutableArray());
        NativeBattleScriptedMeshSubmission Run(NativeBattleWaterExecutionFrame current) => NativeBattleScriptedMeshPackets.EmitSelected(current, group, 0xB290B0, 0, projected.ToImmutableArray(), [11, 23, 35, 47], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)(pointer / 4), NativeBattleGeometryRegisters.Unknown(), scale);
        NativeBattleScriptedMeshSubmission result = Run(frame);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal(reject ? 0 : 52, result.Packets.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[8]);
        if (reject)
        {
            Assert.Null(packet.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[3]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[7]);
        }
        else
        {
            int sign = scale < 0 ? -1 : 1;
            Assert.Equal(new[] { 5 * sign, 11 * sign, 17 * sign, 23 * sign }, packet.Primitive!.VertexPayload4);
            Assert.Equal((byte)12, packet.Data[3]);
            Assert.Equal((byte)0x3C, packet.Data[7]);
        }
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Run(restored))));
    }
}
