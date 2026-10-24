using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64BodyDrawingTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(127, true)]
    [InlineData(128, false)]
    public void Visibility_is_signed_and_uses_128_threshold(short visibility, bool visible)
    {
        NativeBattleEffect64BodyStep result = Draw(Geometry(), visibility);
        Assert.Equal(visible ? 1 : 0, result.Batch.Attempts.Length);
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary152.Read(8, 2)));
    }

    [Fact]
    public void Zero_area_is_accepted_and_quarter_depth_drives_AVSZ()
    {
        NativeBattleEffect64BodyStep result = Draw(Geometry(), 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.Equal(6017, primitive.Bucket);
        Assert.Equal(6000u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
    }

    [Fact]
    public void Clipped_unknown_rows_do_not_require_unwritten_XY_depth_or_selector()
    {
        NativeBattleEffect64BodyStep result = Draw(Geometry(), 128);
        Assert.Empty(result.Batch.Attempts);
        for (int vertex = 0; vertex < 3; vertex++)
        {
            Assert.Equal(0x10, result.VertexScratch.Read(vertex * 8 + 6, 1)[0]);
            Assert.DoesNotContain(true, result.VertexScratch.ReadSnapshot(vertex * 8, 6).Known);
            Assert.False(result.VertexScratch.ReadSnapshot(vertex * 8 + 7, 1).Known[0]);
        }
    }

    [Fact]
    public void Source_bit15_rejects_before_invalid_corner_indices_are_read()
    {
        byte[] geometry = Geometry();
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(44), 0x7FFF);
        geometry[59] |= 0x80;
        NativeBattleEffect64BodyStep result = Draw(geometry, 0);
        Assert.Empty(result.Batch.Attempts);
    }

    private static NativeBattleEffect64BodyStep Draw(byte[] geometry, short visibility)
    {
        NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8000, 4096), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity.ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(0x2000u)).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x18, BitConverter.GetBytes(1u));
        NativeBattleScriptedGraphicsMemory controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096 };
        return NativeBattleEffect64BodyDrawing.DrawBody(memory, temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, 24), Read, 0x1000, [identity], NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes(visibility)), new(null, 100, 160, 108), controls, 0x2000, 24);
        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    private static byte[] Geometry()
    {
        byte[] data = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 3);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), 100);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28), 100);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(46), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(48), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(50), 0x0078);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 0x3D948048);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(56), 0x00B68078);
        return data;
    }
}
