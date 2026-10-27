using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69CasterDrawingTests
{
    private const uint SourceAddress = 0x10000000;
    private const uint ScratchAddress = 0x1968428;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Current_two_stage_projection_emits_primary_material_depth_sidecars_and_counter_underflow(bool quad, bool semi)
    {
        NativeBattleEffect69CasterDrawingStep result = Draw(Source(quad, semi));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        uint command = quad ? semi ? 0x2Eu : 0x2Cu : semi ? 0x26u : 0x24u;
        Assert.Equal(quad ? 40 : 32, result.Batch.AllocationBytes);
        Assert.Equal(command << 24 | 0x00112233u, primitive.Words[1]);
        Assert.Equal(0x10345566u, primitive.Words[5]);
        Assert.Equal(quad ? 125u : 124u, U(result.Temporary96, 0x44));
        Assert.Equal(17 + (quad ? 125 : 124), primitive.Bucket);
        Assert.Equal(new[] { 500, 500, 500, quad ? 500 : 0 }, primitive.VertexPayload4);
        Assert.Equal(ushort.MaxValue, W(result.Temporary96, 8));
        Assert.Equal(ushort.MaxValue, W(result.Temporary96, 10));
        Assert.Equal(ScratchAddress + 32, U(result.Temporary96, 0x5C));
        Assert.Equal(500, W(result.Scratch, 4));
        Assert.Equal(1, result.Scratch.Read(7, 1)[0]);
        Assert.Equal(quad ? 0x99AA : 0x7788, W(attempt.Data.AsSpan(), 28));
        Assert.All(attempt.WriteMask.Skip(30).Take(2), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        if (quad)
        {
            Assert.Equal(0xBBCC, W(attempt.Data.AsSpan(), 36));
            Assert.All(attempt.WriteMask.Skip(38).Take(2), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        }
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x40, 4).Known);
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x48, 4).Known);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Projection_load_preserves_partial_W_knownness_and_matrix_padding_without_seeding_them()
    {
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(31, [0xA5]).MarkUnknown(30, 1);
        NativeBattleEffect69CasterDrawingStep result = Draw(Source(false, false), scratch: scratch);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.True(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(0xA5, result.Memory.ScriptedColorRegisters.NormalInput!.Value[7]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.All(result.Scratch.ReadSnapshot(24, 8).Known, value => Assert.True(value));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect69CasterDrawingStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))))));
    }

    [Fact]
    public void Zero_depth_writes_only_reject_byte_after_world_XYZ_and_does_not_consume_unknown_high_clip()
    {
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        NativeBattleEffect69CasterDrawingStep result = Draw(Source(false, false), worldZ: 0, scratch: scratch);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal((short)-400, S(result.Scratch, 0));
        Assert.Equal((short)-400, S(result.Scratch, 2));
        Assert.Equal((short)0, S(result.Scratch, 4));
        Assert.Equal(0x10, result.Scratch.Read(6, 1)[0]);
        Assert.False(result.Scratch.ReadSnapshot(7, 1).Known[0]);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x3C, 4).Known);
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x44, 4).Known);
    }

    [Fact]
    public void Above_threshold_half_plane_rejects_before_Nclip_and_leaves_every_packet_byte_untouched()
    {
        NativeBattleEffect69CasterDrawingStep result = Draw(Source(false, false, above: true));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(2, result.Scratch.Read(7, 1)[0]);
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x3C, 4).Known);
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x44, 4).Known);
        Assert.All(result.Batch.Attempts[0].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Zero_Nclip_is_accepted_but_negative_area_rejects_before_average(bool negative)
    {
        byte[] source = Source(false, false);
        int face = FaceOffset;
        if (negative)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(face + 2), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(face + 4), 1);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(face + 2), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(face + 4), 0);
        }
        NativeBattleEffect69CasterDrawingStep result = Draw(source);
        Assert.Equal(negative, Assert.Single(result.Batch.Attempts).Primitive is null);
        if (negative)
        {
            Assert.True(unchecked((int)U(result.Temporary96, 0x3C)) < 0);
            Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x44, 4).Known);
        }
        else
        {
            Assert.Equal(0u, U(result.Temporary96, 0x3C));
            Assert.Equal(124u, U(result.Temporary96, 0x44));
        }
    }

    [Fact]
    public void Masked_object_reads_its_directory_pointer_without_touching_groups_scratch_or_registers()
    {
        byte[] source = Source(false, false);
        List<(uint Address, int Length)> reads = [];
        NativeBattleEffect69CasterDrawingStep result = Draw(source, mask: 0, observe: (address, length) => reads.Add((address, length)));
        Assert.Equal(new[] { (SourceAddress, 4), (SourceAddress + 4, 4) }, reads);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(SourceAddress + 8, U(result.Temporary96, 0x50));
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x5C, 4).Known);
        Assert.Null(result.Memory.GeometryRegisters!.Sxy[2]);
        Assert.DoesNotContain(true, result.Scratch.ReadSnapshot(0, 32).Known);
    }

    [Fact]
    public void Current_scratch_capacity_is_required_before_any_geometry_group_can_overrun_it()
    {
        Assert.Throws<InvalidDataException>(() => Draw(Source(false, false), scratchLength: 24));
    }

    [Fact]
    public void Current_ZSF_and_shift_are_observed_instead_of_a_fixed_average_or_bias()
    {
        NativeBattleEffect69CasterDrawingStep result = Draw(Source(false, false), controls: NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096 }, shift: 2);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Batch.Attempts).Primitive!;
        Assert.Equal(1500u, U(result.Temporary96, 0x44));
        Assert.Equal(17 + (1500 >> 2), primitive.Bucket);
        Assert.Equal(new[] { 500, 500, 500, 0 }, primitive.VertexPayload4);
    }

    [Fact]
    public void Canonical_logical_ordering_table_admits_packets_without_inventing_its_numeric_pointer()
    {
        NativeBattleEffect69CasterDrawingStep result = Draw(Source(false, false), unknownOrderingAddress: true);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.DoesNotContain(true, result.Temporary96.ReadSnapshot(0x54, 4).Known);
        Assert.Equal(141, result.Batch.Attempts[0].Primitive!.Bucket);
    }

    private const int FaceOffset = 52;
    private static byte[] Source(bool quad, bool semi, bool above = false)
    {
        byte[] bytes = new byte[FaceOffset + (quad ? 20 : 16)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 4);
        NativeBattleVector[] vertices = [new(-400, above ? (short)100 : (short)-400, 0), new(400, above ? (short)100 : (short)-400, 0), new(-400, above ? (short)300 : (short)0, 0), new(400, above ? (short)300 : (short)0, 0)];
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = 14 + index * 6;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vertices[index].Z);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40 + (quad ? 2 : 0)), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FaceOffset), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FaceOffset + 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FaceOffset + 4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FaceOffset + 6), quad ? (ushort)3 : (ushort)0x7788);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(FaceOffset + 8), 0x33441234);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(FaceOffset + 12), semi ? 0x12345566u : 0x10345566u);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FaceOffset + 16), 0x99AA);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(FaceOffset + 18), 0xBBCC);
        }
        return bytes;
    }

    private static NativeBattleEffect69CasterDrawingStep Draw(byte[] source, int worldZ = 2000, NativeBattleEffectArena? scratch = null, uint mask = 1, int scratchLength = 32, Action<uint, int>? observe = null, NativeBattleScriptedGraphicsMemory? controls = null, uint shift = 0, bool unknownOrderingAddress = false)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 96).Write(4, BitConverter.GetBytes(ScratchAddress)).Write(0x0C, new byte[4]).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x14, BitConverter.GetBytes(0x00112233u)).Write(0x18, BitConverter.GetBytes(mask)).Write(0x4C, new byte[4]).Write(0x54, BitConverter.GetBytes(0x1D8E04Cu + 0x44)).Write(0x58, BitConverter.GetBytes(shift));
        if (unknownOrderingAddress)
        {
            temporary = temporary.MarkUnknown(0x54, 4);
        }
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = worldZ }, null);
        return NativeBattleEffect69CasterDrawing.Draw(memory, temporary, (address, length) =>
        {
            observe?.Invoke(address, length);
            int offset = checked((int)(address - SourceAddress));
            return NativeBattleEffectArena.Clear().Write(0, source.AsSpan(offset, length));
        }, SourceAddress, [bone], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), new(null, 100, 160, 108), controls ?? NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), scratch ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 32), scratchLength);
    }

    private static uint U(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
    private static ushort W(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Read(offset, 2));
    private static ushort W(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));
    private static short S(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
}
