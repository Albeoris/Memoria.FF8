using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleThundaraDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 454)]
    [InlineData(12, 4096)]
    public void Angled_mesh_helper_retains_the_signed_division_fade_boundary(int phase, int fade)
    {
        NativeBattleThundaraGeometry operation = new(0x6DCD90, new(10, 20, 30, 0), 512, phase, 12);
        NativeBattleThundaraMeshDraw draw = NativeBattleThundaraDrawing.DescribeGeometry(operation, Resources(), View);
        Assert.Equal(fade, draw.Context.Fade);
        Assert.Equal(0xF3u, draw.Context.Flags);
        Assert.Equal((uint?)0, draw.Context.FarColor);
        Assert.Equal(10, draw.Matrix.X);
        Assert.Equal(20, draw.Matrix.Y);
        Assert.Equal(4126, draw.Matrix.Z);
        Assert.Equal((byte)1, draw.Resource.Id);
    }

    [Fact]
    public void Angled_mesh_matrix_preserves_the_native_sine_sign_and_WORD_stores()
    {
        NativeBattleThundaraGeometry positive = new(0x6DCD90, new(0, 0, 0, 0), 512, 6, 12);
        NativeBattleThundaraMeshDraw first = NativeBattleThundaraDrawing.DescribeGeometry(positive, Resources(), View);
        NativeBattleThundaraMeshDraw second = NativeBattleThundaraDrawing.DescribeGeometry(positive with { AngleOrBaseY = -512 }, Resources(), View);
        Assert.Equal(new short[] { 2896, 0, 0, 0, 2047, -2047, 0, 2047, 2047 }, first.Matrix.Rotation.ToArray());
        // SAR12 rounds -8,386,816 down to -2048 before the matrix's negated WORD store.
        Assert.Equal((short)2048, second.Matrix.Rotation[5]);
        Assert.Equal((short)-2048, second.Matrix.Rotation[7]);
    }

    [Fact]
    public void Distinct_diagonal_helpers_use_the_actual_baseY_instead_of_the_primary_Y()
    {
        NativeBattleModuleAnchor position = new(10, 1234, 20, 0);
        NativeBattleThundaraMeshDraw column = NativeBattleThundaraDrawing.DescribeGeometry(new(0x6DCEE0, position, -50, 6, 12), Resources(), View);
        Assert.Equal(new short[] { 1236, 0, 0, 0, 4962, 0, 0, 0, 1236 }, column.Matrix.Rotation.ToArray());
        Assert.Equal(-50, column.Matrix.Y);
        Assert.Equal(2896, column.Context.Fade);
        Assert.Equal((byte)2, column.Resource.Id);
        NativeBattleThundaraMeshDraw sphere = NativeBattleThundaraDrawing.DescribeGeometry(new(0x6DCFE0, position, -70, 8, 16), Resources(), View);
        Assert.Equal(new short[] { 2896, 0, 0, 0, 2896, 0, 0, 0, 2896 }, sphere.Matrix.Rotation.ToArray());
        Assert.Equal(-70, sphere.Matrix.Y);
        Assert.Equal((byte)3, sphere.Resource.Id);
    }

    [Fact]
    public void Explicit_geometry_composes_the_existing_view_without_a_billboard_roll_stage()
    {
        NativeBattleActionMatrix view = View with { Rotation = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 256)).Rotation };
        NativeBattleThundaraGeometry operation = new(0x6DCFE0, new(0, 0, 0, 0), 0, 16, 16);
        NativeBattleThundaraMeshDraw descriptor = NativeBattleThundaraDrawing.DescribeGeometry(operation, Resources(), view);
        Assert.Equal(view.Rotation.ToArray(), descriptor.Matrix.Rotation.ToArray());
        NativeBattleGfGeometryBatch packets = NativeBattleThundaraDrawing.Geometry(operation with { Phase = 8 }, Resources(), Projection, View, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Contains(packets.Attempts, attempt => attempt.Primitive is not null);
        Assert.Throws<InvalidDataException>(() => NativeBattleThundaraDrawing.DescribeGeometry(operation with { Duration = 0 }, Resources(), View));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraDrawing.DescribeGeometry(operation with { Helper = 0 }, Resources(), View));
    }

    [Fact]
    public void Sprite_receipts_preserve_explicit_palette_color_frame_and_camera_roll_contract()
    {
        NativeBattleThundaraSprite operation = new(0x131F244, 19, new(0, 0, 0, 0), 4096, -1024, 4, 0x202020);
        NativeBattleModulePacketBatch result = NativeBattleThundaraDrawing.Sprite(operation, Resources(), Projection, View, 200);
        NativeBattleAllocatedPrimitive allocation = Assert.Single(result.Primitives);
        Assert.Equal(0x2C202020u, allocation.Primitive.Words[1]);
        Assert.Equal(40, result.AllocationBytes);
        Assert.Throws<InvalidDataException>(() => NativeBattleThundaraDrawing.Sprite(operation with { Frame = 20 }, Resources(), Projection, View, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraDrawing.Sprite(operation with { ResourceAddress = 0 }, Resources(), Projection, View, 0));
    }

    [Fact]
    public void Fullscreen_flash_uses_its_private_cursor_exact_320_by_216_polygon_and_bucket_eight()
    {
        NativeBattleModulePacketBatch result = NativeBattleThundaraDrawing.Flash(Resources(), 0x8000);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Primitives).Primitive;
        Assert.Equal(24, result.AllocationBytes);
        Assert.Equal(8, primitive.Bucket);
        Assert.Equal(new uint[] { 0x5000000, 0x2A808080, 0, 320, 216u << 16, 216u << 16 | 320 }, primitive.Words.ToArray());
        Assert.All(primitive.PhysicalWriteMask!.Value, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraDrawing.Flash(Resources(), 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleThundaraDrawing.Flash(Resources() with { FlashColor = 0x123456 }, 0));
    }

    private static NativeBattleThundaraResources Resources()
    {
        byte[] bytes = new byte[76];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 20);
        // Put the shared synthetic frame after the complete21-entry directory.
        for (int frame = 0; frame <= 20; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), 52);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 1);
        bytes[56] = 1;
        bytes[57] = 128;
        bytes[58] = 1;
        bytes[59] = 0x2C;
        return new(new("animation", []), [new(1, 0x131F244, 0, 0, bytes.ToImmutableArray())], Mesh(1), Mesh(2), Mesh(3), 1, 0x808080);
    }

    private static NativeBattleStaticMeshResource Mesh(byte id)
    {
        byte[] bytes = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        Put(8, -64, -64);
        Put(16, 64, -64);
        Put(24, -64, 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 0x20808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 4);
        return new(id, id, bytes.ToImmutableArray());

        void Put(int offset, short x, short y)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
        }
    }
}
