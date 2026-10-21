using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedDpctTests
{
    [Fact]
    public void Load_installs_three_FIFO_colors_and_only_third_material()
    {
        NativeBattleScriptedColorRegisters previous = NativeBattleScriptedColorRegisters.Unknown() with { Ir0 = 0x12348000, Flags = 0xFFFFFFFF };
        NativeBattleScriptedColorRegisters loaded = NativeBattleScriptedDpct.LoadColors45E120(previous, 0x110000FF, 0x22000000, 0x33000000);
        Assert.Equal(new uint?[] { 0x110000FF, 0x22000000, 0x33000000 }, loaded.Rgb);
        Assert.Equal(0x33000000u, loaded.Material);
        Assert.Equal(previous.Ir0, loaded.Ir0);
        Assert.Equal(previous.Flags, loaded.Flags);
        Assert.Equal(previous.Mac, loaded.Mac);
        Assert.Equal(previous.Ir, loaded.Ir);
    }

    [Fact]
    public void Earlier_RGB_clamp_survives_but_earlier_IR_overflow_does_not()
    {
        NativeBattleScriptedColorRegisters previous = NativeBattleScriptedDpct.LoadColors45E120(NativeBattleScriptedColorRegisters.Unknown(), 0x110000FF, 0x22000000, 0x33000000) with { Ir0 = 0x12348000 };
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedDpct.Transform45F4C0(previous, Far(0, 0, 0), Constants());
        Assert.Equal(new uint?[] { 0x330000FF, 0x33000000, 0x33000000 }, result.Rgb);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.Mac);
        Assert.Equal(result.Mac, result.Ir);
        Assert.Equal(0x00200000u, result.Flags);
        Assert.Equal(previous.Material, result.Material);
        Assert.Equal(previous.Ir0, result.Ir0);
        NativeBattleScriptedColorRegisters restored = JsonSerializer.Deserialize<NativeBattleScriptedColorRegisters>(JsonSerializer.Serialize(result))!;
        Assert.Equal(result.Rgb, restored.Rgb);
        Assert.Equal(result.Flags, restored.Flags);
    }

    [Fact]
    public void Final_color_publishes_signed_controls_wrap_and_IR_flags()
    {
        NativeBattleScriptedColorRegisters previous = NativeBattleScriptedDpct.LoadColors45E120(NativeBattleScriptedColorRegisters.Unknown(), 0, 0, 0xAB0000FF) with { Ir0 = 0x98761000 };
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedDpct.Transform45F4C0(previous, Far(int.MinValue, 32768, -32769), Constants());
        Assert.Equal(new int?[] { int.MinValue, 32768, -32769 }, result.Mac);
        Assert.Equal(new int?[] { short.MinValue, short.MaxValue, short.MinValue }, result.Ir);
        Assert.Equal(new uint?[] { 0xAB00FF00, 0xAB00FF00, 0xAB00FF00 }, result.Rgb);
        Assert.Equal(0x81F80000u, result.Flags);
        Assert.Equal(0xAB0000FFu, result.Material);
        Assert.Equal(previous.Ir0, result.Ir0);
    }

    [Fact]
    public void Subsequent_first_color_DPCS_overwrites_flags_and_shifts_actual_triple_FIFO()
    {
        NativeBattleScriptedColorRegisters previous = NativeBattleScriptedDpct.LoadColors45E120(NativeBattleScriptedColorRegisters.Unknown(), 0x110000FF, 0x22010203, 0x33040506) with { Ir0 = 0 };
        NativeBattleScriptedColorRegisters triple = NativeBattleScriptedDpct.Transform45F4C0(previous, Far(0, 0, 0), Constants());
        NativeBattleScriptedColorRegisters result = NativeBattleScriptedDpcs.Transform45F270(triple, 0x44070809, 0, Far(0, 0, 0), Constants()).Registers;
        Assert.Equal(new uint?[] { 0x33010203, 0x33040506, 0x44070809 }, result.Rgb);
        Assert.Equal(new int?[] { 144, 128, 112 }, result.Mac);
        Assert.Equal(0u, result.Flags);
        Assert.Equal(0x44070809u, result.Material);
    }

    [Fact]
    public void Unknown_required_inputs_fail_without_hydration()
    {
        NativeBattleScriptedColorRegisters previous = NativeBattleScriptedColorRegisters.Unknown();
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedDpct.Transform45F4C0(previous, Far(0, 0, 0), Constants()));
        previous = previous with { Material = 0, Ir0 = 0, Rgb = [0, null, 0] };
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedDpct.Transform45F4C0(previous, Far(0, 0, 0), Constants()));
    }

    private static NativeBattleModelRendererRegisters Far(int red, int green, int blue) => new(unchecked((uint)red), unchecked((uint)green), unchecked((uint)blue), null, null);
    private static NativeBattleSummonColors Constants() => new(0, 0, 1f / 4096f, 4503601774854144.0);
}
