using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect209ModeMesh96Tests
{
    [Theory]
    [InlineData(0x33u)]
    [InlineData(0xF3u)]
    public void Selected_mixed_groups_keep_flat_and_Gouraud_inline_modes_and_explicit_depths(uint flags)
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleEffect209ModeMesh96Step result = Draw(mesh, Context(flags));
        Assert.Equal(80, result.Batch.Attempts.Length);
        Assert.Equal(2880, result.Batch.AllocationBytes);
        for (int index = 0; index < 80; index++)
        {
            NativeBattleGfGeometryAttempt attempt = result.Batch.Attempts[index];
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            bool triangle = index < 16;
            bool flat = index < 48;
            int bytes = triangle ? 28 : flat ? 32 : 44;
            Assert.Equal(triangle ? 0x06000000u : flat ? 0x07000000u : 0x0A000000u, primitive.Words[0]);
            Assert.Equal(0xE1000240u, primitive.Words[1]);
            Assert.Equal(0xE1000220u, primitive.Words[bytes / 4 - 1]);
            Assert.Equal(triangle ? new[] { 2000, 2000, 2000, 0 } : new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
            Assert.Equal(triangle ? 141 : 142, primitive.Bucket);
            uint code = triangle ? 0x22000000u : flat ? 0x2A000000u : 0x3A000000u;
            Assert.Equal(code | (flags == 0xF3 ? 0x00081119u : 0x00112233u), primitive.Words[2]);
            Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context96, 0x20));
        Assert.Equal(0u, U(result.Context96, 0x18));
        Assert.False(result.Context96.ReadSnapshot(0x28, 1).Known[0]);
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        if (flags == 0xF3)
        {
            Assert.Equal(new uint?[] { 0x00223344, 0x00334455, 0x3A081119 }, result.Memory.ScriptedColorRegisters!.Rgb.ToArray());
            Assert.Equal(0x3A112233u, result.Memory.ScriptedColorRegisters.Material);
        }
    }

    [Fact]
    public void Zero_area_retains_modes_and_context_OTZ_while_publishing_tag_color_and_NCLIP()
    {
        NativeBattleEffect209ModeMesh96Step result = Draw(Mesh(zeroArea: true), Context(0xF3));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        });
        Assert.Equal(0u, U(result.Context96, 0x24));
        Assert.DoesNotContain(true, result.Context96.ReadSnapshot(0x2C, 4).Known);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Viewport_reject_occurs_after_SXY_and_average_but_before_modes_colorcue_and_context_OTZ()
    {
        NativeBattleEffect209ModeMesh96Step result = Draw(Mesh(outside: true), Context(0xF3));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Otz);
        Assert.DoesNotContain(true, result.Context96.ReadSnapshot(0x2C, 4).Known);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[^1]);
        });
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Fourth_vertex_projection_does_not_repeat_the_first_three_FLAG_admission_test()
    {
        NativeBattleEffect209ModeMesh96Step result = Draw(Mesh(fourthBehind: true), Context(0x33));
        NativeBattleGeometryPrimitive firstQuad = Assert.IsType<NativeBattleGeometryPrimitive>(result.Batch.Attempts[16].Primitive);
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, firstQuad.VertexPayload4);
        Assert.Equal(32, result.Batch.Attempts[16].PacketLength);
        Assert.Equal(2880, result.Batch.AllocationBytes);
    }

    [Fact]
    public void Selected_flags_admit_reversed_winding_without_changing_current_source_or_snapshot()
    {
        NativeBattleStaticMeshResource mesh = Mesh(reverse: true);
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory)))!);
        NativeBattleEffect209ModeMesh96Step first = NativeBattleEffect209ModeMesh96.DrawLoaded(memory, mesh, new(null, 100, 160, 108), Context(0xF3), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleEffect209ModeMesh96Step second = NativeBattleEffect209ModeMesh96.DrawLoaded(restored, mesh, new(null, 100, 160, 108), Context(0xF3), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(2880, first.Batch.AllocationBytes);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first.Batch)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second.Batch)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first.Memory.GeometryRegisters)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second.Memory.GeometryRegisters)));
    }

    private static NativeBattleEffect209ModeMesh96Step Draw(NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context) => NativeBattleEffect209ModeMesh96.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 96).Write(0, BitConverter.GetBytes(0x10000000u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048u)).Write(0x18, BitConverter.GetBytes(0x12345678u)).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x58, BitConverter.GetBytes(0xE1000240u)).Write(0x5C, BitConverter.GetBytes(0xE1000220u));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleStaticMeshResource Mesh(bool zeroArea = false, bool outside = false, bool reverse = false, bool fourthBehind = false)
    {
        byte[] bytes = new byte[2032];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 656);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 81);
        NativeBattleVector[] points = zeroArea ? [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)] : outside ? [new(7000, -400, 0), new(7800, -400, 0), new(7400, 400, 0), new(7800, 400, 0)] : [new(-400, -400, 0), new(400, -400, 0), new(0, 400, 0), new(400, 400, fourthBehind ? (short)-2500 : (short)0)];
        for (int index = 0; index < 4; index++)
        {
            int offset = 8 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), points[index].Z);
        }
        int cursor = 656;
        for (int group = 0; group < 8; group++)
        {
            int count = group == 0 ? 16 : group is 1 or 5 ? 32 : 0;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), count);
            cursor += 4;
            for (int face = 0; face < count; face++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), group == 0 ? 0x20112233u : group == 1 ? 0x28112233u : 0x38112233u);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), (ushort)(reverse ? 4 : 2));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 8), (ushort)(reverse ? 2 : 4));
                if (group != 0)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                }
                if (group == 5)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x00224466);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x00446688);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x006688AA);
                }
                cursor += group == 5 ? 24 : 12;
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
