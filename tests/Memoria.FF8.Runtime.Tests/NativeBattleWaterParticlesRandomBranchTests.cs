using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(0u, 0, -4, 0xFFCu, 2531011u)]
    [InlineData(123u, 2, 0, 0x1006u, 28854610u)]
    [InlineData(123u, 3, -12, 0xFF4u, 28854610u)]
    [InlineData(123u, -1, 0, 0x1006u, 28854610u)]
    [InlineData(0x12345678u, 102, 0, 0x1006u, 3018423131u)]
    [InlineData(0x12345678u, 103, 12, 0x100Cu, 3018423131u)]
    public void Random_branch_0F_uses_absolute_sample_signed_threshold_and_same_PC_displacement(uint seed, short threshold, short displacement, uint expectedPc, uint expectedSeed)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x0F, unchecked((ushort)threshold), unchecked((ushort)displacement));
        frame = frame with { Random = new(seed), Arena = frame.Arena.MarkUnknown(Particle, 0x50) };
        if (expectedPc == 0x1006)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1004, 2) };
        }
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(expectedPc, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(expectedSeed, result.Random.Seed);
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(One(restored)));
    }
}
