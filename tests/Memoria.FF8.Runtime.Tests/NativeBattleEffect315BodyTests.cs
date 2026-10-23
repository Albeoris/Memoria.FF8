using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect315BodyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raw_faces_project_world_rows_and_preserve_signed_uv_tail_and_full_depth_FIFO(bool quad)
    {
        NativeBattleEffect315BodyDrawingStep result = Draw(Geometry(quad), Temporary());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(quad ? 40 : 32, result.Batch.AllocationBytes);
        Assert.Equal(quad ? 0x2Cu : 0x24u, primitive.Words[1] >> 24);
        Assert.Equal(quad ? new[] { 8000, 8000, 8000, 8000 } : new[] { 8000, 8000, 8000, 0 }, primitive.VertexPayload4);
        Assert.Equal((short)-1000, S16(result.Scratch, 2));
        Assert.Equal((short)0, S16(result.Scratch, 6));
        Assert.Equal((ushort)1, U16(result.Temporary156, quad ? 0xA : 8));
        Assert.Equal(quad ? 0x80488078u : 0xFFFF8078u, U32(attempt, 28));
        if (quad)
        {
            Assert.Equal(0xFFFF8048u, U32(attempt, 36));
        }
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(8000, NativeBattleGeometry.CaptureMatrix(result.Memory.GeometryRegisters!).Z);
        Assert.Equal(Identity().ReadMatrix().Rotation.ToArray(), NativeBattleGeometry.CaptureMatrix(result.Memory.GeometryRegisters!).Rotation.ToArray());
    }

    [Fact]
    public void Zero_area_rejects_after_early_header_color_uv_stores_without_advancing_cursor()
    {
        byte[] geometry = Geometry(false);
        geometry.AsSpan(14, 18).Clear();
        NativeBattleEffect315BodyDrawingStep result = Draw(geometry, Temporary());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary156.Read(0x44, 4)));
        Assert.Equal(0x07000000u, U32(attempt, 0));
        Assert.Equal(0x24123456u, U32(attempt, 4));
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[20]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[28]);
    }

    [Theory]
    [InlineData(1000, 0)]
    [InlineData(50, 50)]
    public void Deformation_uses_normalized_world_direction_for_both_radius_branches(int radius, short expectedX)
    {
        byte[] geometry = Geometry(false);
        W(geometry, 14, 100);
        W(geometry, 16, 100);
        NativeBattleEffectArena temporary = Temporary().Write(0x34, BitConverter.GetBytes(radius)).Write(0x76, new byte[2]);
        NativeBattleEffect315BodyDrawingStep result = Draw(geometry, temporary);
        Assert.Equal(expectedX, S16(result.Scratch, 0));
        Assert.Equal((short)0, S16(result.Scratch, 2));
        Assert.Equal((short)0, S16(result.Scratch, 4));
        Assert.Equal((short)0, S16(result.Scratch, 6));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), result.Scratch.Read(24, 8));
    }

    [Fact]
    public void Zero_world_height_sets_semibit_and_adds_blend_page_before_rejection()
    {
        NativeBattleEffect315BodyDrawingStep result = Draw(Geometry(false), Temporary().Write(0x76, new byte[2]));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(0x26123456u, U32(attempt, 4));
        Assert.Equal(0x00D68078u, U32(attempt, 20));
    }

    [Fact]
    public void Reached_inner_division_does_not_hide_a_zero_divisor()
    {
        byte[] geometry = Geometry(false);
        W(geometry, 16, 100);
        Assert.Throws<DivideByZeroException>(() => Draw(geometry, Temporary().Write(0x76, new byte[2]).Write(0x3C, new byte[4])));
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0x20, false, true)]
    [InlineData(0x200, true, false)]
    public void Outer_keeps_world_bone_basis_conditional_shadow_and_linked_filter(ushort flags, bool shadow, bool linked)
    {
        NativeBattleGraphicsModel model = new(3, flags) { Color40 = 0x123456, Alpha7 = 77, GeometryWorldMatrix = Identity(100).ReadMatrix(), SequenceState = new() { MeshMask124 = 3 }, LinkedTransform = new(new(0, 0, 0), new(0, 0, 0)) };
        List<string> calls = [];
        NativeBattleEffect315BodyServices services = new(current =>
        {
            calls.Add("shadow");
            return current;
        }, (current, basis) =>
        {
            calls.Add("compose");
            Assert.Equal(100, basis.ReadMatrix().Z);
            Assert.Equal(8100u, BinaryPrimitives.ReadUInt32LittleEndian(current.Temporary156.Read(0x98, 4)));
            return current;
        }, current =>
        {
            calls.Add("draw");
            Assert.Equal(new byte[] { 77, 77, 77, 0xA5 }, current.Temporary156.Read(0x28, 4));
            Assert.Equal(0x2211u, BinaryPrimitives.ReadUInt32LittleEndian(current.Temporary156.Read(0x10, 4)));
            return current;
        }, current =>
        {
            calls.Add("restore");
            return current;
        })
        {
            DrawLinkedAndDeliver = (current, basis) =>
            {
                calls.Add("linked");
                Assert.Equal(100, basis.ReadMatrix().Z);
                Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(current.Temporary156.Read(0x20, 4)));
                return current;
            }
        };
        NativeBattleEffect315BodyFrame frame = new(Memory(), NativeBattleFileStaging.Unknown(), model, Temporary().Write(0x28, Enumerable.Repeat((byte)0xA5, 4).ToArray()), 20);
        frame = NativeBattleEffect315BodyDrawing.DrawOuter(frame, Identity(8000), 0x2000, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x2211u)), services);
        Assert.Equal(shadow, calls.Contains("shadow"));
        Assert.Equal(linked, calls.Contains("linked"));
        Assert.True(calls.IndexOf("draw") < calls.IndexOf("restore"));
        Assert.Equal(4096, BinaryPrimitives.ReadInt32LittleEndian(frame.Temporary156.Read(0x2C, 4)));
    }

    [Fact]
    public void Outer_copies_unconsumed_renderer_mode_DWORD_with_actual_partial_knownness()
    {
        NativeBattleGraphicsModel model = new(3, 0x20) { Color40 = 0x123456, Alpha7 = 77, GeometryWorldMatrix = Identity().ReadMatrix(), SequenceState = new() { MeshMask124 = 1 } };
        NativeBattleEffect315BodyFrame frame = new(Memory(), NativeBattleFileStaging.Unknown(), model, Temporary(), 20);
        NativeBattleEffectArena primitiveFlags = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(1, [0x22]);
        NativeBattleEffect315BodyServices services = new(current => current, (current, basis) => current, current => current, current => current);
        frame = NativeBattleEffect315BodyDrawing.DrawOuter(frame, Identity(8000), 0x2000, primitiveFlags, services);
        (byte[] bytes, bool[] known) = frame.Temporary156.ReadSnapshot(0x10, 4);
        Assert.Equal(new[] { false, true, false, false }, known);
        Assert.Equal((byte)0x22, bytes[1]);
    }

    private static NativeBattleEffect315BodyDrawingStep Draw(byte[] geometry, NativeBattleEffectArena temporary)
    {
        NativeBattleScriptedGraphicsMemory controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096, Zsf4 = 4096 };
        return NativeBattleEffect315BodyDrawing.DrawBody(Memory(), temporary, (address, length) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan().Slice(checked((int)(address - 0x1000)), length)), 0x1000, [Identity()], Identity(8000), new(null, 100, 160, 108), controls, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 32).ToArray()), 0x2000, 32);
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity().ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 156).Write(4, BitConverter.GetBytes(0x2000u)).Write(0x1C, BitConverter.GetBytes(0x00123456u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0x2C, BitConverter.GetBytes(4096)).Write(0x30, new byte[4]).Write(0x34, BitConverter.GetBytes(1000)).Write(0x38, BitConverter.GetBytes(10)).Write(0x3C, BitConverter.GetBytes(10)).Write(0x74, new byte[8]).Write(0x76, BitConverter.GetBytes((short)1000));
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short S16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static uint U32(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset, 4));

    private static byte[] Geometry(bool quad)
    {
        int vertices = quad ? 4 : 3;
        int countOffset = (14 + vertices * 6 + 3) & ~3;
        byte[] bytes = new byte[countOffset + 12 + (quad ? 20 : 16)];
        D(bytes, 0, 1);
        D(bytes, 4, 8);
        W(bytes, 8, 1);
        W(bytes, 12, vertices);
        W(bytes, 20, 100);
        W(bytes, 28, 100);
        if (quad)
        {
            W(bytes, 32, 100);
            W(bytes, 34, 100);
        }
        W(bytes, countOffset + (quad ? 2 : 0), 1);
        int face = countOffset + 12;
        W(bytes, face + 2, 1);
        W(bytes, face + 4, 2);
        W(bytes, face + 6, quad ? 3 : 0x8078);
        D(bytes, face + 8, 0x3D948048);
        D(bytes, face + 12, 0x00B68078);
        if (quad)
        {
            W(bytes, face + 16, 0x8078);
            W(bytes, face + 18, 0x8048);
        }
        return bytes;
    }

    private static void W(byte[] data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), checked((ushort)value));
    private static void D(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
