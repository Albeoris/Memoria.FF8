using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97StageFadeTests
{
    [Theory]
    [InlineData(0x731630u, -1, 0, 25)]
    [InlineData(0x731A50u, 0, 0x600, 13)]
    [InlineData(0x731B40u, 0x600, 0xC00, 7)]
    [InlineData(0x7292D0u, -1, 0, 17)]
    [InlineData(0x7296D0u, 0, 0x600, 13)]
    [InlineData(0x7297C0u, 0x600, 0xC00, 7)]
    public void Original_profiles_preserve_initial_stage_write_timing_and_every_cold_next(uint callback, int firstDepth, int finalDepth, int expectedCallbacks)
    {
        NativeBattleModulePoolTask task = Task(callback);
        NativeBattleChoco97StageFadeStep first = NativeBattleChoco97StageFade.Advance(task);
        Assert.Equal(firstDepth == -1 ? null : (short?)firstDepth, first.Depth);
        Assert.Equal(firstDepth != -1, first.ClearRgb);
        task = first.Task;
        int callbacks = 1;
        NativeBattleChoco97StageFadeStep step;
        do
        {
            NativeBattleModulePoolTask cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
            step = NativeBattleChoco97StageFade.Advance(task);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleChoco97StageFade.Advance(cold)));
            Assert.False(step.ClearRgb);
            task = step.Task;
            callbacks++;
        }
        while (!step.ReadyToComplete && callbacks < 64);
        Assert.True(step.ReadyToComplete);
        Assert.Equal(expectedCallbacks, callbacks);
        Assert.Equal((short)finalDepth, step.Depth);
        Assert.Equal((byte)2, task.ReadByte(0x29));
        NativeBattleChoco97StageFadeStep after = NativeBattleChoco97StageFade.Advance(task);
        Assert.Null(after.Depth);
        Assert.False(after.ClearRgb);
        Assert.True(after.ReadyToComplete);
    }

    [Fact]
    public void Wrapped_word_counter_and_child_hold_do_not_add_another_fade_write()
    {
        NativeBattleModulePoolTask task = Task(0x731630).WriteByte(0x29, 1).WriteWord(0x1C, 0x80).WriteWord(0x24, ushort.MaxValue).WriteByte(0x28, 1);
        NativeBattleChoco97StageFadeStep held = NativeBattleChoco97StageFade.Advance(task);
        Assert.Equal((short)0, held.Depth);
        Assert.Equal((ushort)0, held.Task.ReadWord(0x24));
        Assert.False(held.ReadyToComplete);
        NativeBattleChoco97StageFadeStep done = NativeBattleChoco97StageFade.Advance(held.Task.WriteByte(0x28, 0));
        Assert.Null(done.Depth);
        Assert.True(done.ReadyToComplete);
    }

    [Fact]
    public void Fadein_addition_wraps_before_its_signed_limit_comparison()
    {
        NativeBattleChoco97StageFadeStep step = NativeBattleChoco97StageFade.Advance(Task(0x731A50).WriteByte(0x29, 1).WriteWord(0x1C, 0x7FFF));
        Assert.Equal(unchecked((short)0x807F), step.Depth);
        Assert.False(step.ReadyToComplete);
        Assert.Equal((byte)1, step.Task.ReadByte(0x29));
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[0x70 - 12].ToImmutableArray());
}
