using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCactuarAuxiliaryDrawingTests
{
    private static NativeBattleActionMatrix View => new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 70, -90, 4096, 4096);

    [Theory]
    [InlineData(0x5A9140, 5, 0x33, 0)]
    [InlineData(0x5A9140, 6, 0xF3, 0)]
    [InlineData(0x5A9140, 9, 0xF3, 2046)]
    [InlineData(0x5A9570, -1, 0xF3, 5120)]
    [InlineData(0x5A9570, 3, 0xF3, 1024)]
    [InlineData(0x5A9570, 4, 0x33, 0)]
    [InlineData(0x5A9730, 7, 0xF3, 512)]
    [InlineData(0x5A9730, 8, 0x33, 0)]
    [InlineData(0x5A9730, 13, 0x33, 0)]
    [InlineData(0x5A9730, 14, 0xF3, 0)]
    [InlineData(0x5A9730, 16, 0xF3, 818)]
    public void Mesh_callbacks_keep_their_signed_counter_fade_intervals(int callback, int counter, int flags, int fade)
    {
        NativeBattleModulePoolTask task = Task((uint)callback, counter);
        NativeBattleCactuarMeshDraw draw = NativeBattleCactuarAuxiliaryDrawing.DescribeMesh(task, Mesh((uint)callback), -1590, View);
        Assert.Equal((uint)flags, draw.Context.Flags);
        Assert.Equal(fade, draw.Context.Fade);
        Assert.Equal(0u, draw.Context.FarColor);
        Assert.Null(draw.Context.TexturePage);
        Assert.Equal(17, draw.Context.TableBaseBucket);
        Assert.Equal((byte)2, draw.Context.DepthShift);
    }

    [Theory]
    [InlineData(-1590, 4096)]
    [InlineData(1590, -4096)]
    [InlineData(0, 0)]
    [InlineData(1048576, 0)]
    [InlineData(2147483647, 2)]
    [InlineData(-2147483647, -2)]
    public void Model_Y_division_preserves_native_DWORD_negate_shift_and_signed_truncation(int modelY, int expected)
    {
        Assert.Equal(expected, NativeBattleCactuarAuxiliaryDrawing.VerticalScale5A9140(modelY));
    }

    [Fact]
    public void Ground_mesh_uses_Y_rotation_then_column_scale_before_actual_view_composition()
    {
        NativeBattleModulePoolTask task = Task(0x5A9140, 3).WriteWord(0x18, 1024).WriteWord(0x1C, 2048);
        NativeBattleCactuarMeshDraw draw = NativeBattleCactuarAuxiliaryDrawing.DescribeMesh(task, Mesh(task.Callback), -1590, View);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0)), 2048, 4096, 2048) with { X = 100, Y = -200, Z = 300 };
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Compose56C2F0(View, local);
        Assert.Equal(expected.Rotation.ToArray(), draw.Matrix.Rotation.ToArray());
        Assert.Equal(expected.X, draw.Matrix.X);
        Assert.Equal(expected.Y, draw.Matrix.Y);
        Assert.Equal(expected.Z, draw.Matrix.Z);
        Assert.Throws<InvalidDataException>(() => NativeBattleCactuarAuxiliaryDrawing.DescribeMesh(task, Mesh(0x5A9570), 0, View));
    }

    private static NativeBattleModulePoolTask Task(uint callback, int counter) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[0x20].ToImmutableArray()).WriteWord(0xC, unchecked((ushort)counter)).WriteWord(0x10, 100).WriteWord(0x12, unchecked((ushort)-200)).WriteWord(0x14, 300).WriteWord(0x1C, 4096);
    private static NativeBattleStaticMeshResource Mesh(uint callback) => new(0, callback == 0x5A9140 ? 0xCF26F8u : callback == 0x5A9570 ? 0xCF08ECu : 0xCF11BCu, []);
}
