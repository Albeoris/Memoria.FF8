using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedNormalColorTests
{
    [Fact]
    public void Identity_lighting_modulates_each_material_byte_and_advances_actual_RGB_FIFO()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting(Identity(), Identity(), new(0, 0, 0));
        NativeBattleScriptedColorRegisters initial = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [0x11112222, 0x33334444, 0x55556666], Flags = uint.MaxValue };
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(4096, 2048, 1024), 0x7F804020, initial);
        Assert.Equal(new int?[] { 512, 512, 512 }, result.Mac);
        Assert.Equal(new int?[] { 512, 512, 512 }, result.Ir);
        Assert.Equal(new uint?[] { 0x33334444, 0x55556666, 0x7F202020 }, result.Rgb);
        Assert.Equal(0u, result.Flags);
    }

    [Fact]
    public void First_light_stage_has_no_upper_IR_clamp()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting([32767, 0, 0, 0, 0, 0, 0, 0, 0], Identity(), new(0, 0, 0));
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(32767, 0, 0), 0x340000FF, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(261104, result.Mac[0]);
        Assert.Equal(32767, result.Ir[0]);
        Assert.Equal(0x340000FFu, result.Rgb[2]);
        Assert.Equal(0x81200000u, result.Flags);
    }

    [Fact]
    public void First_stage_stores_float32_before_color_products()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting([23615, 21693, -6562, 0, 0, 0, 0, 0, 0], [30772, 0, 0, 0, 0, 0, 0, 0, 0], new(0, 0, 0));
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(27013, 483, 31076), 0x000000D1, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(665554, result.Mac[0]);
    }

    [Fact]
    public void Back_green_and_blue_have_native_float32_store_while_red_keeps_its_signed_DWORD_exact()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting(new short[9], new short[9], new(16777217, 16777217, 16777217));
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(0, 0, 0), 0x23818181, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new int?[] { 8454145, 8454144, 8454144 }, result.Mac);
        Assert.Equal(0x23FFFFFFu, result.Rgb[2]);
        Assert.Equal(0x81F80000u, result.Flags);
    }

    [Fact]
    public void Negative_light_is_zeroed_before_back_color_and_negative_MAC_sets_each_native_saturation_flag()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting(Identity(), Identity(), new(-16, -16, -16));
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(-4096, -4096, -4096), 0xABFFFFFF, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new int?[] { -16, -16, -16 }, result.Mac);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Ir);
        Assert.Equal(0xAB000000u, result.Rgb[2]);
        Assert.Equal(0x81F80000u, result.Flags);
    }

    [Fact]
    public void RGB_saturation_uses_raw_MAC_and_sets_its_flags_independently_of_IR_saturation()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting(new short[9], new short[9], new(8192, 8192, 8192));
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(0, 0, 0), 0x42808080, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Equal(new int?[] { 4096, 4096, 4096 }, result.Ir);
        Assert.Equal(0x42FFFFFFu, result.Rgb[2]);
        Assert.Equal(0x00380000u, result.Flags);
    }

    [Fact]
    public void Matrix_padding_is_unconsumed_and_missing_current_controls_fail_without_defaults()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting(Identity(), Identity(), new(0, 0, 0));
        lighting = lighting with { LightMatrix = lighting.LightMatrix! with { Known = lighting.LightMatrix!.Known.SetItem(18, false).SetItem(19, false) }, ColorMatrix = lighting.ColorMatrix! with { Known = lighting.ColorMatrix!.Known.SetItem(18, false).SetItem(19, false) } };
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(0, 0, 0), 0, NativeBattleScriptedColorRegisters.Unknown());
        Assert.Null(result.Rgb[0]);
        Assert.Null(result.Rgb[1]);
        Assert.Equal(0u, result.Rgb[2]);
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedNormalColor.Transform4601B0(lighting with { LightMatrix = null }, new(0, 0, 0), 0, result));
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedNormalColor.Transform4601B0(lighting with { ColorMatrix = null }, new(0, 0, 0), 0, result));
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedNormalColor.Transform4601B0(lighting with { BackColor = null }, new(0, 0, 0), 0, result));
    }

    [Fact]
    public void Independent_snapshot_keeps_prepared_controls_FIFO_MAC_IR_and_next_normal_result()
    {
        NativeBattleScriptedLightingMemory lighting = Lighting(Identity(), Identity(), new(16, 32, 48));
        NativeBattleScriptedColorRegisters first = NativeBattleScriptedNormalColor.Transform4601B0(lighting, new(4096, 2048, 1024), 0x7F804020, NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleEffectMemory live = NativeBattleEffectMemory.FromColdProcess() with { ScriptedLighting = lighting, ScriptedColorRegisters = first };
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(live))!;
        NativeBattleScriptedColorRegisters next = NativeBattleScriptedNormalColor.Transform4601B0(live.ScriptedLighting!, new(1, -2, 3), 0x6180FF00, live.ScriptedColorRegisters!);
        NativeBattleScriptedColorRegisters restoredNext = NativeBattleScriptedNormalColor.Transform4601B0(restored.ScriptedLighting!, new(1, -2, 3), 0x6180FF00, restored.ScriptedColorRegisters!);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(restored.ScriptedColorRegisters));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restoredNext));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleScriptedColorRegisters>("{}"));
    }

    private static short[] Identity() => [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
    private static NativeBattleScriptedLightingMemory Lighting(short[] light, short[] color, NativeBattleScriptedBackColor back) => new(Matrix(light), Matrix(color), back);

    private static NativeBattleScriptedLightingMatrix Matrix(short[] rotation)
    {
        byte[] bytes = new byte[20];
        for (int index = 0; index < rotation.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), rotation[index]);
        }
        return new(bytes.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray());
    }
}
