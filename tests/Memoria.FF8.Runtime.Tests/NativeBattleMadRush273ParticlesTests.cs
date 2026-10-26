using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMadRush273ParticlesTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(19, 0)]
    [InlineData(20, 1)]
    [InlineData(22, 2)]
    [InlineData(25, 3)]
    [InlineData(29, 1)]
    [InlineData(30, 0)]
    [InlineData(65535, 0)]
    public void Birth_window_uses_unsigned_ten_frame_gate_and_native_sine(int frame, int expected)
    {
        Assert.Equal(expected, NativeBattleMadRush273Particles.BirthCount(frame));
    }

    [Fact]
    public void Birth_consumes_five_CRT_draws_in_scale_x_z_y_velocity_order()
    {
        NativeBattleModulePoolTask original = Task();
        (NativeCrtRandomState random, NativeBattleModulePoolTask task) = NativeBattleMadRush273Particles.Initialize(new(1), original, new(100, -200, 300));
        Assert.Equal(3403800452u, random.Seed);
        Assert.Equal((ushort)1024, task.ReadWord(14));
        Assert.Equal((short)-121, unchecked((short)task.ReadWord(16)));
        Assert.Equal((short)-1100, unchecked((short)task.ReadWord(18)));
        Assert.Equal((short)234, unchecked((short)task.ReadWord(20)));
        Assert.Equal((short)-49, unchecked((short)task.ReadWord(22)));
        Assert.Equal(original.Callback, task.Callback);
    }

    [Fact]
    public void Thirteenth_phase_releases_before_moving_the_retained_position()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(12, 12).WriteWord(18, 123).WriteWord(22, unchecked((ushort)-17));
        (task, int result) = NativeBattleMadRush273Particles.Advance(task);
        Assert.Equal(2, result);
        Assert.Equal((ushort)13, task.ReadWord(12));
        Assert.Equal((ushort)123, task.ReadWord(18));
    }

    [Fact]
    public void Live_phase_moves_only_signed_y_with_word_wrap()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(12, 11).WriteWord(16, 27).WriteWord(18, 32760).WriteWord(20, 45).WriteWord(22, 100);
        (task, int result) = NativeBattleMadRush273Particles.Advance(task);
        Assert.Equal(0, result);
        Assert.Equal(unchecked((ushort)32860), task.ReadWord(18));
        Assert.Equal((ushort)27, task.ReadWord(16));
        Assert.Equal((ushort)45, task.ReadWord(20));
    }

    private static NativeBattleModulePoolTask Task()
    {
        return new(1, 0, null, 0x68DBE0, new byte[12].ToImmutableArray());
    }
}
