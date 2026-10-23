using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLightBackIrTests
{
    [Fact]
    public void IR_vector_and_zero_translation_differ_from_preserved_V0_BACK_contract()
    {
        var memory = Memory();
        var actual = NativeBattleLightBackTransform.TransformIr(memory);
        var old = NativeBattleLightBackTransform.Transform(memory, 0);
        Assert.Equal(new int?[] { 4096, -2048, 8192 }, actual.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 101, 202, 303 }, old.ScriptedColorRegisters!.Mac);
        Assert.Equal(JsonSerializer.Serialize(memory.GeometryRegisters), JsonSerializer.Serialize(actual.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(memory.ScriptedLighting), JsonSerializer.Serialize(actual.ScriptedLighting));
        Assert.Equal(memory.ScriptedColorRegisters!.Rgb, actual.ScriptedColorRegisters.Rgb);
        Assert.Equal(memory.ScriptedColorRegisters.Material, actual.ScriptedColorRegisters.Material);
        Assert.Equal(memory.ScriptedColorRegisters.Ir0, actual.ScriptedColorRegisters.Ir0);
    }

    [Fact]
    public void Full_MAC_saturates_IR_and_publishes_current_FLAG_without_modifying_input_R_or_T()
    {
        var memory = Memory();
        var matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = new short[] { 32767, 0, 0, 0, -32768, 0, 0, 0, 4096 }.ToImmutableArray() };
        memory = NativeBattleLightBackTransform.LoadBone(memory, NativeBattlePackedMatrix32.FromMatrix(matrix, 123));
        memory = memory with { ScriptedColorRegisters = memory.ScriptedColorRegisters! with { Ir = new int?[] { 32767, 32767, 32767 }.ToImmutableArray() } };
        var actual = NativeBattleLightBackTransform.TransformIr(memory);
        Assert.True(actual.ScriptedColorRegisters!.Mac[0] > 32767);
        Assert.True(actual.ScriptedColorRegisters.Mac[1] < -32768);
        Assert.Equal(new int?[] { 32767, -32768, 32767 }, actual.ScriptedColorRegisters.Ir);
        Assert.Equal(0x81800000u, actual.ScriptedColorRegisters.Flags);
        Assert.Equal(JsonSerializer.Serialize(memory.GeometryRegisters), JsonSerializer.Serialize(actual.GeometryRegisters));
    }

    [Fact]
    public void Partial_LIGHT_knownness_propagates_per_row_without_requiring_unused_BACK()
    {
        var memory = Memory();
        var lighting = memory.ScriptedLighting!;
        var known = Enumerable.Range(0, 20).Select(index => index < 6).ToImmutableArray();
        memory = memory with { ScriptedLighting = lighting with { LightMatrix = lighting.LightMatrix! with { Known = known }, BackColor = NativeBattleScriptedBackColor.Unknown() }, ScriptedColorRegisters = memory.ScriptedColorRegisters! with { Ir = new int?[] { 100, 200, 300 }.ToImmutableArray() } };
        var actual = NativeBattleLightBackTransform.TransformIr(memory);
        Assert.Equal(100, actual.ScriptedColorRegisters!.Mac[0]);
        Assert.Null(actual.ScriptedColorRegisters.Mac[1]);
        Assert.Null(actual.ScriptedColorRegisters.Mac[2]);
        Assert.Null(actual.ScriptedColorRegisters.Flags);
        Assert.All(NativeBattleLightBackTransform.Transform(memory, 0).ScriptedColorRegisters!.Mac, value => Assert.Null(value));
    }

    [Fact]
    public void IR_input_is_signed_low_WORD_and_zero_coefficient_can_resolve_unknown_component()
    {
        var memory = Memory();
        memory = memory with { ScriptedColorRegisters = memory.ScriptedColorRegisters! with { Ir = new int?[] { unchecked((int)0xFFFF0001), null, 0x00018000 }.ToImmutableArray() } };
        var actual = NativeBattleLightBackTransform.TransformIr(memory);
        Assert.Equal(1, actual.ScriptedColorRegisters!.Mac[0]);
        Assert.Null(actual.ScriptedColorRegisters.Mac[1]);
        Assert.Equal(-32768, actual.ScriptedColorRegisters.Mac[2]);
    }

    private static NativeBattleEffectMemory Memory()
    {
        var matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 }, 123);
        var memory = NativeBattleLightBackTransform.LoadBone(NativeBattleEffectMemory.FromColdProcess(), matrix);
        var registers = NativeBattleGeometry.LoadVector(new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), 0, new(1, 2, 3), 4);
        return memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, 123), ScriptedColorRegisters = registers.Colors with { Ir = new int?[] { 4096, -2048, 8192 }.ToImmutableArray(), Ir0 = 1234, Material = 0xAB123456, Rgb = new uint?[] { 1, 2, 3 }.ToImmutableArray() } };
    }
}
