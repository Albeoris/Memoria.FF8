using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect101TintTests
{
    [Fact]
    public void Saved_RGB_bytes_load_directly_to_IR_and_factor_half_preserves_material_and_fifo_prefix()
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x7A123456, Rgb = [1, 2, 3] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedColorRegisters = colors };
        NativeBattleEffect101TintStep step = NativeBattleEffect101Tint.Execute(memory, 0xAB204080, 2048, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0x00102040u, step.Rgb);
        Assert.Equal(new int?[] { 64, 32, 16 }, step.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 64, 32, 16 }, step.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(new uint?[] { 2, 3, 0x7A102040 }, step.Memory.ScriptedColorRegisters.Rgb);
        Assert.Equal(0x7A123456u, step.Memory.ScriptedColorRegisters.Material);
        Assert.Same(memory.GeometryRegisters, step.Memory.GeometryRegisters);
    }

    [Fact]
    public void Negative_factor_returns_BYTE_of_signed_IR_rather_than_saturated_RGB_fifo_color()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x55000000 } };
        NativeBattleEffect101TintStep step = NativeBattleEffect101Tint.Execute(memory, 0x00204080, -4096, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0x00E0C080u, step.Rgb);
        Assert.Equal(new int?[] { -128, -64, -32 }, step.Memory.ScriptedColorRegisters!.Ir);
        Assert.Equal(0x55000000u, step.Memory.ScriptedColorRegisters.Rgb[2]);
        Assert.Equal(0x00380000u, step.Memory.ScriptedColorRegisters.Flags);
    }

    [Fact]
    public void Missing_containers_remain_unknown_where_native_tint_does_not_write()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = null, ScriptedColorRegisters = null };
        NativeBattleEffect101TintStep step = NativeBattleEffect101Tint.Execute(memory, 0x32FF8040, 4096, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0x00FF8040u, step.Rgb);
        Assert.Null(step.Memory.GeometryRegisters);
        Assert.Null(step.Memory.ScriptedColorRegisters!.Material);
        Assert.Null(step.Memory.ScriptedColorRegisters.Rgb[0]);
        Assert.Null(step.Memory.ScriptedColorRegisters.Rgb[1]);
        Assert.Null(step.Memory.ScriptedColorRegisters.Rgb[2]);
        Assert.Equal(4096u, step.Memory.ScriptedColorRegisters.Ir0);
    }

    [Fact]
    public void Independent_JSON_next_preserves_current_GPF_fifo_and_unknown_material_owner()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedColorRegisters = null };
        memory = NativeBattleEffect101Tint.Execute(memory, 0x32FE5B11, 2048, NativeBattleGteColorInterpolation.CodeConstants45F270).Memory;
        NativeBattleEffectMemory imported = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleEffect101TintStep expected = NativeBattleEffect101Tint.Execute(memory, 0x32010203, 3072, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleEffect101TintStep actual = NativeBattleEffect101Tint.Execute(imported, 0x32010203, 3072, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }
}
