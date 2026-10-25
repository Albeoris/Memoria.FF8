using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl18Primitive16Tests
{
    [Theory]
    [InlineData(0xB105B0u, 0.25f, 1)]
    [InlineData(0xAEC030u, 0.25f, 1)]
    [InlineData(0xB50540u, 0.25f, 1)]
    [InlineData(0xB50540u, -0.25f, -1)]
    public void Type16_clones_truncate_fractional_scaled_unsigned_depths_before_publishing_four_metadata_lanes(uint handler, float scale, int sign)
    {
        NativeBattleScriptedMeshSubmission result = Run(handler, scale);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(new[] { sign, 2 * sign, 3 * sign, 4 * sign }, primitive.VertexPayload4);
        Assert.Equal(32, result.Packets.AllocationBytes);
        Assert.Equal((byte)7, attempt.Data[3]);
        Assert.Equal(0xE1000003u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(4, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(8, 4)));
        Assert.Equal(0x28808080u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(12, 4)));
        Assert.Equal(0x28808080u, result.Registers.Rgb[2]);
        Assert.Equal(new byte[] { 0, 16, 0, 16, 0, 16, 0, 0 }, result.Registers.NormalInput!.Value.ToArray());
    }

    [Theory]
    [InlineData(0x46u << 16, false)]
    [InlineData(0u, true)]
    public void Original_control18_handler_keeps_partial_SXY_writes_and_allocates_only_accepted_faces(uint clip, bool accepted)
    {
        NativeBattleScriptedMeshSubmission result = Run(0xB50540, 0.25f, clip);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Equal(accepted ? 32 : 0, result.Packets.AllocationBytes);
        Assert.Equal(accepted, attempt.Primitive is not null);
        Assert.All(attempt.WriteMask.Skip(16), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(accepted ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[3]);
        Assert.Equal(accepted ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
    }

    private static NativeBattleScriptedMeshSubmission Run(uint handler, float depthScale, uint clip = 0)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame();
        byte[] face = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x00808080);
        byte[] projected = new byte[32];
        uint[] screens = [0, 20, 20u << 16, 20u | 20u << 16];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4 + corner * 2), (ushort)(corner * 8));
            BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(corner * 8), screens[corner]);
            BinaryPrimitives.WriteUInt32LittleEndian(projected.AsSpan(corner * 8 + 4), clip | 100u);
        }
        return NativeBattleScriptedMeshPackets.EmitSelected(frame, new(16, 1, face.ToImmutableArray()), handler, 0, projected.ToImmutableArray(), [0xFFFF0007, 11, 15, 19], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)(pointer / 4), NativeBattleGeometryRegisters.Unknown(), depthScale);
    }
}
