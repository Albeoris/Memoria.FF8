using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221DepthQuad58Tests
{
    [Theory]
    [InlineData(1.75f, 1, 3, 5, 114686)]
    [InlineData(-1.75f, -1, -3, -5, -114686)]
    [InlineData(4294967296f, 0, 0, 0, 0)]
    public void Scaled_depths_truncate_through_low_DWORD_and_packet_preserves_exact_material_UV_and_padding(float scale, int first, int second, int third, int fourth)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleScriptedMeshSubmission result = Emit(frame, Group(), Records(), scale);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Equal(44, result.Packets.AllocationBytes);
        Assert.Equal(0, attempt.ByteOffsetRelative);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(0x1064, primitive.Bucket);
        Assert.Equal(new[] { first, second, third, fourth }, primitive.VertexPayload4.ToArray());
        Assert.Equal(0x0A000000u, primitive.Words[0]);
        Assert.Equal(0x2E332211u, primitive.Words[1]);
        Assert.Equal(0x00000101u, primitive.Words[3]);
        Assert.Equal(0x02500100u, primitive.Words[5]);
        Assert.Equal(0xE1000220u, primitive.Words[10]);
        foreach (int offset in new[] { 0, 1, 2, 30, 31, 38, 39 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
        }
        Assert.Equal(37, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Equal(0x802Cu, result.Frame.ReadTemporaryDword(0x60));
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xF0));
        Assert.Equal(0x2E332211u, result.Registers.Rgb[2]);
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Theory]
    [InlineData(true, false, 0u, true)]
    [InlineData(true, true, 0u, false)]
    [InlineData(false, false, 0x20000u, true)]
    [InlineData(false, false, 0x40000u, true)]
    [InlineData(false, false, 0x400000u, true)]
    [InlineData(false, false, 0x10000u, false)]
    public void Rejected_faces_keep_screen_attempts_and_current_cursor_and_optional_Nclip_register(bool clockwise, bool skipArea, uint clip, bool rejected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryDword(0x90, skipArea ? 0x10u : 0u);
        NativeBattleScriptedMeshSubmission result = Emit(frame, Group(), Records(clockwise, clip), 1);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Equal(rejected ? 0 : 44, result.Packets.AllocationBytes);
        Assert.Equal(rejected, attempt.Primitive is null);
        Assert.Equal(rejected ? 16 : 37, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Equal(rejected ? 0x8000u : 0x802Cu, result.Frame.ReadTemporaryDword(0x60));
        if (rejected)
        {
            Assert.Equal(unchecked((uint)(clockwise ? -100 : 100)), result.Registers.Flags);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[3]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[40]);
        }
    }

    [Fact]
    public void Consecutive_rejected_and_accepted_faces_reuse_the_same_native_output_address()
    {
        byte[] faces = Group().Faces.ToArray();
        byte[] rejected = faces.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(rejected.AsSpan(14), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(rejected.AsSpan(16), 8);
        NativeBattleScriptedMeshSubmission result = Emit(Fixture(), new(18, 2, rejected.Concat(faces).ToImmutableArray()), Records(), 1);
        Assert.Equal(2, result.Packets.Attempts.Length);
        Assert.Null(result.Packets.Attempts[0].Primitive);
        Assert.NotNull(result.Packets.Attempts[1].Primitive);
        Assert.All(result.Packets.Attempts, attempt => Assert.Equal(0, attempt.ByteOffsetRelative));
        Assert.Equal(44, result.Packets.AllocationBytes);
    }

    [Fact]
    public void A_different_original_handler_is_never_admitted_as_the_depth_quad()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleApocalypse221DepthQuad58.Emit(Fixture(), Group(), 0xB28DD0, Records(), [1, 2, 3, 4], 0x8000, NativeBattleScriptedColorRegisters.Unknown(), address => (int)address, 1));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        return WaterSchedulerFixture.Create().WriteTemporaryDword(0x50, 0xE1000210).WriteTemporaryDword(0x54, uint.MaxValue).WriteTemporaryDword(0x5C, 0x1000).WriteTemporaryDword(0x84, 0x808080).WriteTemporaryDword(0x8C, 0x02000000).WriteTemporaryDword(0x90, 0).WriteTemporaryWord(0x98, 0x0101);
    }

    private static NativeBattleWaterMeshGroup Group()
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x332211);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0xFFFF);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(12 + index * 2), (ushort)(index * 8));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(22), 0x40);
        return new(18, 1, face.ToImmutableArray());
    }

    private static ImmutableArray<byte> Records(bool clockwise = false, uint clip = 0)
    {
        byte[] records = new byte[32];
        uint[] screens = clockwise ? [0, 0x000A0000, 10, 0x000A000A] : [0, 10, 0x000A0000, 0x000A000A];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(index * 8), screens[index]);
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(index * 8 + 4), (uint)(40 * (index + 1)) | (index == 3 ? clip : 0));
        }
        return records.ToImmutableArray();
    }

    private static NativeBattleScriptedMeshSubmission Emit(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, ImmutableArray<byte> records, float scale)
        => NativeBattleApocalypse221DepthQuad58.Emit(frame, group, 0xAAAE60, records, [0xABCD0001, 2, 3, 65535], 0x8000, NativeBattleScriptedColorRegisters.Unknown() with { Flags = 0x12345678 }, address => (int)address, scale);
}
