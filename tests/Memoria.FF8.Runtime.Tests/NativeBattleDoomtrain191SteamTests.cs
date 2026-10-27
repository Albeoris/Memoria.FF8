using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191SteamTests
{
    [Fact]
    public void Constructors_publish_both_native_profiles_and_preserve_unwritten_task_WORDs()
    {
        NativeBattleDoomtrain191Frame frame = Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).ToImmutableArray() });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        frame = NativeBattleDoomtrain191Steam.Spawn(NativeBattleDoomtrain191Steam.Spawn(frame, false), true);
        NativeBattleModulePoolTask first = frame.Effect.Pools[1].Slots[1];
        NativeBattleModulePoolTask second = frame.Effect.Pools[1].Slots[2];
        Assert.Equal(0x641580u, first.Callback);
        Assert.Equal(0x6418E0u, second.Callback);
        Assert.Equal((ushort)1, first.ReadWord(0xE));
        Assert.Equal((ushort)2, second.ReadWord(0xE));
        Assert.Equal((ushort)0xF830, first.ReadWord(0x14));
        Assert.Equal((ushort)0x8AD0, second.ReadWord(0x14));
        Assert.Equal((ushort)2000, first.ReadWord(0x1C));
        Assert.Equal((ushort)4096, first.ReadWord(0x1E));
        Assert.Equal((ushort)2048, second.ReadWord(0x1E));
        Assert.Equal((ushort)0xA5A5, first.ReadWord(0x16));
        Assert.Equal((ushort)0xA5A5, second.ReadWord(0x18));
        Assert.Equal((ushort)0xA5A5, second.ReadWord(0x1C));
    }

    [Fact]
    public void Curved_steam_draws_before_one_pair_spawn_and_updates_exact_signed_wave_parameters()
    {
        NativeBattleDoomtrain191Frame frame = Task(false, 0);
        int calls = 0;
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0, (_, _) => calls++);
        Assert.Equal(0, calls);
        Assert.Equal((short)-2000, Word(result.Frame, 8));
        Assert.Equal((short)-2000, Word(result.Frame, 12));
        Assert.Equal((short)2000, Word(result.Frame, 32));
        Assert.Equal((short)-2000, Word(result.Frame, 36));
        NativeBattleModulePoolTask task = result.Frame.Effect.Pools[1].Slots[0];
        Assert.Equal((ushort)1, task.ReadWord(0xC));
        Assert.Equal((ushort)0, task.ReadWord(0x10));
        Assert.Equal(unchecked((ushort)-20), task.ReadWord(0x12));
        Assert.Equal((ushort)0, task.ReadWord(0x14));
        Assert.Equal((ushort)160, task.ReadWord(0x18));
        Assert.Equal((ushort)1910, task.ReadWord(0x1C));
        Assert.Equal((ushort)4008, task.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)-91), task.ReadWord(0x20));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Straight_steam_spawns_two_pairs_and_copies_retained_position_padding_without_clearing_tail()
    {
        NativeBattleDoomtrain191Frame frame = Task(true, 0);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(0x16, 0x1234);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, task) })) } };
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0);
        Assert.Equal(new short[] { -900, 900, -900, 900 }, Enumerable.Range(0, 4).Select(index => Word(result.Frame, index * 24 + 8)));
        Assert.Equal(new short[] { -30000, -30000, -28500, -28500 }, Enumerable.Range(0, 4).Select(index => Word(result.Frame, index * 24 + 12)));
        Assert.All(Enumerable.Range(0, 4), index => Assert.Equal((ushort)0x1234, unchecked((ushort)Word(result.Frame, index * 24 + 14))));
        Assert.Equal((short)-27000, unchecked((short)result.Frame.Effect.Pools[1].Slots[0].ReadWord(0x14)));
        Assert.Equal((ushort)2016, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0x1E));
        Assert.Equal(unchecked((short)0xA5A5), Word(result.Frame, 16));
    }

    [Theory]
    [InlineData(false, 32767)]
    [InlineData(true, 51000)]
    public void Paused_draw_uses_profile_IR_or_MAC_translation_and_keeps_all_pool_counters(bool pair, int expectedX)
    {
        NativeBattleDoomtrain191Frame frame = Particle(Task(pair, 3), pair ? 2u : 1u, 0, 1000);
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191!;
        frame = frame with { Memory = frame.Memory with { Doomtrain191 = state with { BaseView = NativeBattleDoomtrain191Controller.Pack(Identity with { X = 50000 }, state.BaseView, true) } } };
        int calls = 0;
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 1, (_, draw) =>
        {
            calls++;
            Assert.Equal(expectedX, draw.Matrix.X);
            Assert.Equal((short)4096, draw.Matrix.Rotation[0]);
            Assert.Equal((ushort)0, draw.Phase);
        });
        Assert.Equal(1, calls);
        Assert.Equal(0, result.Result);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Memory.Arena), NativeBattleTestSnapshots.Json(result.Frame.Memory.Arena));
        Assert.Equal((ushort)3, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal(50000, result.Frame.Memory.ScriptedLighting!.BackColor!.Red);
    }

    [Fact]
    public void Terminal_sprite_directory_resets_age_only_after_real_draw()
    {
        NativeBattleDoomtrain191Frame frame = Particle(Task(false, 18), 1, 1, 0);
        int calls = 0;
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0, (current, draw) =>
        {
            calls++;
            Assert.Equal((ushort)1, draw.Phase);
            Assert.Equal((short)1, Word(current, 4));
        });
        Assert.Equal(1, calls);
        Assert.Equal((short)0, Word(result.Frame, 4));
        Assert.Equal(2, result.Result);
    }

    [Fact]
    public void Native_contiguous_pair_write_does_not_search_or_reject_second_slot()
    {
        NativeBattleDoomtrain191Frame frame = Task(false, 0);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 169; index++)
        {
            arena = arena.Write(Start + index * 24, BitConverter.GetBytes(2u));
        }
        arena = arena.Write(Start + 170 * 24, BitConverter.GetBytes(0xDEADBEEFu));
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0);
        Assert.Equal(1u, Dword(result.Frame, 169 * 24));
        Assert.Equal(1u, Dword(result.Frame, 170 * 24));
        Assert.Equal((short)2000, Word(result.Frame, 170 * 24 + 8));
    }

    [Theory]
    [InlineData(false, 17)]
    [InlineData(true, 89)]
    public void Completion_clears_only_records_matching_this_tasks_mask_and_roundtrips(bool pair, ushort age)
    {
        uint mask = pair ? 2u : 1u;
        uint other = pair ? 1u : 2u;
        NativeBattleDoomtrain191Frame frame = Particle(Task(pair, age), mask, 0, 0);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 1; index < 170; index++)
        {
            arena = arena.Write(Start + index * 24, BitConverter.GetBytes(other));
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        string replay = NativeBattleTestSnapshots.Json(frame);
        NativeBattleDoomtrain191LeafStep result = Advance(frame, 0);
        Assert.Equal(2, result.Result);
        Assert.Equal(0u, Dword(result.Frame, 0));
        Assert.Equal(other, Dword(result.Frame, 24));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Advance(NativeBattleTestSnapshots.RestoreJson<NativeBattleDoomtrain191Frame>(replay)!, 0)));
    }

    private const int Start = 0x2E000;
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleDoomtrain191Frame Fixture()
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Tests.Initialize(NativeBattleDoomtrain191Tests.Fixture());
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191!;
        return frame with { Memory = frame.Memory with { Doomtrain191 = state with { BaseView = NativeBattleDoomtrain191Controller.Pack(Identity, state.BaseView, true) } } };
    }
    private static NativeBattleDoomtrain191Frame Task(bool pair, ushort age)
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Steam.Spawn(Fixture(), pair);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = (NativeBattleTaskFixture.EditTask(pool.Slots[0], taskData => taskData with { Callback = pool.Slots[1].Callback, Payload = NativeBattleStorageSnapshots.Capture(pool.Slots[1]).Payload, Next = null })).WriteWord(0xC, age);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, task).SetItem(1, NativeBattleTaskFixture.EditTask(pool.Slots[1], taskData => taskData with { Flags = 0 })), Tail = 0 });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
    }
    private static NativeBattleDoomtrain191Frame Particle(NativeBattleDoomtrain191Frame frame, uint flags, short age, short x)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), age);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Start, bytes) } };
    }
    private static NativeBattleDoomtrain191LeafStep Advance(NativeBattleDoomtrain191Frame frame, uint flags, Action<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191SteamDraw>? draw = null)
    {
        return NativeBattleDoomtrain191Steam.Advance(frame, 0, Resource(), new(() => flags, (current, value) =>
        {
            draw?.Invoke(current, value);
            return current;
        }));
    }
    private static NativeBattleModuleResource Resource()
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 0xFFFF);
        return new(0, 0xE33E40, 0, 0, bytes.ToImmutableArray());
    }
    private static short Word(NativeBattleDoomtrain191Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(Start + offset, 2));
    private static uint Dword(NativeBattleDoomtrain191Frame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(Start + offset, 4));
}
