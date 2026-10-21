using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicFlatFt4Depth14Tests
{
    [Theory]
    [InlineData(1.75f, 1, 3, 5, 114686)]
    [InlineData(-1.75f, -1, -3, -5, -114686)]
    [InlineData(4294967296f, 0, 0, 0, 0)]
    public void Flat_FT4_uses_actual_material_UVs_and_truncated_low_WORD_depth_payloads(float scale, int first, int second, int third, int fourth)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleScriptedLightingMemory lighting = NativeBattleScriptedLightingMemory.Unknown();
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown() with { Flags = 0x12345678 };
        NativeBattleScriptedMeshSubmission result = Emit(frame, Group(), Records(), scale, lighting, registers);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(40, result.Packets.AllocationBytes);
        Assert.Equal(0x1064, primitive.Bucket);
        Assert.Equal(new[] { first, second, third, fourth }, primitive.VertexPayload4.ToArray());
        Assert.Equal(0x09000000u, primitive.Words[0]);
        Assert.Equal(0x2E332211u, primitive.Words[1]);
        Assert.Equal(0x00031001u, primitive.Words[3]);
        Assert.Equal(0x0210FFFFu, primitive.Words[5]);
        Assert.Equal(0x0203u, primitive.Words[7]);
        Assert.Equal(0x84A5u, primitive.Words[9]);
        Assert.Equal(33, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        foreach (int offset in new[] { 0, 1, 2, 30, 31, 38, 39 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
        }
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xF0));
        Assert.Equal(0x8028u, result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(0x11223344u, result.Frame.ReadTemporaryDword(0x6C));
        Assert.Equal(0x55667788u, result.Frame.ReadTemporaryDword(0x70));
        Assert.Same(lighting, result.Lighting);
        Assert.Equal(registers.Rgb, result.Registers.Rgb);
        Assert.Null(result.Registers.NormalInput);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Theory]
    [InlineData(true, false, 0u, true)]
    [InlineData(true, true, 0u, false)]
    [InlineData(false, false, 0x20000u, true)]
    [InlineData(false, false, 0x40000u, true)]
    [InlineData(false, false, 0x400000u, true)]
    [InlineData(false, false, 0x10000u, false)]
    [InlineData(false, true, 0x20000u, true)]
    public void Nclip_and_clip_rejections_keep_all_screen_stores_without_allocating_or_reading_material(bool clockwise, bool skipArea, uint clip, bool rejected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryDword(0x90, skipArea ? 0x10u : 0u);
        if (rejected)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x204, 4) };
        }
        NativeBattleScriptedMeshSubmission result = Emit(frame, Group(), Records(clockwise, clip), 1);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Equal(rejected, attempt.Primitive is null);
        Assert.Equal(rejected ? 0 : 40, result.Packets.AllocationBytes);
        Assert.Equal(rejected ? 16 : 33, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Equal(rejected ? 0x8000u : 0x8028u, result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(skipArea ? 0x12345678u : unchecked((uint)(clockwise ? -100 : 100)), result.Registers.Flags);
    }

    [Fact]
    public void Rejected_then_accepted_faces_reuse_current_output_and_advance_only_for_the_accepted_packet()
    {
        byte[] rejected = Group().Faces.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(rejected.AsSpan(14), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(rejected.AsSpan(16), 8);
        NativeBattleWaterMeshGroup group = new(14, 2, rejected.Concat(Group().Faces).ToImmutableArray());
        NativeBattleScriptedMeshSubmission result = Emit(Fixture(), group, Records(), 1);
        Assert.Null(result.Packets.Attempts[0].Primitive);
        Assert.NotNull(result.Packets.Attempts[1].Primitive);
        Assert.All(result.Packets.Attempts, attempt => Assert.Equal(0, attempt.ByteOffsetRelative));
        Assert.Equal(40, result.Packets.AllocationBytes);
    }

    [Fact]
    public void Vertex_byte_offsets_are_not_rounded_and_depth_index_uses_the_original_shift()
    {
        byte[] face = Group().Faces.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(12), 1);
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryDword(0x90, 0x10);
        ImmutableArray<byte> records = Records();
        NativeBattleScriptedMeshSubmission result = Emit(frame, new(14, 1, face.ToImmutableArray()), records, 1);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(records.AsSpan().Slice(1)), primitive.Words[2]);
        Assert.Equal(1, primitive.VertexPayload4[0]);
    }

    [Fact]
    public void Wrong_handler_and_short_face_projected_or_separate_depth_owners_fail_fast()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicFlatFt4Depth14.Emit(frame, Group(), 0xB28DD0, Records(), [1, 2, 3, 4], 0x8000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)pointer, 1));
        Assert.Throws<InvalidDataException>(() => Emit(frame, Group() with { Faces = [] }, Records(), 1));
        Assert.Throws<InvalidDataException>(() => Emit(frame, Group(), Records().Slice(0, 24), 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicFlatFt4Depth14.Emit(frame, Group(), 0xB36F40, Records(), [1, 2, 3], 0x8000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), pointer => (int)pointer, 1));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x50, 0xE1000210).WriteTemporaryDword(0x54, 5).WriteTemporaryDword(0x5C, 0x1000).WriteTemporaryDword(0x80, NativeBattleEffectNodeMemory.ArenaAddress + 0x200).WriteTemporaryDword(0x84, 0).WriteTemporaryDword(0x8C, 0x40000000).WriteTemporaryDword(0x90, 0).WriteTemporaryWord(0x98, 0xFEFE).WriteTemporaryDword(0x6C, 0x11223344).WriteTemporaryDword(0x70, 0x55667788);
        return frame with { Arena = frame.Arena.Write(0x204, BitConverter.GetBytes(0x02332211u)) };
    }

    private static NativeBattleWaterMeshGroup Group()
    {
        byte[] face = new byte[20];
        ushort[] uv = [0x1001, 0xFFFF, 0x0203, 0x84A5];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(corner * 2), uv[corner]);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(12 + corner * 2), (ushort)(corner * 8));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0xFFFE);
        return new(14, 1, face.ToImmutableArray());
    }

    private static ImmutableArray<byte> Records(bool clockwise = false, uint clip = 0)
    {
        byte[] bytes = new byte[32];
        uint[] screens = clockwise ? [0, 0x000A0000, 10, 0x000A000A] : [0, 10, 0x000A0000, 0x000A000A];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(corner * 8), screens[corner]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(corner * 8 + 4), (uint)(40 * (corner + 1)) | (corner == 3 ? clip : 0));
        }
        return bytes.ToImmutableArray();
    }

    private static NativeBattleScriptedMeshSubmission Emit(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, ImmutableArray<byte> records, float scale, NativeBattleScriptedLightingMemory? lighting = null, NativeBattleScriptedColorRegisters? registers = null)
    {
        return NativeBattleCinematicFlatFt4Depth14.Emit(frame, group, 0xB36F40, records, [0xABCD0001, 2, 3, 65535], 0x8000, lighting ?? NativeBattleScriptedLightingMemory.Unknown(), registers ?? NativeBattleScriptedColorRegisters.Unknown() with { Flags = 0x12345678 }, address => (int)address, scale);
    }
}
