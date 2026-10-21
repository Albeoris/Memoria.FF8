using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGeometryOuterProductTests
{
    [Fact]
    public void Diagonal_loader_writes_sign_extended_DWORDs_and_preserves_the_other_rotation_words_and_data_lanes()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Rotation20 = Enumerable.Repeat((byte?)0xA5, 20).ToImmutableArray(), Mac0 = 123, Otz = 456 };
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Flags = 789, Ir = [1, 2, 3] };
        NativeBattleGeometryRegisterStep loaded = NativeBattleGeometry.LoadDiagonal45DC70(new(geometry, colors), new(-1, 4096, -32768));
        Assert.Equal(new byte?[] { 255, 255, 255, 255, 165, 165, 165, 165, 0, 16, 0, 0, 165, 165, 165, 165, 0, 128, 255, 255 }, loaded.Geometry.Rotation20);
        Assert.Equal(geometry.Mac0, loaded.Geometry.Mac0);
        Assert.Equal(geometry.Otz, loaded.Geometry.Otz);
        Assert.Equal(colors, loaded.Colors);
    }

    [Fact]
    public void Look_basis_outer_product_keeps_full_MAC_and_clamps_signed_IR_without_touching_vector_or_FIFO_owners()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Mac0 = 123, Otz = 456 };
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [7, 8, 9], Material = 10, Ir0 = 11, Flags = uint.MaxValue };
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadDiagonal45DC70(new(geometry, colors), new(0, 4096, 0));
        step = NativeBattleGeometry.LoadSignedIr45DC40(step, new(-32768, 123, -9));
        step = NativeBattleGeometry.OuterProduct45E820(step);
        Assert.Equal(new int?[] { -9, 0, 32768 }, step.Colors.Mac);
        Assert.Equal(new int?[] { -9, 0, 32767 }, step.Colors.Ir);
        Assert.Equal(0x00400000u, step.Colors.Flags);
        Assert.Equal(colors.Rgb, step.Colors.Rgb);
        Assert.Equal(colors.Material, step.Colors.Material);
        Assert.Equal(colors.Ir0, step.Colors.Ir0);
        Assert.Equal(geometry.Mac0, step.Geometry.Mac0);
        Assert.Equal(geometry.Otz, step.Geometry.Otz);
        Assert.Equal(geometry.V1, step.Geometry.V1);
        Assert.Null(step.Colors.NormalInput);
    }

    [Theory]
    [InlineData(3072, 1)]
    [InlineData(-3072, -1)]
    [InlineData(2048, 0)]
    public void Outer_product_uses_native_biased_rounding_instead_of_an_arithmetic_shift(short coefficient, int expected)
    {
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadDiagonal45DC70(new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), new(0, coefficient, 0));
        step = NativeBattleGeometry.LoadSignedIr45DC40(step, new(0, 0, 1));
        step = NativeBattleGeometry.OuterProduct45E820(step);
        Assert.Equal(expected, step.Colors.Mac[0]);
        Assert.Equal(0u, step.Colors.Flags);
    }
}
