using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedMeshTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Fact]
    public void Plain_projection_supports_more_than_Water_selected_vertices_and_preserves_IR0_record_upper_DWORD()
    {
        NativeBattleWaterMeshPrepared prepared = Prepared(180, 0);
        int cue = 0x20000 + 180 * 8;
        prepared = prepared with { Frame = prepared.Frame with { Arena = prepared.Frame.Arena.Write(cue + 4, [1, 2, 3, 4]) } };
        NativeBattleScriptedMeshProjected result = Project(prepared, 0x40 + 180 * 8, 1, 0);
        Assert.Equal(180 * 8, result.Frame.Arena.Read(0x20000, 180 * 8).Length);
        Assert.Equal(1000u, BinaryPrimitives.ReadUInt32LittleEndian(result.DepthOwner.Read(179 * 4, 4)));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Frame.Arena.Read(cue + 4, 4));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(cue, 4)));
        Assert.Equal(Base + (uint)cue, result.Frame.ReadTemporaryDword(0x88));
    }

    [Fact]
    public void Normal_pass_uses_actual_packed_normal_and_preserves_work_upper_DWORD_and_FIFO()
    {
        NativeBattleWaterMeshPrepared prepared = Prepared(1, 1);
        NativeBattleWaterExecutionFrame frame = prepared.Frame;
        frame = frame with { Arena = frame.Arena.Write(0x20E1, [1]).Write(0x10048, [0, 0x10, 0, 0, 0, 0, 0xEF, 0xBE]).Write(0x2000C, [9, 8, 7, 6]) };
        NativeBattleScriptedLightingMemory lighting = UnitLighting();
        NativeBattleScriptedMeshProjected result = NativeBattleScriptedMeshPreparation.Project(prepared with { Frame = frame }, 0x50, new(null, 216, 160, 108), 0, 0, NativeBattleEffectArena.Clear(), lighting, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, result.Frame.Arena.Read(0x2000C, 4));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(result.Registers.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Equal(0x00000080u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x20008, 4)));
        Assert.Equal(0x00808080u, result.Registers.Material);
    }

    [Fact]
    public void Zero_E1_does_not_consume_normals_or_unknown_lighting_controls()
    {
        NativeBattleWaterMeshPrepared prepared = Prepared(1, 1);
        prepared = prepared with { Frame = prepared.Frame with { Arena = prepared.Frame.Arena.MarkUnknown(0x10048, 8) } };
        NativeBattleScriptedMeshProjected result = Project(prepared, 0x50);
        Assert.Null(result.Lighting.LightMatrix);
        Assert.Null(result.Registers.Rgb[2]);
    }

    [Theory]
    [InlineData(8, 0xB28110, 20, 32, 0x24)]
    [InlineData(18, 0xB28DD0, 24, 40, 0x2C)]
    [InlineData(19, 0xB290B0, 36, 52, 0x3C)]
    public void Ordinary_textured_writers_publish_exact_layout_UV_carry_and_actual_rank_one_material_controls(short type, uint handler, int stride, int length, byte command)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame().WriteTemporaryDword(0x98, 1);
        byte[] face = Face(type, stride);
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedMeshPackets.EmitSelected(frame, new(type, 1, face.ToImmutableArray()), handler, 0, Records(), [10, 20, 30, 40], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)(pointer / 4));
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(length, result.Packets.AllocationBytes);
        Assert.Equal(command, packet.Data[7]);
        Assert.Equal((ushort)0x1300, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(12)));
        Assert.Equal((ushort)0xFFF3, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(14)));
        Assert.Equal(new short[] { 4096, 0, 0, 4096, 0, 0, 4096, 0, 0 }, result.Lighting.LightMatrix!.Rotation);
        Assert.Equal(new short[] { 4096, 0, 0, 4096, 0, 0, 4096, 0, 0 }, result.Lighting.ColorMatrix!.Rotation);
        Assert.Equal(new NativeBattleScriptedBackColor(0, 0, 0), result.Lighting.BackColor);
        Assert.Equal(0x00808080u | ((uint)command << 24), BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(4)));
        Assert.Equal((byte)0, result.Registers.NormalInput!.Value[6]);
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedMeshPackets.EmitSelected(frame, new(type, 1, face.ToImmutableArray()), handler + 1, 0, Records(), [10, 20, 30, 40], 0, result.Lighting, result.Registers, _ => 0));
    }

    [Fact]
    public void Existing_GT3_packet_kernel_is_reused_with_actual_shader_register_publications()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame();
        byte[] face = new byte[28];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(index * 4), 0x00808080);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(18 + index * 2), (ushort)(index * 8));
        }
        NativeBattleWaterMeshGroup group = new(9, 1, face.ToImmutableArray());
        NativeBattleWaterMeshResult existing = NativeBattleWaterMeshPackets.EmitSelected(frame, group, Records(), [10, 20, 30, 40], 0x5000, pointer => (int)(pointer / 4));
        NativeBattleScriptedMeshSubmission current = NativeBattleScriptedMeshPackets.EmitSelected(frame, group, 0xB283B0, 0, Records(), [10, 20, 30, 40], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)(pointer / 4));
        Assert.Equal(existing.Packets.Attempts[0].Data, current.Packets.Attempts[0].Data);
        Assert.Equal(new uint?[] { 0x34808080, 0x00808080, 0x00808080 }, current.Registers.Rgb);
    }

    [Fact]
    public void Normal_G4_emits_quad_then_mode_same_OT_and_keeps_actual_lighting_for_four_corner_shades()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame().WriteTemporaryDword(0x68, Base + 0x10000).WriteTemporaryDword(0x78, Base + 0x10040);
        frame = frame with { Arena = frame.Arena.Write(0x10040, [0, 0x10, 0, 0, 0, 0, 0xEF, 0xBE]) };
        byte[] face = Face(12, 28);
        NativeBattleScriptedLightingMemory lighting = UnitLighting();
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedMeshPackets.EmitSelected(frame, new(12, 1, face.ToImmutableArray()), 0xB28690, 0x48, Records(), [10, 20, 30, 40], 0x5000, lighting, NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)(pointer / 4));
        Assert.Equal(48, result.Packets.AllocationBytes);
        Assert.Equal(2, result.Packets.Attempts.Length);
        Assert.Equal(36, result.Packets.Attempts[1].ByteOffsetRelative);
        Assert.Equal((byte)8, result.Packets.Attempts[0].Data[3]);
        Assert.Equal(0x3A000080u, BinaryPrimitives.ReadUInt32LittleEndian(result.Packets.Attempts[0].Data.AsSpan().Slice(4)));
        Assert.Equal(0x02000000u, BinaryPrimitives.ReadUInt32LittleEndian(result.Packets.Attempts[1].Data.AsSpan()));
        Assert.Equal(result.Packets.Attempts[0].Primitive!.Bucket, result.Packets.Attempts[1].Primitive!.Bucket);
        Assert.Same(lighting, result.Lighting);
        Assert.Equal(0x3A000000u, result.Frame.ReadTemporaryDword(0x84));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(result.Registers.NormalInput!.Value.AsSpan().Slice(6)));
    }

    [Fact]
    public void Rejected_lit_quad_writes_only_projected_positions_and_does_not_consume_undefined_normal()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame().WriteTemporaryDword(0x68, Base + 0x10000).WriteTemporaryDword(0x78, Base + 0x10040);
        byte[] records = Records().ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(4), 0x00020000);
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedMeshPackets.EmitSelected(frame, new(12, 1, Face(12, 28).ToImmutableArray()), 0xB28690, 0x48, records.ToImmutableArray(), [10, 20, 30, 40], 0x5000, NativeBattleScriptedLightingMemory.Unknown(), registers, _ => 0);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Equal(16, Assert.Single(result.Packets.Attempts).WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Same(registers, result.Registers);
    }

    [Fact]
    public void Independent_snapshot_repeats_plain_projection_work_and_native_shader_state()
    {
        NativeBattleWaterMeshPrepared prepared = Prepared(3, 0);
        NativeBattleWaterMeshPrepared restored = JsonSerializer.Deserialize<NativeBattleWaterMeshPrepared>(JsonSerializer.Serialize(prepared))!;
        NativeBattleScriptedMeshProjected expected = Project(prepared, 0x58);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Project(restored, 0x58)));
    }

    private static NativeBattleScriptedMeshProjected Project(NativeBattleWaterMeshPrepared prepared, int extent, short dqa = 0, int dqb = 0) => NativeBattleScriptedMeshPreparation.Project(prepared, extent, new(null, 216, 160, 108), dqa, dqb, NativeBattleEffectArena.Clear(), NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown());

    private static NativeBattleWaterMeshPrepared Prepared(int vertices, int normals)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x40, 0).WriteTemporaryDword(0x44, 0).WriteTemporaryDword(0x48, 0).WriteTemporaryDword(0x4C, 0).WriteTemporaryDword(0x14C, 0x600000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, Base + 0x20000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD8, Base + 0x10000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x00808080);
        NativeBattleEffectArena arena = frame.Arena.Write(0x10014, BitConverter.GetBytes(0x40u)).Write(0x10018, BitConverter.GetBytes(vertices)).Write(0x1001C, BitConverter.GetBytes((uint)(0x40 + vertices * 8))).Write(0x10020, BitConverter.GetBytes(normals));
        for (int index = 0; index < vertices; index++)
        {
            arena = arena.Write(0x10040 + index * 8, [0, 0, 0, 0, 0xE8, 3, 0, 0]);
        }
        return new(frame with { Arena = arena }, Identity, 216);
    }

    private static byte[] Face(short type, int stride)
    {
        byte[] face = new byte[stride];
        int colors = type is 12 or 19 ? 4 : 1;
        for (int index = 0; index < colors; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(index * 4), 0x00808080);
        }
        int corners = type == 8 ? 3 : 4;
        int indices = type == 8 ? 10 : type == 18 ? 12 : type == 19 ? 24 : 20;
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(indices + corner * 2), (ushort)(corner * 8));
        }
        if (type != 12)
        {
            int uv = type == 19 ? 16 : 4;
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(uv), 0x12FF);
            int metadata = type == 8 ? 16 : type == 18 ? 20 : 32;
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(metadata), 0xFFF0);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(metadata + 2), 0x10);
        }
        return face;
    }

    private static ImmutableArray<byte> Records()
    {
        byte[] records = new byte[32];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(index * 8), (uint)(index * 10));
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(index * 8 + 4), (uint)(index + 1) * 100);
        }
        return records.ToImmutableArray();
    }

    private static NativeBattleScriptedLightingMemory UnitLighting()
    {
        byte[] bytes = new byte[20];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 4096);
        NativeBattleScriptedLightingMatrix matrix = new(bytes.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray());
        return new(matrix, matrix, new(0, 0, 0));
    }
}
