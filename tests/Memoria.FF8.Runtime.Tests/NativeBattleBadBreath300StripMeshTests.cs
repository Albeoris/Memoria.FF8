using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath300StripMeshTests
{
    private const uint SourceAddress = 0x103FA04;
    private const uint VertexAddress = 0x20DFAB8 + 0x85C;

    [Fact]
    public void FT4_and_GT4_keep_exact_72_and_84_byte_extents_and_native_window_mode_link_order()
    {
        NativeBattleBadBreath300StripMeshStep result = Draw(Source(), Context());
        Assert.Equal(156, result.Batch.AllocationBytes);
        Assert.Equal(10, result.Batch.Attempts.Length);
        NativeBattleGeometryPrimitive[] linked = result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(new byte[] { 0xE2, 0xE1, 0x2E, 0xE2, 0xE2, 0xE1, 0x3E, 0xE2 }, linked.Select(primitive => unchecked((byte)(primitive.Words[1] >> 24))));
        Assert.Equal(new[] { 40, 52, 0, 60, 124, 136, 72, 144 }, result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(0xE1000220u, linked[1].Words[1]);
        Assert.Equal(0xE2000000u, linked[0].Words[1]);
        Assert.Equal(0xE2000010u, linked[3].Words[1]);
        Assert.All(linked, primitive => Assert.Equal(142, primitive.Bucket));
        Assert.Equal(SourceAddress + 132, U32(result.Context92, 0x24));
        Assert.Equal(500u, U32(result.Context92, 0x30));
        Assert.Equal(0u, U32(result.Context92, 0x34) & 0x60000);
        Assert.Throws<NotSupportedException>(() => result.Context92.Read(0x2C, 4));
        Assert.Throws<NotSupportedException>(() => result.Context92.Read(0x38, 36));
    }

    [Fact]
    public void Byte_IR_shader_preserves_material_and_retains_unwritten_GT4_color_code_bytes()
    {
        NativeBattleBadBreath300StripMeshStep result = Draw(Source(), Context());
        NativeBattleGfGeometryAttempt flat = result.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt gouraud = result.Batch.Attempts[5];
        Assert.Equal(0x2E604020u, BinaryPrimitives.ReadUInt32LittleEndian(flat.Data.AsSpan(4, 4)));
        Assert.Equal(new byte[] { 32, 64, 96 }, gouraud.Data.AsSpan(4, 3).ToArray());
        Assert.Equal(new byte[] { 16, 32, 48 }, gouraud.Data.AsSpan(16, 3).ToArray());
        Assert.Equal(new byte[] { 64, 128, 191 }, gouraud.Data.AsSpan(28, 3).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0 }, gouraud.Data.AsSpan(40, 3).ToArray());
        foreach (int offset in new[] { 19, 31, 43 })
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, gouraud.WriteMask[offset]);
        }
        Assert.Equal(0x7F123456u, result.Registers.Colors.Material);
        Assert.Equal(new uint?[] { 0x7F302010, 0x7FBF8040, 0x7F000000 }, result.Registers.Colors.Rgb);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Registers.Colors.Mac);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x4000, Enumerable.Repeat((byte)0xA5, 200).ToArray()) };
        NativeBattleEffectPacketWrite applied = NativeBattleEffectPacketArena.Append(memory, 0x4000, NativeBattleLogicalOrdering.Clear(), result.Batch);
        Assert.Equal(new byte[] { 0xA5 }, applied.Memory.Arena!.Read(0x4000 + 72 + 19, 1));
    }

    [Fact]
    public void IR_byte_store_wraps_instead_of_using_the_saturated_RGB_FIFO()
    {
        ImmutableArray<byte> source = Source();
        byte[] bytes = source.ToArray();
        bytes[56] = 255;
        NativeBattleEffectArena context = Context().Write(4, [255, 255, 255]);
        NativeBattleBadBreath300StripMeshStep result = Draw(bytes.ToImmutableArray(), context);
        Assert.Equal(new byte[] { 252, 252, 252 }, result.Batch.Attempts[0].Data.AsSpan(4, 3).ToArray());
    }

    [Fact]
    public void U_overflow_wrap_adjusts_every_corner_once_and_keeps_V_material_and_full_DWORD_stores()
    {
        NativeBattleBadBreath300StripMeshStep result = Draw(Source(), Context());
        NativeBattleGfGeometryAttempt flat = result.Batch.Attempts[0];
        Assert.Equal(0x12340184u, BinaryPrimitives.ReadUInt32LittleEndian(flat.Data.AsSpan(12, 4)));
        Assert.Equal(0x5678027Au, BinaryPrimitives.ReadUInt32LittleEndian(flat.Data.AsSpan(20, 4)));
        Assert.Equal(0x04FA0389u, BinaryPrimitives.ReadUInt32LittleEndian(flat.Data.AsSpan(28, 4)));
        Assert.Equal(0x00000484u, BinaryPrimitives.ReadUInt32LittleEndian(flat.Data.AsSpan(36, 4)));
        Assert.All(flat.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void Negative_winding_has_no_invented_backface_gate_and_current_matrix_padding_stays_owned()
    {
        NativeBattleGeometryRegisterStep registers = Registers();
        NativeBattleBadBreath300StripMeshStep result = Draw(Source(true), Context(), registers: registers, unknownW: true);
        Assert.Equal(156, result.Batch.AllocationBytes);
        Assert.Equal(registers.Geometry.Rotation20, result.Registers.Geometry.Rotation20);
        Assert.Equal(registers.Geometry.Translation, result.Registers.Geometry.Translation);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(result.Registers.Colors.NormalInputKnown.Value[7]);
    }

    [Fact]
    public void Near_rejection_does_not_read_color_window_or_AVSZ_and_preserves_partial_packet_prefixes()
    {
        NativeBattleEffectArena context = Context().MarkUnknown(4, 28);
        NativeBattleBadBreath300StripMeshStep result = Draw(Source(), context, Registers(0), zsf4: null);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[^1]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        });
        Assert.Null(result.Registers.Geometry.Otz);
        Assert.Throws<NotSupportedException>(() => result.Context92.Read(0x30, 4));
        Assert.NotEqual(0u, U32(result.Context92, 0x34) & 0x60000);
    }

    [Fact]
    public void Screen_rejection_reaches_fourth_projection_and_AVSZ_but_never_reads_shader_or_windows()
    {
        NativeBattleBadBreath300StripMeshStep result = Draw(Source(), Context().MarkUnknown(4, 28), projection: new(null, 100, -100, -100));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal((uint?)500, result.Registers.Geometry.Otz);
        Assert.Equal((uint?)500, result.Registers.Geometry.Mac0);
        Assert.Throws<NotSupportedException>(() => result.Context92.Read(0x30, 4));
        Assert.Equal(0x7F123456u, result.Registers.Colors.Material);
    }

    [Fact]
    public void Serialized_context_and_GTE_continue_with_the_same_next_packets_without_source_hydration()
    {
        NativeBattleBadBreath300StripMeshStep first = Draw(Source(), Context(), unknownW: true);
        NativeBattleBadBreath300StripMeshStep restored = JsonSerializer.Deserialize<NativeBattleBadBreath300StripMeshStep>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(Draw(Source(), first.Context92, first.Registers, unknownW: true)), JsonSerializer.Serialize(Draw(Source(), restored.Context92, restored.Registers, unknownW: true)));
    }

    private static NativeBattleBadBreath300StripMeshStep Draw(ImmutableArray<byte> source, NativeBattleEffectArena context, NativeBattleGeometryRegisterStep? registers = null, NativeBattleCameraProjection? projection = null, short? zsf4 = 0x100, bool unknownW = false)
    {
        NativeBattleVector[] vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        return NativeBattleBadBreath300StripMesh.Draw66F710(source, SourceAddress, context, VertexAddress, offset => offset >= 0 && offset <= 24 && (offset & 7) == 0 ? (vertices[offset / 8], unknownW ? null : (short?)0x1234) : throw new InvalidDataException(), registers ?? Registers(), projection ?? new(null, 100, 160, 108), 0, 0, zsf4, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleGeometryRegisterStep Registers(int depth = 2000)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = depth };
        return new(NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, unchecked((short)0xBEEF)), NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x7F123456 });
    }

    private static NativeBattleEffectArena Context()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 92).Write(0, BitConverter.GetBytes(SourceAddress)).Write(4, [32, 64, 96]);
        arena = arena.Write(0x0C, [10, 0]).Write(0x20, BitConverter.GetBytes(VertexAddress));
        arena = arena.Write(0x10, [0, 0, 0, 0, 0, 1, 0, 1]);
        return arena.Write(0x18, [0, 0, 0, 0, 128, 0, 0, 1]);
    }

    private static ImmutableArray<byte> Source(bool reverse = false)
    {
        byte[] bytes = new byte[132];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(92), 1);
        foreach (int at in new[] { 56, 96 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), at == 56 ? 0x2E000080u : 0x3E000080u);
            ushort[] offsets = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
            for (int index = 0; index < 4; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 4 + index * 2), offsets[index]);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 12), 0x123401FA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 16), 0x567802F0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 20), 0x04FA03FF);
            if (at == 96)
            {
                bytes[at + 24] = 64;
                bytes[at + 28] = 255;
                bytes[at + 32] = 0;
            }
        }
        return bytes.ToImmutableArray();
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
