using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaLateChildrenTests
{
    [Fact]
    public void Spawner_obeys_pause_then_publishes_the_actual_child_slot_and_sparse_raw_fields()
    {
        NativeBattleShivaEarlyTaskFrame source = Fixture(0x5C6690, 0);
        NativeBattleShivaEarlyTaskStep paused = NativeBattleShivaLateChildren.Advance5C6690(source, 0, 0x201, []);
        Assert.Equal(NativeBattleTestSnapshots.Json(source), NativeBattleTestSnapshots.Json(paused.Frame));
        NativeBattleShivaEarlyTaskStep first = NativeBattleShivaLateChildren.Advance5C6690(source, 0, 0, []);
        Assert.Equal(new NativeCrtRandomState(0xE7847115), first.Frame.Random);
        Assert.Equal(0x22BC1CCu, first.Frame.Memory.Shiva!.SpawnedChildD32508);
        NativeBattleModulePoolTask owner = first.Frame.Pool.Slots[1];
        NativeBattleModulePoolTask moving = first.Frame.Pool.Slots[2];
        Assert.Equal(0x5C6870u, owner.Callback);
        Assert.Equal((ushort)10, owner.ReadWord(0x10));
        Assert.Equal(unchecked((ushort)-130), owner.ReadWord(0x12));
        Assert.Equal((ushort)30, owner.ReadWord(0x14));
        Assert.Equal((ushort)0xA5A5, owner.ReadWord(0x16));
        Assert.Equal((ushort)0xB6B6, owner.ReadWord(0x0E));
        Assert.Equal((ushort)41, owner.ReadWord(0x18));
        Assert.Equal(unchecked((ushort)-97), owner.ReadWord(0x1A));
        Assert.Equal((ushort)4096, owner.ReadWord(0x1C));
        Assert.Equal(0x5C6BB0u, moving.Callback);
        Assert.Equal((ushort)2238, moving.ReadWord(0x18));
        Assert.Equal((ushort)60, moving.ReadWord(0x1A));
        Assert.Equal((ushort)0xB6B6, moving.ReadWord(0x1C));
        byte[] rows = new byte[16];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 4), (short)(index + 1));
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 4 + 2), (short)(-10 + index));
        }
        NativeBattleShivaEarlyTaskStep second = NativeBattleShivaLateChildren.Advance5C6690(first.Frame, 0, 0, rows.ToImmutableArray());
        Assert.Equal(new NativeCrtRandomState(0x81EB59D1), second.Frame.Random);
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModulePoolTask child = second.Frame.Pool.Slots[index + 3];
            Assert.Equal(0x5C69E0u, child.Callback);
            Assert.Equal((ushort)(index + 1), child.ReadWord(0x0E));
            Assert.Equal((ushort)(20 + index), child.ReadWord(0x14));
            Assert.Equal((ushort)0xB6B6, child.ReadWord(0x16));
            Assert.Equal((ushort)0xB6B6, child.ReadWord(0x1C));
            Assert.Equal((ushort)0xB6B6, child.ReadWord(0x1E));
        }
    }

    [Theory]
    [InlineData(32000, 1000, -32536)]
    [InlineData(28000, 1000, 28672)]
    public void Growing_mesh_draws_before_pause_and_clamps_only_after_signed_WORD_wrap(short before, short addition, short expected)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0x5C6870, 22);
        NativeBattleModulePoolTask task = frame.Pool.Slots[0].WriteWord(0x18, 0).WriteWord(0x1A, 1).WriteWord(0x1C, unchecked((ushort)before)).WriteWord(0x1E, unchecked((ushort)addition)).WriteWord(0x20, unchecked((ushort)before)).WriteWord(0x22, unchecked((ushort)addition));
        frame = Set(frame, 0, task);
        int draws = 0;
        NativeBattleShivaEarlyTaskStep paused = NativeBattleShivaLateChildren.Advance5C6870(frame, 0, 0x201, NativeBattleCuragaMatrices.Identity8DD770(), new(0, 0xD21B9C, [0]), (current, draw) =>
        {
            draws++;
            Assert.Equal(0xF3u, draw.Context.Flags);
            Assert.Equal(0, draw.Context.Fade);
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(paused.Frame));
        NativeBattleShivaEarlyTaskStep grown = NativeBattleShivaLateChildren.Advance5C6870(frame, 0, 0, NativeBattleCuragaMatrices.Identity8DD770(), new(0, 0xD21B9C, [0]), (current, _) => current);
        Assert.Equal(unchecked((ushort)expected), grown.Frame.Pool.Slots[0].ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)expected), grown.Frame.Pool.Slots[0].ReadWord(0x20));
    }

    [Fact]
    public void Delayed_mesh_samples_the_live_freed_slot_and_reinitializes_on_every_paused_counter_zero_callback()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0x5C69E0, 0);
        frame = Set(frame, 0, frame.Pool.Slots[0].WriteWord(0x0E, 1));
        NativeBattleShivaEarlyTaskStep delayed = NativeBattleShivaLateChildren.Advance5C69E0(frame, 0, 0, null!, null!, null!);
        Assert.Equal((ushort)0, delayed.Frame.Pool.Slots[0].ReadWord(0x0E));
        Assert.Equal(frame.Random, delayed.Frame.Random);
        frame = delayed.Frame with { Memory = delayed.Frame.Memory with { Shiva = delayed.Frame.Memory.Shiva! with { SpawnedChildD32508 = 0x22BC1CC } } };
        frame = Set(frame, 1, frame.Pool.Slots[1].WriteWord(0x1C, unchecked((ushort)-1000)));
        Assert.Equal((ushort)0, (ushort)(frame.Pool.Slots[1].Flags & 1));
        NativeBattleShivaEarlyTaskStep first = NativeBattleShivaLateChildren.Advance5C69E0(frame, 0, 0x201, NativeBattleCuragaMatrices.Identity8DD770(), new(0, 0xD3138C, [0]), (current, _) => current);
        Assert.Equal(unchecked((ushort)-191), first.Frame.Pool.Slots[0].ReadWord(0x1C));
        Assert.Equal((ushort)0, first.Frame.Pool.Slots[0].ReadWord(0x0C));
        NativeBattleShivaEarlyTaskFrame changed = Set(first.Frame, 1, first.Frame.Pool.Slots[1].WriteWord(0x1C, unchecked((ushort)-2000)));
        NativeBattleShivaEarlyTaskStep second = NativeBattleShivaLateChildren.Advance5C69E0(changed, 0, 0x201, NativeBattleCuragaMatrices.Identity8DD770(), new(0, 0xD3138C, [0]), (current, _) => current);
        Assert.Equal(unchecked((ushort)-393), second.Frame.Pool.Slots[0].ReadWord(0x1C));
        Assert.Equal(new NativeCrtRandomState(0xC823F683), second.Frame.Random);
        NativeBattleShivaEarlyTaskFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaEarlyTaskFrame>(NativeBattleTestSnapshots.Json(changed))!;
        NativeBattleShivaEarlyTaskStep independent = NativeBattleShivaLateChildren.Advance5C69E0(restored, 0, 0x201, NativeBattleCuragaMatrices.Identity8DD770(), new(0, 0xD3138C, [0]), (current, _) => current);
        Assert.Equal(NativeBattleTestSnapshots.Json(second), NativeBattleTestSnapshots.Json(independent));
    }

    [Fact]
    public void Delayed_mesh_signed_decay_and_completion_follow_the_real_draw()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0x5C69E0, 17);
        frame = Set(frame, 0, frame.Pool.Slots[0].WriteWord(0x0E, 0).WriteWord(0x18, 5).WriteWord(0x1A, unchecked((ushort)-9)).WriteWord(0x1C, 1000).WriteWord(0x1E, unchecked((ushort)-7)).WriteWord(0x20, 4096));
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaLateChildren.Advance5C69E0(frame, 0, 0, NativeBattleCuragaMatrices.Identity8DD770(), new(0, 0xD3138C, [0]), (current, draw) =>
        {
            Assert.Equal(3410, draw.Context.Fade);
            return current;
        });
        Assert.Equal(unchecked((ushort)-4), result.Frame.Pool.Slots[0].ReadWord(0x18));
        Assert.Equal(unchecked((ushort)-7), result.Frame.Pool.Slots[0].ReadWord(0x1A));
        Assert.Equal((ushort)993, result.Frame.Pool.Slots[0].ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-5), result.Frame.Pool.Slots[0].ReadWord(0x1E));
        Assert.Equal(2, result.Result);
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture(uint callback, ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xB6, 24).ToImmutableArray() })).ToImmutableArray() });
        (pool, int slot) = pool.Append(callback);
        NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(pool.Slots[slot], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() });
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, task.WriteWord(0x0C, counter)) });
        NativeBattleShivaProcess process = NativeBattleShivaProcess.FromColdProcess() with { LocalAnchor22BCFC8 = new(10, 20, 30, 40) };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Shiva = process }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleShivaEarlyTaskFrame Set(NativeBattleShivaEarlyTaskFrame frame, int slot, NativeBattleModulePoolTask task) => frame with { Pool = NativeBattleTaskFixture.EditPool(frame.Pool, data => data with { Slots = frame.Pool.Slots.SetItem(slot, task) }) };
}
