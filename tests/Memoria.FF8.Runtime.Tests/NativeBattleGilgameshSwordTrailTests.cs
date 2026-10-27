using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSwordTrailTests
{
    [Fact]
    public void Appender_clears_only_ten_active_DWORDs_and_task_counter_without_clearing_history_or_pointer_arrays()
    {
        NativeBattleEffectArena memory = NativeBattleEffectArena.Clear().Write(NativeBattleGilgameshSwordTrailState.History, Enumerable.Repeat((byte)0xA5, 200).ToArray()).Write(NativeBattleGilgameshSwordTrailState.GatherA, Enumerable.Repeat((byte)0xA5, 80).ToArray());
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).ToImmutableArray() });
        NativeBattleGilgameshSwordTrailInitialization initialized = NativeBattleGilgameshSwordTrail.Initialize5943A0(pool, new(memory));
        for (int index = 0; index < 10; index++)
        {
            Assert.Equal(new byte[4], initialized.State.Memory.Read(NativeBattleGilgameshSwordTrailState.History + index * 20, 4));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 16).ToArray(), initialized.State.Memory.Read(NativeBattleGilgameshSwordTrailState.History + index * 20 + 4, 16));
        }
        Assert.Equal(memory.Read(0, 80), initialized.State.Memory.Read(0, 80));
        Assert.Equal(0x5943D0u, initialized.Pool.Slots[0].Callback);
        Assert.Equal((ushort)0, initialized.Pool.Slots[0].ReadWord(12));
        Assert.Equal((ushort)0xA5A5, initialized.Pool.Slots[0].ReadWord(14));
    }

    [Fact]
    public void First_history_sample_normalizes_B_to1400_and_copies_unknown_temporary_W_without_pool_publication()
    {
        List<(int, byte, short)> calls = [];
        NativeBattleGilgameshSwordTrailStep step = NativeBattleGilgameshSwordTrail.Step5943D0(NativeBattleGilgameshSwordTrailState.FromColdProcess(), Task(0), 3, (selector, bone, fraction) =>
        {
            calls.Add((selector, bone, fraction));
            return new((short)(10 + fraction / 4096 * 100), 20, 30);
        }, null!, null!, null, null!, null!, 0);
        Assert.Equal(new (int, byte, short)[] { (3, 1, 0), (3, 1, 4096) }, calls);
        Assert.Equal(1, step.GatheredCount);
        Assert.Null(step.PoolCursorAdvance);
        Assert.Empty(step.Packets.Attempts);
        Assert.Equal((ushort)1, step.UpdatedTask.ReadWord(12));
        Assert.Equal((short)10, Word(step.State.Memory, NativeBattleGilgameshSwordTrailState.History + 4));
        Assert.Equal((short)1410, Word(step.State.Memory, NativeBattleGilgameshSwordTrailState.History + 12));
        Assert.Throws<NotSupportedException>(() => step.State.Memory.Read(NativeBattleGilgameshSwordTrailState.History + 10, 2));
        Assert.Throws<NotSupportedException>(() => step.State.Memory.Read(NativeBattleGilgameshSwordTrailState.GatherA + 6, 2));
        Assert.Equal(new byte[8], step.State.Memory.Read(NativeBattleGilgameshSwordTrailState.CurveA, 8));
    }

    [Fact]
    public void Paused_history_and_curve_updates_repeat_current_slot_and_json_next_is_exact()
    {
        NativeBattleGilgameshSwordTrailStep first = Run(NativeBattleGilgameshSwordTrailState.FromColdProcess(), Task(0), 10);
        NativeBattleGilgameshSwordTrailStep paused = Run(first.State, first.UpdatedTask, 20, 0x201);
        Assert.Same(first.UpdatedTask, paused.UpdatedTask);
        Assert.Equal(2, paused.GatheredCount);
        Assert.Equal(60, paused.Packets.Attempts.Length);
        Assert.Equal(2196, paused.PoolCursorAdvance);
        NativeBattleGilgameshSwordTrailStep repeated = Run(paused.State, paused.UpdatedTask, 30, 0x201);
        Assert.Equal(2, repeated.GatheredCount);
        Assert.Equal((short)30, Word(repeated.State.Memory, NativeBattleGilgameshSwordTrailState.GatherA));
        Assert.Equal((short)10, Word(repeated.State.Memory, NativeBattleGilgameshSwordTrailState.GatherA + 8));
        Assert.Equal((short)30, Word(repeated.State.Memory, NativeBattleGilgameshSwordTrailState.CurveA));
        Assert.Equal((short)10, Word(repeated.State.Memory, NativeBattleGilgameshSwordTrailState.CurveA + 31 * 8));
        NativeBattleGilgameshSwordTrailStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGilgameshSwordTrailStep>(NativeBattleTestSnapshots.Json(repeated))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Run(repeated.State, repeated.UpdatedTask, 40)), NativeBattleTestSnapshots.Json(Run(restored.State, restored.UpdatedTask, 40)));
    }

    [Fact]
    public void Reverse_history_wraps_zero_to_nine_stops_at_first_inactive_and_preserves_copied_known_W()
    {
        NativeBattleEffectArena memory = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 80).ToArray());
        memory = Seed(memory, 9, 900);
        memory = Seed(memory, 8, 800);
        NativeBattleGilgameshSwordTrailStep step = Run(new(memory), Task(0), 100);
        Assert.Equal(3, step.GatheredCount);
        Assert.Equal((short)100, Word(step.State.Memory, 0));
        Assert.Equal((short)900, Word(step.State.Memory, 8));
        Assert.Equal((short)800, Word(step.State.Memory, 16));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, step.State.Memory.Read(14, 2));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 56).ToArray(), step.State.Memory.Read(24, 56));
        Assert.Equal((short)800, Word(step.State.Memory, NativeBattleGilgameshSwordTrailState.CurveA + 31 * 8));
    }

    [Fact]
    public void Ten_native_callbacks_retire_after_the_last_draw_and_negative_counter_is_unsupported_before_bone_reads()
    {
        NativeBattleGilgameshSwordTrailState state = NativeBattleGilgameshSwordTrailState.FromColdProcess();
        NativeBattleModulePoolTask task = Task(0);
        for (int frame = 0; frame < 10; frame++)
        {
            NativeBattleGilgameshSwordTrailStep step = Run(state, task, (short)(frame * 10));
            Assert.Equal(frame == 9 ? 2 : 0, step.Result);
            Assert.Equal(frame + 1, step.GatheredCount);
            if (frame > 0)
            {
                Assert.Equal(60, step.Packets.Attempts.Length);
            }
            state = step.State;
            task = step.UpdatedTask;
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshSwordTrail.Step5943D0(state, Task(65535), 3, (_, _, _) => throw new InvalidOperationException(), null!, null!, null, null!, null!, 0));
    }

    [Fact]
    public void Missing_imported_banks_or_color_table_do_not_hydrate_and_state_regions_are_bounded()
    {
        Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleGilgameshSwordTrailState>("{}"));
        Assert.Throws<JsonException>(() => NativeBattleTestSnapshots.RestoreJson<NativeBattleGilgameshSwordTrailResources>("{}"));
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshSwordTrailState(NativeBattleEffectArena.Clear().Write(0x200, new byte[4])).Validate());
    }

    private static NativeBattleGilgameshSwordTrailStep Run(NativeBattleGilgameshSwordTrailState state, NativeBattleModulePoolTask task, short x, uint flags = 0) => NativeBattleGilgameshSwordTrail.Step5943D0(state, task, 3, (_, _, fraction) => new((short)(x + fraction / 4096 * 100), -100, 0), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096), new(null, 288, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), NativeBattleGteColorInterpolation.CodeConstants45F270, new([0x3A602020, 0x3A407070, 0x3A903030, 0x3A303090]), flags);
    private static NativeBattleModulePoolTask Task(ushort counter) => new NativeBattleModulePoolTask(1, 0, null, 0x5943D0, new byte[24].ToImmutableArray()).WriteWord(12, counter);
    private static short Word(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(offset, 2));

    private static NativeBattleEffectArena Seed(NativeBattleEffectArena memory, int slot, short x)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 20).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), -100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 0);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), (short)(x + 1400));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), -100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 0);
        return memory.Write(NativeBattleGilgameshSwordTrailState.History + slot * 20, bytes);
    }
}
