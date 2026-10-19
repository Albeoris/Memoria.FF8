using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicColorScaleTests
{
    [Theory]
    [InlineData(0, 0x3A000000u)]
    [InlineData(1, 0x3A000000u)]
    [InlineData(2048, 0x3A204080u)]
    [InlineData(4096, 0x3A4080FFu)]
    public void RawIr0UsesUnshiftedDepthFraction(short ir0, uint expected)
    {
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x3A000000, Rgb = [1, 2, 3] };
        NativeBattleCinematicColorScaleResult result = NativeBattleCinematicColorScale.ScaleRawIr0(0x3A4080FF, ir0, registers);
        Assert.Equal(expected, result.Color);
        Assert.Equal(new uint?[] { 2, 3, expected }, result.Registers.Rgb);
        Assert.Equal(0u, result.Registers.Flags);
    }

    [Theory]
    [InlineData(256, 0x2C80C0FFu, 0x3480C0FFu, 0u)]
    [InlineData(512, 0x2C0080FEu, 0x34FFFFFFu, 0x00380000u)]
    [InlineData(-256, 0x2C804001u, 0x34000000u, 0x00380000u)]
    public void ReturnedIrBytesDifferFromRgbFifoClamping(short factor, uint expectedColor, uint expectedFifo, uint flags)
    {
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x34112233, Rgb = [0x11111111, 0x22222222, 0x33333333] };
        NativeBattleCinematicColorScaleResult result = NativeBattleCinematicColorScale.ScaleB66A20(0x2C80C0FF, factor, registers);
        Assert.Equal(expectedColor, result.Color);
        Assert.Equal(new uint?[] { 0x22222222, 0x33333333, expectedFifo }, result.Registers.Rgb);
        Assert.Equal(flags, result.Registers.Flags);
        Assert.Equal(registers.Material, result.Registers.Material);
        Assert.Equal(registers.NormalInput, result.Registers.NormalInput);
    }

    [Fact]
    public void FactorShiftsThroughSignedIr0WordAndUnknownCodeStaysUnknown()
    {
        NativeBattleCinematicColorScaleResult result = NativeBattleCinematicColorScale.ScaleB66A20(0x2C000001, 2048, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(0x2C0000F8u, result.Color);
        Assert.Equal(new int?[] { -8, 0, 0 }, result.Registers.Mac);
        Assert.Equal(new int?[] { -8, 0, 0 }, result.Registers.Ir);
        Assert.Null(result.Registers.Rgb[2]);
        Assert.Equal(0x00200000u, result.Registers.Flags);
    }
}
