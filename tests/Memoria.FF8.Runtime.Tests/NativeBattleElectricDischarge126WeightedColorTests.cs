using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126WeightedColorTests
{
    [Theory]
    [InlineData(4096, 0, 0xABA0B0C0u)]
    [InlineData(2048, 2048, 0xAB707070u)]
    [InlineData(0, 4096, 0xAB403020u)]
    [InlineData(-4096, 0, 0xAB605040u)]
    public void RGB_byte_blend_publishes_GPF_GPL_feedback_and_preserves_destination_high_byte(short firstFactor, short secondFactor, uint expected)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = null, ScriptedColorRegisters = null };
        NativeBattleWeightedColorStep step = NativeBattleWeightedColor56CC00.Blend(memory, 0xFFA0B0C0, 0xEE403020, firstFactor, secondFactor, 0xAB010203, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(expected, step.Color);
        Assert.Null(step.Memory.GeometryRegisters);
        Assert.Null(step.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(unchecked((uint)(int)secondFactor), step.Memory.ScriptedColorRegisters.Ir0);
        Assert.All(step.Memory.ScriptedColorRegisters.Mac, value => Assert.NotNull(value));
        Assert.All(step.Memory.ScriptedColorRegisters.Ir, value => Assert.NotNull(value));
        Assert.NotNull(step.Memory.ScriptedColorRegisters.Flags);
        Assert.Same(memory.PrimitiveProcess, step.Memory.PrimitiveProcess);
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Memory)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Memory.ScriptedColorRegisters)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored.ScriptedColorRegisters)));
        NativeBattleWeightedColorStep next = NativeBattleWeightedColor56CC00.Blend(step.Memory, step.Color, 0xFFFFFF, 1024, 3072, step.Color, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleWeightedColorStep restoredNext = NativeBattleWeightedColor56CC00.Blend(restored, step.Color, 0xFFFFFF, 1024, 3072, step.Color, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(next.Color, restoredNext.Color);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(next.Memory.ScriptedColorRegisters)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restoredNext.Memory.ScriptedColorRegisters)));
    }
}
