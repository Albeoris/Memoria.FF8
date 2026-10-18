using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshCommonTasksTests
{
    [Fact]
    public void Initializers_append_actual_callbacks_and_preserve_unwritten_suffix()
    {
        NativeBattleEffectTaskPool pool = Pool();
        pool = NativeBattleGilgameshCommonTasks.Append5929B0(pool, 0, 0xFFFF);
        pool = NativeBattleGilgameshCommonTasks.Append5929B0(pool, -1, 92);
        pool = NativeBattleGilgameshCommonTasks.Append592AB0(pool);
        pool = NativeBattleGilgameshCommonTasks.Append592B30(pool);
        Assert.Equal(new uint[] { 0x592A00, 0x592A50, 0x592AE0, 0x592B60 }, pool.Slots.Take(4).Select(task => task.Callback));
        Assert.Equal(new ushort[] { 0xFFFF, 92, 16, 16 }, pool.Slots.Take(4).Select(task => task.ReadWord(0x0E)));
        for (int slot = 0; slot < 4; slot++)
        {
            Assert.Equal((ushort)0, pool.Slots[slot].ReadWord(0x0C));
            for (int offset = 0x10; offset < 0x24; offset += 2)
            {
                Assert.Equal(slot >= 2 && offset == 0x18 ? (ushort)2048 : (ushort)0xA5A5, pool.Slots[slot].ReadWord(offset));
            }
        }
        Assert.Equal(0, pool.Head);
        Assert.Equal(3, pool.Tail);
        NativeBattleEffectTaskPool restored = JsonSerializer.Deserialize<NativeBattleEffectTaskPool>(JsonSerializer.Serialize(pool))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(pool), JsonSerializer.Serialize(restored));
        NativeBattleEffectTaskPool full = pool;
        for (int slot = 4; slot < 100; slot++)
        {
            (full, _) = full.Append(1);
        }
        Assert.Throws<InvalidOperationException>(() => NativeBattleGilgameshCommonTasks.Append592AB0(full));
    }

    [Theory]
    [InlineData(0x592A00u, -1, 1)]
    [InlineData(0x592A00u, 0, 1)]
    [InlineData(0x592A50u, -1, 0)]
    [InlineData(0x592A50u, 0, 0)]
    [InlineData(0x592A50u, 1, 1)]
    public void Fade_draws_before_pause_with_native_arguments_and_signed_positive_gate(uint callback, short counter, int calls)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(0x0C, unchecked((ushort)counter)).WriteWord(0x0E, 43);
        List<int[]> effects = [];
        NativeBattleGilgameshCommonStep step = NativeBattleGilgameshCommonTasks.StepFade(task, 0x200, (a, b, c, d, e) => effects.Add([a, b, c, d, e]));
        Assert.Equal(calls, effects.Count);
        Assert.All(effects, effect => Assert.Equal(new[] { 0, 255, 255, 255, 63 }, effect));
        Assert.Same(task, step.UpdatedTask);
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(42, 43, 43, 2)]
    [InlineData(32767, 16, -32768, 0)]
    [InlineData(-1, -1, 0, 2)]
    public void Fade_completion_uses_wrapped_signed_counter_and_retains_suffix(short counter, short duration, short next, int result)
    {
        NativeBattleModulePoolTask task = Task(0x592A00).WriteWord(0x0C, unchecked((ushort)counter)).WriteWord(0x0E, unchecked((ushort)duration));
        NativeBattleGilgameshCommonStep step = NativeBattleGilgameshCommonTasks.StepFade(task, 0, (_, _, _, _, _) => { });
        Assert.Equal(unchecked((ushort)next), step.UpdatedTask.ReadWord(0x0C));
        Assert.Equal(result, step.Result);
        Assert.Equal(task.Payload.AsSpan().Slice(2).ToArray(), step.UpdatedTask.Payload.AsSpan().Slice(2).ToArray());
    }

    [Theory]
    [InlineData(0x592AE0u, 10, 3, 2, 6)]
    [InlineData(0x592B60u, 10, 3, 2, 4)]
    [InlineData(0x592AE0u, -10, 3, 2, -6)]
    [InlineData(0x592B60u, -10, 3, 2, -4)]
    [InlineData(0x592AE0u, 10, -3, 2, -6)]
    public void Stage_divides_signed_extent_before_multiplication_and_calls_before_pause(uint callback, short extent, short duration, short counter, int expected)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(0x0C, unchecked((ushort)counter)).WriteWord(0x0E, unchecked((ushort)duration)).WriteWord(0x18, unchecked((ushort)extent));
        List<(int, int)> effects = [];
        NativeBattleGilgameshCommonStep step = NativeBattleGilgameshCommonTasks.StepStage(task, 1, (value, second) => effects.Add((value, second)));
        Assert.Equal(new[] { (expected, 0) }, effects);
        Assert.Same(task, step.UpdatedTask);
        Assert.Equal(0, step.Result);
        Assert.Throws<DivideByZeroException>(() => NativeBattleGilgameshCommonTasks.StepStage(task.WriteWord(0x0E, 0), 1, (_, _) => throw new InvalidOperationException()));
    }

    [Theory]
    [InlineData(0x592AE0u, 1)]
    [InlineData(0x592B60u, 2)]
    public void Stage_terminal_draw_precedes_increment_and_only_reverse_task_resets(uint callback, int calls)
    {
        NativeBattleModulePoolTask task = Task(callback).WriteWord(0x0C, 15).WriteWord(0x0E, 16).WriteWord(0x18, 2048);
        List<int> values = [];
        NativeBattleGilgameshCommonStep step = NativeBattleGilgameshCommonTasks.StepStage(task, 0, (value, _) => values.Add(value));
        Assert.Equal(calls, values.Count);
        Assert.Equal(callback == 0x592AE0 ? 1920 : 128, values[0]);
        if (calls == 2)
        {
            Assert.Equal(0, values[1]);
        }
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)16, step.UpdatedTask.ReadWord(0x0C));
        Assert.Equal(task.Payload.AsSpan().Slice(2).ToArray(), step.UpdatedTask.Payload.AsSpan().Slice(2).ToArray());
    }

    [Fact]
    public void Texture_task_uses_real_completion_pointer_and_retains_pending_state_across_JSON()
    {
        NativeBattleModulePoolTask task = Task(0x596B30).WriteWord(0x0C, 0).WriteWord(0x0E, 0);
        int scheduled = 0;
        NativeBattleModulePoolTask Queue(uint texture, uint completion, NativeBattleModulePoolTask current)
        {
            Assert.Equal(0x300E000u, texture);
            Assert.Equal(0x21FFD76u, completion);
            scheduled++;
            return current;
        }
        NativeBattleGilgameshCommonStep first = NativeBattleGilgameshCommonTasks.Step596B30(task, 0x3000000, 0x21FFD68, Queue);
        Assert.Equal(0, scheduled);
        Assert.Equal(0, first.Result);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.UpdatedTask))!;
        NativeBattleGilgameshCommonStep second = NativeBattleGilgameshCommonTasks.Step596B30(restored, 0x3000000, 0x21FFD68, Queue);
        Assert.Equal(1, scheduled);
        Assert.Equal(0, second.Result);
        Assert.Equal((ushort)0, second.UpdatedTask.ReadWord(0x0E));
        NativeBattleModulePoolTask completed = second.UpdatedTask.WriteWord(0x0E, 1);
        NativeBattleGilgameshCommonStep third = NativeBattleGilgameshCommonTasks.Step596B30(completed, 0x3000000, 0x21FFD68, Queue);
        Assert.Equal(1, scheduled);
        Assert.Equal(2, third.Result);
        Assert.Equal((ushort)3, third.UpdatedTask.ReadWord(0x0C));
        Assert.Equal(completed.Payload.AsSpan().Slice(2).ToArray(), third.UpdatedTask.Payload.AsSpan().Slice(2).ToArray());
    }

    [Fact]
    public void Texture_completion_tests_whole_WORD_and_allows_only_service_owned_low_BYTE()
    {
        NativeBattleModulePoolTask task = Task(0x596B30).WriteWord(0x0C, 1).WriteWord(0x0E, 0xA500);
        NativeBattleGilgameshCommonStep highOnly = NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current);
        Assert.Equal(2, highOnly.Result);
        NativeBattleGilgameshCommonStep lowWrite = NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current.WriteWord(0x0E, 0xA501));
        Assert.Equal((ushort)0xA501, lowWrite.UpdatedTask.ReadWord(0x0E));
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current.WriteWord(0x0E, 1)));
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current.WriteWord(0x10, 1)));
        NativeBattleGilgameshCommonStep wrap = NativeBattleGilgameshCommonTasks.Step596B30(task.WriteWord(0x0C, 0xFFFF).WriteWord(0x0E, 0), 0, 0, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal((ushort)0, wrap.UpdatedTask.ReadWord(0x0C));
        Assert.Equal(0, wrap.Result);
    }

    [Fact]
    public void Four_actor_BYTE_operations_change_only_bit_two()
    {
        ImmutableArray<byte> flags = [0, 1, 0xFE, 0xFF];
        Assert.Equal(new byte[] { 2, 3, 0xFE, 0xFF }, NativeBattleGilgameshCommonTasks.SetActorFlags5904D0(flags));
        Assert.Equal(new byte[] { 0, 1, 0xFC, 0xFD }, NativeBattleGilgameshCommonTasks.ClearActorFlags5904F0(flags));
        Assert.Equal(new byte[] { 0, 1, 0xFE, 0xFF }, flags);
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0xBEEF, null, callback, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray());

    private static NativeBattleEffectTaskPool Pool() => new(0x21FFD58, 0x21FFD68, 0x24, Enumerable.Range(0, 100).Select(_ => Task(0) with { Flags = 0 }).ToImmutableArray(), null, null);
}
