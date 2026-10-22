using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleReflectBeam135GeometryTests
{
    [Theory]
    [InlineData(181, 0, 0, 32761, 0, 0, 0u)]
    [InlineData(182, 0, 0, 33124, 0, 0, 0x81000000u)]
    [InlineData(0, 182, 0, 0, 33124, 0, 0x80800000u)]
    [InlineData(0, 0, -182, 0, 0, 33124, 0x00400000u)]
    [InlineData(65535, 65536, -32768, 1, 0, 1073741824, 0x00400000u)]
    public void SQR_uses_signed_low_WORD_full_MAC_and_exact_independent_IR_saturation_flags(int x, int y, int z, int mx, int my, int mz, uint flags)
    {
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Ir = [x, y, z], Flags = uint.MaxValue });
        NativeBattleGeometryRegisterStep result = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        Assert.Equal(new int?[] { mx, my, mz }, result.Colors.Mac);
        Assert.Equal(new int?[] { Math.Min(mx, 32767), Math.Min(my, 32767), Math.Min(mz, 32767) }, result.Colors.Ir);
        Assert.Equal(flags, result.Colors.Flags);
        Assert.Same(registers.Geometry, result.Geometry);
    }

    [Fact]
    public void Missing_reached_IR_lane_fails_without_fabricated_zero()
    {
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Ir = [1, null, 3] });
        Assert.Throws<NotSupportedException>(() => NativeBattleReflectBeam135Geometry.Square45F930(registers));
    }

    [Fact]
    public void JSON_continuation_preserves_RGB_material_IR0_and_unread_geometry_owners()
    {
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Ir = [-100, 200, 300], Rgb = [1, 2, 3], Material = 0xCAFEBABE, Ir0 = 1234 });
        NativeBattleGeometryRegisterStep restored = JsonSerializer.Deserialize<NativeBattleGeometryRegisterStep>(JsonSerializer.Serialize(registers))!;
        NativeBattleGeometryRegisterStep result = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleReflectBeam135Geometry.Square45F930(restored)));
        Assert.Equal(registers.Colors.Rgb, result.Colors.Rgb);
        Assert.Equal(registers.Colors.Material, result.Colors.Material);
        Assert.Equal(registers.Colors.Ir0, result.Colors.Ir0);
        Assert.Null(result.Colors.NormalInput);
        Assert.Equal(registers.Geometry, result.Geometry);
    }
}
