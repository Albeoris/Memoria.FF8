using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDisolvingAcid132TemplateSpriteTests
{
    [Fact]
    public void Raw_quad_colors_signed_palette_and_reverse_count_buckets_preserve_native_order()
    {
        NativeBattleActionMatrix main = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleGfGeometryBatch batch = Draw(Resource(), 0, main, new(main, 288, 160, 108), 17);
        Assert.Equal(80, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 40 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(new[] { 19, 18 }, batch.Attempts.Select(attempt => attempt.Primitive!.Bucket).ToArray());
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.NotNull(attempt.Primitive);
            Assert.Equal(0x2C061104u, attempt.Primitive!.Words[1]);
            Assert.Equal(0xFFA0FCFAu, attempt.Primitive.Words[3]);
            Assert.Equal(0x0002FCFEu, attempt.Primitive.Words[5]);
            Assert.Equal(0x0000FFFAu, attempt.Primitive.Words[7]);
            Assert.Equal(0x0000FFFEu, attempt.Primitive.Words[9]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        });
        byte[] rotated = Resource().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(rotated.AsSpan(20 + 12), 2048);
        Assert.Equal(batch.Attempts[0].Primitive!.Words.ToArray(), Draw(rotated.ToImmutableArray(), 0, main, new(main, 288, 160, 108), 17).Attempts[0].Primitive!.Words.ToArray());
    }

    [Fact]
    public void Clipped_quads_reuse_cursor_but_keep_partial_native_stores_and_unwritten_screen_bytes()
    {
        NativeBattleActionMatrix main = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleGfGeometryBatch batch = Draw(Resource(), 0, main, new(main, 288, 160, 108), 17);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[14]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[22]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[29]);
        });
        Assert.Throws<InvalidDataException>(() => Draw(Resource().Slice(0, 30), 0, main, new(main, 288, 160, 108), 17));
        byte[] missing = Resource().ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(missing.AsSpan(20 + 14), 0x4000);
        Assert.Throws<NotSupportedException>(() => Draw(missing.ToImmutableArray(), 0, main, new(main, 288, 160, 108), 17));
    }

    [Fact]
    public void Retained_template_keeps_unknown_corner_W_and_matrix_padding_and_does_not_average_depth()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 123 } };
        NativeBattleEffectArena temporary = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(Temporary(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 }, Resource())))!;
        NativeBattleModuleSpriteDraw result = NativeBattleTemplateSprite683490.Draw(memory, temporary, new(0, 0x1000000, 0, 0, Resource()), new(null, 288, 160, 108));
        Assert.Equal(80, result.Batch.AllocationBytes);
        Assert.Equal(123u, result.Memory.GeometryRegisters!.Otz);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x76, 2));
        Assert.Throws<NotSupportedException>(() => result.Temporary180.Read(0x62, 2));
        Assert.Equal(0x1000014u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary180.Read(0x2C, 4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary180.Read(0x30, 4)));
    }

    private static NativeBattleGfGeometryBatch Draw(ImmutableArray<byte> source, ushort phase, NativeBattleActionMatrix main, NativeBattleCameraProjection projection, int tableBaseBucket)
    {
        _ = phase;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        return NativeBattleTemplateSprite683490.Draw(memory, Temporary(main, source), new(0, 0x1000000, 0, 0, source), projection, tableBaseBucket).Batch;
    }
    private static NativeBattleEffectArena Temporary(NativeBattleActionMatrix main, ImmutableArray<byte> source)
    {
        int entry = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(10));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(main, null);
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0x84, packed.Bytes.AsSpan()).MarkUnknown(0x96, 2).Write(0x20, source.AsSpan().Slice(4, 4)).Write(0x2C, BitConverter.GetBytes(checked(0x1000000u + (uint)entry + 4))).Write(0x30, source.AsSpan().Slice(entry, 4));
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            result = result.Write(offset, BitConverter.GetBytes((short)0));
        }
        return result;
    }

    private static ImmutableArray<byte> Resource()
    {
        byte[] bytes = new byte[60];
        bytes[5] = 0xFE;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 2);
        for (int index = 0; index < 2; index++)
        {
            Span<byte> quad = bytes.AsSpan(20 + index * 20, 20);
            quad[0] = 4;
            quad[1] = 17;
            quad[2] = 6;
            quad[3] = 0x2C;
            quad[4] = 250;
            quad[5] = 252;
            BinaryPrimitives.WriteUInt16LittleEndian(quad[6..], 0x20);
            BinaryPrimitives.WriteUInt16LittleEndian(quad[14..], 0x1002);
            BinaryPrimitives.WriteInt16LittleEndian(quad[16..], 4096);
            BinaryPrimitives.WriteInt16LittleEndian(quad[18..], 2048);
        }
        return bytes.ToImmutableArray();
    }
}
