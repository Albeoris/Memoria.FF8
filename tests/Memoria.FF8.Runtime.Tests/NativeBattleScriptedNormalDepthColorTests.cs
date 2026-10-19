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
    public void Far_controls_are_signed_float_stored_DWORDs_and_require_real_publication()
    {
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown();
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(0), new(0, 0, 0), 0, 4096, new(16777217, unchecked((uint)-32), 0, null, null), initial);
        Assert.Equal(new int?[] { 16777216, -32, 0 }, result.Mac);
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedNormalDepthColor.Transform45EE80(Lighting(0), new(0, 0, 0), 0, 0, new(null, 0, 0, null, null), initial));
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
