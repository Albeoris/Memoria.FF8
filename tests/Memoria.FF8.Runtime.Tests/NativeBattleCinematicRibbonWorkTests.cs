using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicRibbonWorkTests
{
    [Fact]
    public void CrossSectionPreservesPaddingAndUsesSignedInnerWidthWithTransformSaturation()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        int output = 0x4000;
        frame = frame with { Arena = frame.Arena with { UnknownRegions = Enumerable.Range(0, 9).Select(index => new NativeBattleEffectUnknownRegion(output + index * 8 + 6, 2)).ToImmutableArray() } };
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 32760, 7, 90, 4096);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicRibbonWork.BuildCrossSection(frame, matrix, 11, 15, NativeBattleEffectNodeMemory.ArenaAddress + (uint)output, true);
        Assert.Equal(new short[] { 32760, 7, 75 }, Vector(0));
        Assert.Equal(new short[] { 32749, 7, 75 }, Vector(1));
        Assert.Equal(new short[] { 32767, 7, 75 }, Vector(2));
        Assert.Equal(new short[] { 32756, 7, 75 }, Vector(3));
        Assert.Equal(new short[] { 32764, 7, 75 }, Vector(4));
        Assert.Equal(new short[] { 32760, 18, 75 }, Vector(6));
        Assert.Equal(new short[] { 32760, 11, 75 }, Vector(8));
        for (int index = 0; index < 9; index++)
        {
            Assert.Throws<NotSupportedException>(() => result.Arena.Read(output + index * 8 + 6, 2));
        }
        Assert.Equal(0u, result.ReadTemporaryDword(0xFC));
        Assert.Equal((ushort)4, result.ReadTemporaryWord(0xF2));
        Assert.Equal(unchecked((ushort)-15), result.ReadTemporaryWord(0xF4));

        short[] Vector(int index)
        {
            byte[] bytes = result.Arena.Read(output + index * 8, 6);
            return Enumerable.Range(0, 3).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(lane * 2))).ToArray();
        }
    }

    [Fact]
    public void RampKeepsPlateauInputCodesAndOnlyScaledStepsAdvanceFifo()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0xF0, 2).WriteTemporaryDword(0xF4, 3).WriteTemporaryDword(0xF8, 5);
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [1, 2, 3] };
        NativeBattleCinematicRibbonRampResult result = NativeBattleCinematicRibbonWork.BuildColorRamp(frame, initial, 0x00102040, 0x004080FE, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000);
        uint[] words = Enumerable.Range(0, 20).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x4000 + index * 4, 4))).ToArray();
        Assert.Equal(new uint[] { 0x3A000000, 0x3A000000, 0x3A081020, 0x3A20407F, 0x38102040, 0x384080FE, 0x3A102040, 0x3A4080FE, 0x3A081020, 0x3A20407F }, words.Take(10));
        Assert.All(words.Skip(10), word => Assert.Equal(0x3A000000u, word));
        Assert.Equal(new uint?[] { 0x3A4080FE, 0x3A081020, 0x3A20407F }, result.Registers.Rgb);
        Assert.Equal(0x3A000000u, result.Registers.Material);
        Assert.Equal(0u, result.Registers.Flags);
        Assert.Equal(0x38102040u, result.Frame.ReadTemporaryDword(0xE0));
    }

    [Fact]
    public void RampWithNoScalingPreservesExistingFlagsAndRgb()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0xF0, 0).WriteTemporaryDword(0xF4, 1).WriteTemporaryDword(0xF8, 1);
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown() with { Flags = 0x12345678, Rgb = [1, 2, 3] };
        NativeBattleCinematicRibbonRampResult result = NativeBattleCinematicRibbonWork.BuildColorRamp(frame, initial, 0x02112233, 0x00445566, NativeBattleEffectNodeMemory.ArenaAddress + 0x4000);
        Assert.Equal(initial.Rgb, result.Registers.Rgb);
        Assert.Equal(initial.Flags, result.Registers.Flags);
        Assert.Equal(0x3A112233u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x4000, 4)));
        Assert.Equal(0x38445566u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Arena.Read(0x4004, 4)));
    }
}
