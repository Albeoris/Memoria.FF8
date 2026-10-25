using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312EarlyTests
{
    [Theory]
    [InlineData(0x59D680u, 1)]
    [InlineData(0x59D680u, 11)]
    [InlineData(0x59D7B0u, 4)]
    [InlineData(0x59D7B0u, 17)]
    [InlineData(0x59D930u, 0)]
    [InlineData(0x59DD30u, 1)]
    [InlineData(0x59DD30u, 15)]
    public void Middle_age_draws_preserve_the_previous_temporary_fade(uint callback, short age)
    {
        var result = NativeBattleEffect312Early.MeshFade(callback, age);
        Assert.Equal(0x33u, result.Flags);
        Assert.Null(result.Fade);
    }

    [Theory]
    [InlineData(0x59D680u, 0, 0xF3u, 2048)]
    [InlineData(0x59D680u, 12, 0xF3u, 0)]
    [InlineData(0x59D7B0u, 0, 0x33u, 4288)]
    [InlineData(0x59D7B0u, 18, 0xF3u, 0)]
    [InlineData(0x59D930u, 1, 0xF3u, 0)]
    [InlineData(0x59D930u, 11, 0xF3u, 3720)]
    [InlineData(0x59DD30u, 16, 0xF3u, 0)]
    public void Boundary_draws_select_the_original_flags_and_fade(uint callback, short age, uint flags, int fade)
    {
        Assert.Equal((flags, (int?)fade), NativeBattleEffect312Early.MeshFade(callback, age));
    }

    [Fact]
    public void Negative_spin_decay_divides_toward_zero_and_wraps_the_angle_WORD()
    {
        NativeBattleModulePoolTask task = Task(0x59D7B0).WriteWord(0x18, unchecked((ushort)-32760)).WriteWord(0x1A, unchecked((ushort)-15));
        var result = NativeBattleEffect312Early.UpdateMotion(task);
        Assert.Equal(32761, result.Task.ReadWord(0x18));
        Assert.Equal(-14, unchecked((short)result.Task.ReadWord(0x1A)));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Initial_beam_step_samples_twenty_four_positions_before_completion()
    {
        NativeBattleModulePoolTask task = Task(0x59DD30).WriteWord(0x18, 0).WriteWord(0x1A, 2);
        for (int tick = 0; tick < 24; tick++)
        {
            Assert.Equal(tick * 2, task.ReadWord(0x18));
            var result = NativeBattleEffect312Early.UpdateMotion(task);
            Assert.Equal(tick == 23 ? 2 : 0, result.Result);
            task = result.Task;
        }
        Assert.Equal(24, task.ReadWord(12));
        Assert.Equal(48, task.ReadWord(0x18));
    }

    [Theory]
    [InlineData(0x59D680u, 20)]
    [InlineData(0x59D7B0u, 26)]
    [InlineData(0x59D930u, 12)]
    public void Mesh_task_lifetime_ends_after_its_last_rendered_age(uint callback, int ticks)
    {
        NativeBattleModulePoolTask task = Task(callback);
        for (int tick = 0; tick < ticks; tick++)
        {
            var result = NativeBattleEffect312Early.UpdateMotion(task);
            Assert.Equal(tick == ticks - 1 ? 2 : 0, result.Result);
            task = result.Task;
        }
        Assert.Equal(ticks, task.ReadWord(12));
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[24].ToImmutableArray());
}
