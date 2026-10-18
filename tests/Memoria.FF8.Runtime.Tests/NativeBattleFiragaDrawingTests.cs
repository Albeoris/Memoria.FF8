using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFiragaDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleFiragaState State => new([new(30, -40, 50, 1), null, null]);

    [Theory]
    [InlineData(-32768, 0)]
    [InlineData(-2000, 0)]
    [InlineData(-1999, 1)]
    [InlineData(0, 160)]
    [InlineData(1000, 240)]
    [InlineData(2000, 64)]
    [InlineData(3000, 144)]
    public void Surface_ground_RGB_uses_nested_signed_truncation_and_low_BYTE_wrap(int y, int intensity)
    {
        NativeBattleModulePoolTask task = Task(0x61C6B0, 0).WriteWord(18, unchecked((ushort)y)).WriteWord(22, 75).WriteWord(28, 4096);
        (NativeBattleModuleDraw billboard, NativeBattleModuleDraw ground) = NativeBattleFiragaDrawing.Surface(task, Resources());
        Assert.Equal(-256, billboard.DepthOffset);
        Assert.Equal((short)y, billboard.Anchor!.Y);
        Assert.Equal((short)75, ground.Anchor!.Y);
        Assert.Equal((ushort)0xC, ground.SpriteFlags);
        Assert.Equal((uint)intensity * 0x10101, ground.SpriteColor);
        Assert.Equal(0, ground.DepthOffset);
        Assert.Equal(9, ground.SpriteWorldRotation.Length);
    }

    [Fact]
    public void Surface_submits_billboard_then_ground_with_the_same_original_frame()
    {
        NativeBattleModulePoolTask task = Task(0x61C6B0, 15).WriteWord(28, 4096);
        NativeBattleFiragaDrawResult result = NativeBattleFiragaDrawing.Draw(task, State, Resources(), Projection, View, 200, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(result.Mesh);
        Assert.Equal(2, result.Sprites.Length);
        Assert.Equal(0x2C808080u, Assert.Single(result.Sprites[0].Primitives).Primitive.Words[1]);
        Assert.Equal(0x2CA0A0A0u, Assert.Single(result.Sprites[1].Primitives).Primitive.Words[1]);
        (NativeBattleModuleDraw billboard, NativeBattleModuleDraw ground) = NativeBattleFiragaDrawing.Surface(task, Resources());
        Assert.Same(billboard.SpriteFrame, ground.SpriteFrame);
        Assert.Equal((ushort)15, ground.Phase);
    }

    [Fact]
    public void Portal_uses_the_actual_billboard_matrix_and_depth_without_sprite_counterroll()
    {
        NativeBattleActionMatrix rotatedView = View with { Rotation = NativeBattleGfMatrices.Euler56CD50(new(0, 512, 0)).Rotation };
        NativeBattleModulePoolTask task = Task(0x61CB10, 8).WriteWord(28, 4096);
        NativeBattleFiragaMeshDraw portal = NativeBattleFiragaDrawing.Mesh(task, State, Resources(), rotatedView);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, portal.Matrix.Rotation.ToArray());
        Assert.Equal(3968, portal.Matrix.Z);
        Assert.Equal(0x30u, portal.Context.Flags);
        Assert.Null(portal.Context.Fade);
        Assert.Equal((byte)2, portal.Resource.Id);
        NativeBattleFiragaDrawResult emitted = NativeBattleFiragaDrawing.Draw(task, State, Resources(), Projection, rotatedView, 777, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Empty(emitted.Sprites);
        Assert.Contains(emitted.Mesh!.Attempts, attempt => attempt.Primitive is not null);
    }

    [Theory]
    [InlineData(0x61C870u, 9, 0x33, -1)]
    [InlineData(0x61C870u, 10, 0xF3, 0)]
    [InlineData(0x61C870u, 19, 0xF3, 3681)]
    [InlineData(0x61C9C0u, 13, 0x30, -1)]
    [InlineData(0x61C9C0u, 14, 0xF0, 0)]
    [InlineData(0x61C9C0u, 23, 0xF0, 3681)]
    public void Ring_and_flare_keep_their_distinct_material_and_opacity_boundaries(uint callback, int counter, int flags, int opacity)
    {
        NativeBattleFiragaMeshDraw draw = NativeBattleFiragaDrawing.Mesh(Task(callback, counter).WriteWord(28, 4096), State, Resources(), View);
        Assert.Equal((uint)flags, draw.Context.Flags);
        Assert.Equal(opacity < 0 ? null : (int?)opacity, draw.Context.Fade);
        Assert.Equal(opacity < 0 ? null : (uint?)0, draw.Context.FarColor);
        Assert.Equal(17, draw.Context.TableBaseBucket);
        Assert.Equal((byte)2, draw.Context.DepthShift);
    }

    [Theory]
    [InlineData(0, 0xF0, 4096)]
    [InlineData(5, 0xF0, 686)]
    [InlineData(6, 0x30, -1)]
    [InlineData(13, 0x30, -1)]
    [InlineData(14, 0xF0, 0)]
    [InlineData(19, 0xF0, 3410)]
    public void Orbit_uses_retained_cached_position_and_exact_opacity_intervals(int counter, int flags, int opacity)
    {
        NativeBattleModulePoolTask task = Orbit(counter);
        NativeBattleFiragaMeshDraw draw = NativeBattleFiragaDrawing.Mesh(task, State, Resources(), View);
        Assert.Equal((uint)flags, draw.Context.Flags);
        Assert.Equal(opacity < 0 ? null : (int?)opacity, draw.Context.Fade);
        Assert.Equal(30, draw.Matrix.X);
        Assert.Equal(-40, draw.Matrix.Y);
        Assert.Equal(4146, draw.Matrix.Z);
        Assert.Equal((byte)3, draw.Resource.Id);
        Assert.Throws<NotSupportedException>(() => NativeBattleFiragaDrawing.Mesh(task.WriteWord(14, 1), State, Resources(), View));
    }

    [Fact]
    public void Orbit_alignment_consumes_the_raw_rotated_direction_without_an_extra_normalization()
    {
        NativeBattleModulePoolTask task = Orbit(7).WriteWord(22, 768).WriteWord(24, 1000).WriteWord(26, 2000).WriteWord(28, unchecked((ushort)-500));
        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(768, new(1000, 2000, -500)), 0, 0, 0, 4096);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(rotation, 1000, -3800, 1000);
        NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(0, -4096, 0, x, y, z);
        NativeBattleActionMatrix expected = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
        expected = NativeBattlePoseMatrices.Scale56BEF0(expected, 8192, 4096, 8192);
        Assert.Equal(expected.Rotation.ToArray(), NativeBattleFiragaDrawing.Mesh(task, State, Resources(), View).Matrix.Rotation.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleFiragaDrawing.Mesh(Task(0x61C0A0, 0), State, Resources(), View));
    }

    private static NativeBattleModulePoolTask Orbit(int counter)
    {
        return Task(0x61CBB0, counter).WriteWord(16, 1000).WriteWord(18, unchecked((ushort)-3800)).WriteWord(20, 1000).WriteWord(26, 4096).WriteWord(34, 4096);
    }

    private static NativeBattleModulePoolTask Task(uint callback, int counter)
    {
        return new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(12, unchecked((ushort)counter));
    }

    private static NativeBattleFiragaResources Resources()
    {
        return new(new("animation", []), Sprite(), Mesh(1), Mesh(2), Mesh(3), Mesh(4), 1);
    }

    private static NativeBattleModuleResource Sprite()
    {
        const int start = 44;
        byte[] bytes = new byte[start + 24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
        for (int frame = 0; frame <= 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), start);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start), 1);
        bytes[start + 4] = 1;
        bytes[start + 5] = 128;
        bytes[start + 6] = 1;
        bytes[start + 7] = 0x2C;
        return new(1, 1, 0, 0, bytes.ToImmutableArray());
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
