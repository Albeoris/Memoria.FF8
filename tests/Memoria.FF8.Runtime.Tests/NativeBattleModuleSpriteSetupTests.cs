using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleSpriteSetupTests
{
    [Fact]
    public void Unknown_source_W_is_retained_in_loaded_input_without_changing_MAC_IR_or_billboard_stores()
    {
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 500, Y = -200, Z = 1000 }, null);
        NativeBattleEffectMemory initial = Memory(NativeBattlePackedMatrix32.FromMatrix(Identity(), 0xABCD));
        NativeBattleEffectMemory known = NativeBattleModuleSpriteSetup.Setup571BC0(initial, camera, new NativeBattleVector(10, -20, 30), unchecked((short)0xCAFE), 512, -32);
        NativeBattleEffectMemory unknown = NativeBattleModuleSpriteSetup.Setup571BC0(initial, camera, new NativeBattleVector(10, -20, 30), null, 512, -32);
        Assert.Equal(JsonSerializer.Serialize(known.GeometryRegisters), JsonSerializer.Serialize(unknown.GeometryRegisters));
        Assert.Equal(JsonSerializer.Serialize(known.ModuleSpriteBillboard21DFED0), JsonSerializer.Serialize(unknown.ModuleSpriteBillboard21DFED0));
        Assert.Equal(known.ScriptedColorRegisters!.Mac, unknown.ScriptedColorRegisters!.Mac);
        Assert.Equal(known.ScriptedColorRegisters.Ir, unknown.ScriptedColorRegisters.Ir);
        Assert.Equal(known.ScriptedColorRegisters.Flags, unknown.ScriptedColorRegisters.Flags);
        Assert.Equal(known.ScriptedColorRegisters.Rgb, unknown.ScriptedColorRegisters.Rgb);
        Assert.Equal(known.ScriptedColorRegisters.NormalInput!.Value.Take(6), unknown.ScriptedColorRegisters.NormalInput!.Value.Take(6));
        Assert.All(known.ScriptedColorRegisters.NormalInputKnown!.Value, value => Assert.True(value));
        Assert.All(unknown.ScriptedColorRegisters.NormalInputKnown!.Value.Skip(6), value => Assert.False(value));
        Assert.Equal(new byte[] { 0xFE, 0xCA }, known.ScriptedColorRegisters.NormalInput.Value.Skip(6));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(unknown))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleModuleSpriteSetup.Setup571BC0(unknown, camera, new NativeBattleVector(10, -20, 30), null, 512, -32)), JsonSerializer.Serialize(NativeBattleModuleSpriteSetup.Setup571BC0(restored, camera, new NativeBattleVector(10, -20, 30), null, 512, -32)));
    }

    [Fact]
    public void Billboard_writes_only_three_diagonal_words_and_translation_while_retaining_padding_knownness()
    {
        NativeBattleActionMatrix retained = new([1, 2, 3, 4, 5, 6, 7, 8, 9], 100, 200, 300, 4096);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(retained, null);
        packed = packed with { Bytes = packed.Bytes.SetItem(18, 0xCD).SetItem(19, 0xAB), Known = packed.Known.SetItem(18, true) };
        NativeBattleEffectMemory memory = Memory(packed);
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(Identity(), 0x7654);
        NativeBattleEffectMemory result = NativeBattleModuleSpriteSetup.Setup571BC0(memory, camera, new(10, -20, 30, unchecked((short)0xBEEF)), -4096, 0);
        NativeBattlePackedMatrix32 actual = result.ModuleSpriteBillboard21DFED0!;
        Assert.Equal(new short[] { -4096, 2, 3, 4, -4096, 6, 7, 8, -4096 }, actual.ReadMatrix().Rotation);
        Assert.Equal(10, actual.ReadMatrix().X);
        Assert.Equal(-20, actual.ReadMatrix().Y);
        Assert.Equal(30, actual.ReadMatrix().Z);
        Assert.Equal((byte)0xCD, actual.Bytes[18]);
        Assert.Equal((byte)0xAB, actual.Bytes[19]);
        Assert.True(actual.Known[18]);
        Assert.False(actual.Known[19]);
        Assert.Equal((byte)0xCD, result.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.GeometryRegisters.Rotation20[19]);
        Assert.Equal(new byte[] { 0xEF, 0xBE }, result.ScriptedColorRegisters!.NormalInput!.Value.Skip(6));
        Assert.Equal(memory.GeometryRegisters!.Sxy, result.GeometryRegisters.Sxy);
        Assert.Equal(memory.GeometryRegisters.Sz, result.GeometryRegisters.Sz);
        Assert.Equal(memory.GeometryRegisters.Mac0, result.GeometryRegisters.Mac0);
        Assert.Equal(memory.GeometryRegisters.Otz, result.GeometryRegisters.Otz);
        Assert.Equal(memory.ScriptedColorRegisters!.Rgb, result.ScriptedColorRegisters.Rgb);
    }

    [Theory]
    [InlineData(-256, 39744)]
    [InlineData(0, 40000)]
    [InlineData(256, 40256)]
    public void Depth_displacement_uses_normalized_raw_MAC_and_preserves_the_original_saturated_IR(short depth, int expected)
    {
        NativeBattleEffectMemory memory = Memory(NativeBattlePackedMatrix32.FromMatrix(Identity(), 0));
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 40000 }, null);
        NativeBattleEffectMemory result = NativeBattleModuleSpriteSetup.Setup571BC0(memory, camera, new(0, 0, 0, 123), 4096, depth);
        Assert.Equal(expected, result.ModuleSpriteBillboard21DFED0!.ReadMatrix().X);
        Assert.Equal(40000, result.ScriptedColorRegisters!.Mac[0]);
        Assert.Equal(32767, result.ScriptedColorRegisters.Ir[0]);
        Assert.Equal(0x81000000u, result.ScriptedColorRegisters.Flags);
        Assert.Equal(expected, result.GeometryRegisters!.Translation[0]);
        Assert.Equal(123, BinaryPrimitives.ReadInt16LittleEndian(result.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6)));
    }

    [Fact]
    public void Matrix_capture_preserves_each_partial_control_byte_and_unknown_translation_lane()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Rotation20 = new byte?[20].ToImmutableArray().SetItem(0, 0x12).SetItem(19, 0xAB), Translation = [-1, null, int.MinValue] };
        NativeBattlePackedMatrix32 actual = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(geometry);
        Assert.Equal((byte)0x12, actual.Bytes[0]);
        Assert.True(actual.Known[0]);
        Assert.False(actual.Known[1]);
        Assert.True(actual.Known[19]);
        Assert.Equal((byte)0xAB, actual.Bytes[19]);
        Assert.All(actual.Known.Skip(20).Take(4), value => Assert.True(value));
        Assert.All(actual.Known.Skip(24).Take(4), value => Assert.False(value));
        Assert.All(actual.Known.Skip(28).Take(4), value => Assert.True(value));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(actual.Bytes.AsSpan().Slice(20)));
        Assert.Equal(int.MinValue, BinaryPrimitives.ReadInt32LittleEndian(actual.Bytes.AsSpan().Slice(28)));
        Assert.Throws<NotSupportedException>(() => actual.ReadMatrix());
    }

    [Fact]
    public void Missing_retained_matrix_or_unknown_untouched_rotation_remains_unsupported()
    {
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(Identity(), null);
        NativeBattleEffectMemory memory = Memory(camera);
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleSpriteSetup.Setup571BC0(memory with { ModuleSpriteBillboard21DFED0 = null }, camera, new(0, 0, 0, 0), 4096, 0));
        NativeBattlePackedMatrix32 unknown = camera with { Known = camera.Known.SetItem(2, false) };
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleSpriteSetup.Setup571BC0(memory with { ModuleSpriteBillboard21DFED0 = unknown }, camera, new(0, 0, 0, 0), 4096, 0));
        NativeBattlePackedMatrix32 unknownDiagonal = camera with { Known = camera.Known.SetItem(0, false) };
        NativeBattleEffectMemory initialized = NativeBattleModuleSpriteSetup.Setup571BC0(memory with { ModuleSpriteBillboard21DFED0 = unknownDiagonal }, camera, new(0, 0, 0, 0), 4096, 0);
        Assert.True(initialized.ModuleSpriteBillboard21DFED0!.Known[0]);
    }

    [Fact]
    public void Packed_global_and_canonical_registers_have_identical_independent_JSON_next_setup()
    {
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(Identity() with { X = 500, Y = -200, Z = 1000 }, null);
        NativeBattleEffectMemory memory = NativeBattleModuleSpriteSetup.Setup571BC0(Memory(NativeBattlePackedMatrix32.FromMatrix(Identity(), 0xABCD)), camera, new(1, 2, 3, 4), 4096, -256);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleEffectMemory actual = NativeBattleModuleSpriteSetup.Setup571BC0(memory, camera, new(5, 6, 7, 8), 2048, 256);
        NativeBattleEffectMemory cold = NativeBattleModuleSpriteSetup.Setup571BC0(restored, camera, new(5, 6, 7, 8), 2048, 256);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(cold));
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattleEffectMemory Memory(NativeBattlePackedMatrix32 billboard)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Sxy = [1, 2, 3], Sz = [4, 5, 6, 7], Mac0 = 123, Otz = 456 };
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = geometry, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [8, 9, 10] }, ModuleSpriteBillboard21DFED0 = billboard };
    }
}
