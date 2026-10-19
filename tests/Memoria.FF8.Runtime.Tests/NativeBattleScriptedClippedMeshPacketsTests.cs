using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedClippedMeshPacketsTests
{
    [Fact]
    public void Clipped_FT3_uses_direct_material_color_and_preserves_partial_packet_layout()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x02010203);
        ushort[] words = [0x12FF, 0x3344, 0x5566, 0, 8, 16, 0xFFF0, 0x20];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4 + 2 * index), words[index]);
        }
        NativeBattleWaterMeshGroup group = new(8, 1, face.ToImmutableArray());
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedClippedMeshPackets.EmitFt3(Frame(), group, 0xB11100, Records([0, 10, 20], [8, 2, 0]), [65535, 11, 13], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), address => (int)(address / 4), 0.5f);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal(32, result.Packets.AllocationBytes);
        Assert.Equal(0x5020u, result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(2712, packet.Primitive!.Bucket);
        Assert.Equal(new[] { 32768, 6, 6, 0 }, packet.Primitive.VertexPayload4);
        Assert.Equal(0x26010203u, Word32(packet, 4));
        Assert.Equal((ushort)0x12FF, Word16(packet, 12));
        Assert.Equal((ushort)0x3344, Word16(packet, 20));
        Assert.Equal((ushort)0x5566, Word16(packet, 28));
        Assert.Equal((ushort)0xFFF3, Word16(packet, 14));
        Assert.Equal((ushort)0x30, Word16(packet, 22));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[31]);
        NativeBattleScriptedMeshSubmission clipped = NativeBattleScriptedClippedMeshPackets.EmitFt3(Frame(), group, 0xB11100, Records([0, 10, 20], [8, 8, 8]), [1, 2, 3], 0x5000, result.Lighting, result.Registers, _ => throw new InvalidOperationException(), 1);
        Assert.All(Assert.Single(clipped.Packets.Attempts).WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Theory]
    [InlineData(0xB053E0u)]
    [InlineData(0xB11340u)]
    public void Clipped_GT3_preserves_direct_colors_and_UVs_and_uses_full_depth_sum_before_division(uint handler)
    {
        NativeBattleWaterExecutionFrame frame = Frame();
        NativeBattleScriptedLightingMemory lighting = NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedClippedMeshPackets.EmitGt3(frame, Group(), handler, Records([0, 10, 20], [8, 2, 0]), [65535, 11, 13], 0x5000, lighting, registers, address => (int)(address / 4), 0.5f);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal(40, result.Packets.AllocationBytes);
        Assert.Equal(0x5028u, result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(2712, packet.Primitive!.Bucket);
        Assert.Equal(new[] { 32768, 6, 6, 0 }, packet.Primitive.VertexPayload4);
        Assert.Equal(0x36010203u, Word32(packet, 4));
        Assert.Equal(0xDE0A0B0Cu, Word32(packet, 16));
        Assert.Equal(0xFE101112u, Word32(packet, 28));
        Assert.Equal((ushort)0x12FF, Word16(packet, 12));
        Assert.Equal((ushort)0x3344, Word16(packet, 24));
        Assert.Equal((ushort)0x5566, Word16(packet, 36));
        Assert.Equal((ushort)0xFFF3, Word16(packet, 14));
        Assert.Equal((ushort)0x30, Word16(packet, 26));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[39]);
        Assert.Same(lighting, result.Lighting);
        Assert.Same(registers, result.Registers);
    }

    [Fact]
    public void Common_clip_intersection_rejects_before_any_screen_stores()
    {
        NativeBattleScriptedMeshSubmission result = Emit(Records([0, 10, 20], [8, 8, 8]));
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.All(Assert.Single(result.Packets.Attempts).WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Fact]
    public void Negative_winding_always_rejects_after_screen_stores_even_when_ordinary_no_cull_flag_is_set()
    {
        NativeBattleWaterExecutionFrame frame = Frame().WriteTemporaryDword(0x90, 0x10);
        NativeBattleScriptedMeshSubmission result = Emit(Records([0, 80u << 16, 80], [0, 0, 0]), frame);
        Assert.Equal(0, result.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal(12, packet.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Null(packet.Primitive);
    }

    private static NativeBattleScriptedMeshSubmission Emit(ImmutableArray<byte> records, NativeBattleWaterExecutionFrame? frame = null) => NativeBattleScriptedClippedMeshPackets.EmitGt3(frame ?? Frame(), Group(), 0xB053E0, records, [1, 2, 3], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), address => (int)(address / 4), 1);

    private static NativeBattleWaterExecutionFrame Frame()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x50, 0xE1000010).WriteTemporaryDword(0x54, 3).WriteTemporaryDword(0x5C, 0);
        return frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0x84, false).SetItem(0x8C, false).SetItem(0x90, false).SetItem(0x98, false) } };
    }

    private static NativeBattleWaterMeshGroup Group()
    {
        byte[] face = new byte[28];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x02010203);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(4), 0xDE0A0B0C);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0xFE101112);
        ushort[] words = [0x12FF, 0x3344, 0x5566, 0, 8, 16, 0xFFF0, 0x20];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(12 + index * 2), words[index]);
        }
        return new(9, 1, face.ToImmutableArray());
    }

    private static ImmutableArray<byte> Records(uint[] screens, ushort[] clips)
    {
        byte[] bytes = new byte[24];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 8), screens[index]);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 4), 60000);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 8 + 6), clips[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static uint Word32(NativeBattleGfGeometryAttempt packet, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(offset));
    private static ushort Word16(NativeBattleGfGeometryAttempt packet, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(offset));
}
