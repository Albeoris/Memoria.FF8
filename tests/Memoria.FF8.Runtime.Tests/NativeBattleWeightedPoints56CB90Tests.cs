using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWeightedPoints56CB90Tests
{
    [Theory]
    [InlineData(4096, 0, 10)]
    [InlineData(2048, 2048, 20)]
    [InlineData(0, 4096, 30)]
    [InlineData(-4096, 0, -10)]
    public void Signed_weights_publish_IR_WORD_and_preserve_absent_geometry(short firstFactor, short secondFactor, short expected)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = null, ScriptedColorRegisters = null };
        NativeBattleWeightedPointsStep step = NativeBattleWeightedPoints56CB90.Blend(memory, new(10, -10, 0), new(30, -30, 0), firstFactor, secondFactor, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(new NativeBattleVector(expected, unchecked((short)-expected), 0), step.Position);
        Assert.Null(step.Memory.GeometryRegisters);
        Assert.Equal(unchecked((uint)(int)secondFactor), step.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Null(step.Memory.ScriptedColorRegisters.Material);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(step.Memory))!;
        Assert.Null(restored.GeometryRegisters);
        Assert.Equal(step.Memory.ScriptedColorRegisters.Mac.ToArray(), restored.ScriptedColorRegisters!.Mac.ToArray());
    }

    [Fact]
    public void Geometry_and_unrelated_memory_owners_remain_identical()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleWeightedPointsStep step = NativeBattleWeightedPoints56CB90.Blend(memory, new(32767, -32768, 100), new(32767, -32768, 200), 4096, 4096, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(new NativeBattleVector(32767, -32768, 300), step.Position);
        Assert.Same(memory.GeometryRegisters, step.Memory.GeometryRegisters);
        Assert.Same(memory.Arena, step.Memory.Arena);
        Assert.Same(memory.PrimitiveProcess, step.Memory.PrimitiveProcess);
        Assert.NotEqual(memory.ScriptedColorRegisters, step.Memory.ScriptedColorRegisters);
    }
}
