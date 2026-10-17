using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect311ControllerTests
{
    [Fact]
    public void NativeBurstConsumesExactlySixCrtDrawsPerAdmittedPhysicalSlotAndKeepsNativeWidths()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25073D8, 0x20DFAB8, 28, 64);
        NativeCrtRandomState initial = new(17);
        NativeBattleEffect311ParticleInitialization first = NativeBattleEffect311Particles.Initialize665848(pool, initial);
        NativeCrtRandomState expected = initial;
        for (int index = 0; index < 384; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.Random);
        Assert.Equal(64, first.Pool.Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(first.Pool.Slots, task =>
        {
            Assert.Equal(0x665C30u, task.Callback);
            Assert.Equal((ushort)0, task.ReadWord(12));
            Assert.InRange(task.ReadWord(14), (ushort)43, (ushort)50);
            Assert.InRange(task.ReadWord(18), (ushort)1500, (ushort)2523);
            Assert.InRange(task.ReadWord(16), (ushort)0, (ushort)32767);
        });
        NativeBattleEffect311ParticleInitialization restored = JsonSerializer.Deserialize<NativeBattleEffect311ParticleInitialization>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(restored));
        NativeBattleEffect311ParticleInitialization exhausted = NativeBattleEffect311Particles.Initialize665848(restored.Pool, restored.Random);
        Assert.Equal(first.Random, exhausted.Random);
        Assert.Equal(JsonSerializer.Serialize(first.Pool), JsonSerializer.Serialize(exhausted.Pool));
    }

    [Fact]
    public void ReusedPayloadRetainsTheUnwrittenFinalWordAndFullSlotsDoNotConsumeRandom()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25073D8, 0x20DFAB8, 28, 64);
        for (int index = 0; index < 63; index++)
        {
            (pool, _) = pool.Append(0x665C30);
        }
        pool = pool with { Slots = pool.Slots.SetItem(63, pool.Slots[63].WriteWord(26, 0xABCD)) };
        NativeCrtRandomState before = new(123);
        NativeBattleEffect311ParticleInitialization initialized = NativeBattleEffect311Particles.Initialize665848(pool, before);
        NativeCrtRandomState expected = before;
        for (int index = 0; index < 6; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, initialized.Random);
        Assert.Equal((ushort)0xABCD, initialized.Pool.Slots[63].ReadWord(26));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 2625)]
    [InlineData(8, 3000)]
    [InlineData(66, 3000)]
    [InlineData(67, 2625)]
    [InlineData(73, 375)]
    [InlineData(74, 0)]
    public void StageEnvelopeKeepsOriginalRiseHoldFallAndCleanup(short frame, int expected)
    {
        Assert.Equal(expected, NativeBattleEffect311Timing.StageIntensity665550(frame));
    }

    [Fact]
    public void NativeOrbitRadiusKeepsTheActualDiscontinuousCosineHoldSineBranches()
    {
        Assert.Equal((short)4096, NativeBattleEffect311Timing.Radius6658B3(0));
        Assert.Equal((short)NativeBattleCameraMath.Cosine(15 << 6), NativeBattleEffect311Timing.Radius6658B3(15));
        Assert.Equal((short)4096, NativeBattleEffect311Timing.Radius6658B3(16));
        Assert.Equal((short)4096, NativeBattleEffect311Timing.Radius6658B3(34));
        Assert.Equal((short)NativeBattleCameraMath.Sine(64), NativeBattleEffect311Timing.Radius6658B3(35));
        Assert.Equal(3072, NativeBattleEffect311Timing.ScreenColor6657B7(47));
        Assert.Equal(3136, NativeBattleEffect311Timing.ScreenColor6657B7(48));
        Assert.Equal(4032, NativeBattleEffect311Timing.ScreenColor6657B7(55));
    }
}
