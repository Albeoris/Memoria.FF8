using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSuicide180Tests
{
    [Fact]
    public void Ring_negative_spin_uses_arithmetic_shift_but_negative_growth_uses_truncating_division()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(4, 16, 36, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(12, 15).WriteWord(24, 2).WriteWord(26, unchecked((ushort)-17)).WriteWord(28, 5).WriteWord(30, unchecked((ushort)-8));
        NativeBattleModulePoolTask updated = NativeBattleSuicide180.AdvanceRing(task, false);
        Assert.Equal((short)-15, unchecked((short)updated.ReadWord(24)));
        Assert.Equal((short)-15, unchecked((short)updated.ReadWord(26)));
        Assert.Equal((short)-3, unchecked((short)updated.ReadWord(28)));
        Assert.Equal((short)-7, unchecked((short)updated.ReadWord(30)));
        Assert.Equal((ushort)16, updated.ReadWord(12));
        NativeBattleModulePoolTask second = NativeBattleSuicide180.AdvanceRing(task, true);
        Assert.Equal(task.ReadWord(24), second.ReadWord(24));
        Assert.Equal(task.ReadWord(26), second.ReadWord(26));
    }

    [Fact]
    public void Explosion_header_fade_latches_across_younger_particles_in_the_same_scan()
    {
        NativeBattleSuicide180ExplosionHeader header = NativeBattleSuicide180ExplosionHeader.BeginTask();
        Assert.Equal(new NativeBattleSuicide180ExplosionHeader(0x33u, 0), header.SelectParticle(1));
        header = header.SelectParticle(3);
        Assert.Equal(new NativeBattleSuicide180ExplosionHeader(0xF3u, 1024), header);
        Assert.Equal(header, header.SelectParticle(0));
        Assert.Equal(header, header.SelectParticle(1));
        Assert.Equal(new NativeBattleSuicide180ExplosionHeader(0xF3u, 2048), header.SelectParticle(4));
        Assert.Equal(new NativeBattleSuicide180ExplosionHeader(0x33u, 0), NativeBattleSuicide180ExplosionHeader.BeginTask());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 3840)]
    [InlineData(16, 4096)]
    [InlineData(23, 4096)]
    public void Caster_overlay_reaches_full_brightness_at_native_counter16(int counter, int expected)
    {
        Assert.Equal(expected, NativeBattleSuicide180.GlowLevel(counter));
    }
}
