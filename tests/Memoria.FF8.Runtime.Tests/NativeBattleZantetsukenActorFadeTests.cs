using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleZantetsukenActorFadeTests
{
    [Fact]
    public void Fade_publishes_actual_far_and_DPCS_then_replaces_Color40_top_byte_without_touching_flags()
    {
        NativeBattleGraphicsModel model = new(3, 0x010E) { Color40 = 0xABABCDEF, Color44 = 0xFF010203, Alpha7 = 99, Flags2 = 0x1234 };
        NativeBattleZantetsukenActorFadeStep step = NativeBattleZantetsukenActorFade.Apply(model, NativeBattleEffectMemory.FromColdProcess(), 2048, Constants());
        Assert.Equal((uint?)0x32404040, step.Model.Color44);
        Assert.Equal((uint?)0x02402010, step.Model.Color40);
        Assert.Equal((byte?)0, step.Model.Alpha7);
        Assert.Equal(model.Flags2, step.Model.Flags2);
        Assert.Equal(model.Flags0, step.Model.Flags0);
        NativeBattleScriptedColorRegisters registers = step.Memory.ScriptedColorRegisters!;
        Assert.Equal((uint?)0, registers.Material);
        Assert.Equal((uint?)2048, registers.Ir0);
        Assert.Equal((uint?)0x00402010, registers.Rgb[2]);
        Assert.Equal(new int?[] { 256, 512, 1024 }, registers.Mac);
        Assert.Equal((uint?)512, step.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal((uint?)1024, step.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal((uint?)2048, step.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        NativeBattleZantetsukenActorFadeStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleZantetsukenActorFadeStep>(NativeBattleTestSnapshots.Json(step))!;
        NativeBattleZantetsukenActorFadeStep next = NativeBattleZantetsukenActorFade.Apply(step.Model, step.Memory, 2048, Constants());
        Assert.Equal(step.Model.Color40, next.Model.Color40);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(NativeBattleZantetsukenActorFade.Apply(restored.Model, restored.Memory, 2048, Constants())));
    }

    [Fact]
    public void Fade_writes_the_actor_color_without_reading_its_previous_value_and_requires_actual_far_controls()
    {
        NativeBattleZantetsukenActorFadeStep step = NativeBattleZantetsukenActorFade.Apply(new(3, 2), NativeBattleEffectMemory.FromColdProcess(), 4096, Constants());
        Assert.Equal((uint?)0x02804020, step.Model.Color40);
        Assert.Throws<NotSupportedException>(() => NativeBattleZantetsukenActorFade.Apply(new(3, 2), NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = null }, 4096, Constants()));
    }

    private static NativeBattleSummonColors Constants() => new(0x00804020, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
}
