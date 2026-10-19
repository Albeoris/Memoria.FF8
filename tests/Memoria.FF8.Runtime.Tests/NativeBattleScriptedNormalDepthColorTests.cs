using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedNormalDepthColorTests
{
    [Theory]
    [InlineData(0, 2048, 0x5A808080u)]
    [InlineData(2048, 1536, 0x5A606060u)]
    [InlineData(4096, 1024, 0x5A404040u)]
    public void Depth_fraction_interpolates_material_lighting_to_current_far_controls(short cue, int mac, uint rgb)
    {
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [1u, 2u, 3u], Flags = uint.MaxValue };
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(0), new(4096, 4096, 4096), 0x5A808080, cue, new(1024, 1024, 1024, null, null), initial);
        Assert.Equal(new int?[] { mac, mac, mac }, result.Mac);
        Assert.Equal(new int?[] { mac, mac, mac }, result.Ir);
        Assert.Equal(new uint?[] { 2u, 3u, rgb }, result.Rgb);
        Assert.Equal(0u, result.Flags);
    }

    [Fact]
    public void Negative_float_lighting_is_zero_before_interpolation_and_negative_cue_is_signed()
    {
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(-8192), new(0, 0, 0), 0x7F808080, -2048, new(32, 32, 32, null, null), NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new int?[] { -16, -16, -16 }, result.Mac);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Ir);
        Assert.Equal(0x7F000000u, result.Rgb[2]);
        Assert.Equal(0x81F80000u, result.Flags);
    }

    [Fact]
    public void Material_float_intermediate_is_not_rounded_to_NCCS_MAC_before_depth_interpolation()
    {
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(11), new(0, 0, 0), 0x00404040, 2048, new(0, 0, 0, null, null), NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new int?[] { 1, 1, 1 }, result.Mac);
    }

    [Fact]
    public void Far_controls_are_signed_float_stored_DWORDs_and_require_real_publication()
    {
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(0), new(0, 0, 0), 0, 4096, new(16777217, unchecked((uint)-32), 0, null, null), initial);
        Assert.Equal(new int?[] { 16777216, -32, 0 }, result.Mac);
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(0), new(0, 0, 0), 0, 0, new(null, 0, 0, null, null), initial));
    }

    [Fact]
    public void Partial_packed_normal_known_mask_survives_both_shaders_and_snapshot()
    {
        NativeBattleScriptedColorRegisters registers = NativeBattleScriptedColorRegisters.Unknown() with { NormalInput = new byte[8].ToImmutableArray(), NormalInputKnown = new bool[] { true, true, true, true, true, true, false, false }.ToImmutableArray() };
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(0), new(0, 0, 0), 0, 0, new(0, 0, 0, null, null), registers);
        result = NativeBattleScriptedNormalColor.Transform4601B0(Lighting(0), new(0, 0, 0), 0, result);
        Assert.Equal(registers.NormalInputKnown, result.NormalInputKnown);
        string json = System.Text.Json.JsonSerializer.Serialize(result);
        NativeBattleScriptedColorRegisters restored = System.Text.Json.JsonSerializer.Deserialize<NativeBattleScriptedColorRegisters>(json)!;
        Assert.Equal(registers.NormalInputKnown!.Value.ToArray(), restored.NormalInputKnown!.Value.ToArray());
        restored.Validate();
    }

    [Fact]
    public void Explicit_lighting_argument_does_not_consume_or_publish_temporary84_and_resets_old_input_mask()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(0x84, false) } };
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown() with { NormalInput = new byte[8].ToImmutableArray(), NormalInputKnown = Enumerable.Repeat(false, 8).ToImmutableArray() };
        NativeBattleScriptedMaterialStep result = NativeBattleScriptedMaterial.Prepare(frame, initial, 0x00030201);
        Assert.False(result.Frame.TemporaryFrame.Known[0x84]);
        Assert.Null(result.Registers.NormalInputKnown);
        Assert.Equal(new byte[] { 0, 16, 0, 16, 0, 16, 0, 0 }, result.Registers.NormalInput!.Value.ToArray());
        Assert.Equal(96u, result.Frame.ReadTemporaryDword(0xEC));
        Assert.Equal(32, result.Lighting.ColorMatrix!.Rotation[0]);
        Assert.Equal(64, result.Lighting.ColorMatrix.Rotation[3]);
        Assert.Equal(96, result.Lighting.ColorMatrix.Rotation[6]);
    }

    private static NativeBattleScriptedLightingMemory Lighting(int back)
    {
        short[] rotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        byte[] data = new byte[20];
        for (int index = 0; index < rotation.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * 2), rotation[index]);
        }
        NativeBattleScriptedLightingMatrix matrix = new(data.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray());
        return new(matrix, matrix, new(back, back, back));
    }
}
