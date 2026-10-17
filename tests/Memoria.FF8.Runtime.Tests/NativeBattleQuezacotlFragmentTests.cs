using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlFragmentTests
{
    [Fact]
    public void Native508300_clears_the_entire_EXE_bank_and_preserves_both_surrounding_regions()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { QuezacotlExecutableMemory = new(Enumerable.Repeat((byte)0xA5, NativeBattleQuezacotlExecutableMemory.Length).ToImmutableArray()), QuezacotlSecondaryHeads = [11, 22, 33, 44, 55] };
        NativeBattleQuezacotlFragmentWrite initialized = NativeBattleQuezacotlFragmentPool.Initialize6C4725(memory, Effect());
        int offset = checked((int)(NativeBattleQuezacotlFragmentPool.Bank - NativeBattleQuezacotlExecutableMemory.Address));
        ImmutableArray<byte> bytes = initialized.Memory.QuezacotlExecutableMemory!.Data;
        Assert.All(bytes.AsSpan().Slice(offset, 32 * 44).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.All(bytes.AsSpan().Slice(0, offset).ToArray(), value => Assert.Equal((byte)0xA5, value));
        Assert.All(bytes.AsSpan().Slice(offset + 32 * 44).ToArray(), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(new uint[] { 11, 22, 33, 44, 0 }, initialized.Memory.QuezacotlSecondaryHeads!.Value);
        NativeBattleEffectTaskPool pool = Assert.Single(initialized.Effect.Pools);
        Assert.Null(pool.Head);
        Assert.Null(pool.Tail);
        Assert.Equal(32, pool.Slots.Length);
        Assert.All(pool.Slots, task => Assert.All(task.Payload, value => Assert.Equal((byte)0, value)));
        Assert.Equal(memory.Arena, initialized.Memory.Arena);
    }

    [Fact]
    public void Two_admitted_tasks_consume_exactly_twenty_two_shared_draws_and_link_the_current_physical_EXE_rows()
    {
        NativeBattleQuezacotlFragmentWrite start = Fixture();
        NativeBattleQuezacotlFragmentWrite spawned = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(start.Memory with { QuezacotlPause = 1 }, start.Effect);
        Assert.Equal(31916u, spawned.Memory.SharedEffectRandom!.State);
        Assert.Equal((ushort)1, spawned.Effect.Quezacotl!.Random17A0);
        NativeBattleEffectTaskPool pool = Assert.Single(spawned.Effect.Pools);
        Assert.Equal(0, pool.Head);
        Assert.Equal(1, pool.Tail);
        Assert.Equal(1, pool.Slots[0].Next);
        Assert.Null(pool.Slots[1].Next);
        ImmutableArray<byte> first = NativeBattleQuezacotlFragmentPool.ReadTask(spawned.Memory, 0);
        ImmutableArray<byte> second = NativeBattleQuezacotlFragmentPool.ReadTask(spawned.Memory, 1);
        Assert.Equal(new short[] { 0, -2034, 0, 3406, 2, -214, 8, 12044, 30954, 2640 }, Words(first, [12, 16, 18, 20, 24, 26, 28, 32, 34, 36]));
        Assert.Equal(new byte[] { 30, 180, 242 }, first.AsSpan().Slice(40, 3).ToArray());
        Assert.Equal(new short[] { 0, 1080, 0, 3536, -4, -160, 26, 3118, 29316, 27266 }, Words(second, [12, 16, 18, 20, 24, 26, 28, 32, 34, 36]));
        Assert.Equal(new byte[] { 136, 118, 172 }, second.AsSpan().Slice(40, 3).ToArray());
        Assert.Equal(NativeBattleQuezacotlFragmentPool.Bank + 44, BinaryPrimitives.ReadUInt32LittleEndian(first.AsSpan().Slice(4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(second.AsSpan().Slice(4)));
        Assert.Equal(NativeBattleQuezacotlFragmentPool.Bank, spawned.Memory.QuezacotlSecondaryHeads!.Value[4]);
        Assert.All(new[] { first, second }, task =>
        {
            Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(task.AsSpan()));
            Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(task.AsSpan().Slice(2)));
            Assert.Equal(0x6C6660u, BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan().Slice(8)));
            foreach (int offset in new[] { 14, 15, 22, 23, 30, 31, 38, 39, 43 })
            {
                Assert.Equal((byte)0, task[offset]);
            }
        });
        NativeBattleQuezacotlFragmentWrite restored = JsonSerializer.Deserialize<NativeBattleQuezacotlFragmentWrite>(JsonSerializer.Serialize(spawned))!;
        Assert.Equal(JsonSerializer.Serialize(spawned), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Missing_random_owner_is_strict_only_when_an_allocation_is_admitted()
    {
        NativeBattleQuezacotlFragmentWrite state = Fixture();
        Assert.Contains("7059E0", Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(state.Memory with { SharedEffectRandom = null }, state.Effect)).Message);
        for (int iteration = 0; iteration < 16; iteration++)
        {
            state = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(state.Memory, state.Effect);
        }
        NativeBattleQuezacotlFragmentWrite full = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(state.Memory with { SharedEffectRandom = null }, state.Effect);
        Assert.Null(full.Memory.SharedEffectRandom);
        Assert.Equal(JsonSerializer.Serialize(state.Effect), JsonSerializer.Serialize(full.Effect));
        Assert.Equal(state.Memory.QuezacotlExecutableMemory!.Data.ToArray(), full.Memory.QuezacotlExecutableMemory!.Data.ToArray());
    }

    [Fact]
    public void One_free_row_consumes_only_eleven_draws_and_second_failed_attempt_does_not_draw()
    {
        NativeBattleQuezacotlFragmentWrite state = Fixture();
        for (int iteration = 0; iteration < 16; iteration++)
        {
            state = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(state.Memory, state.Effect);
        }
        NativeBattleEffectTaskPool pool = state.Effect.Pools[0].Advance((current, index) => new(current, index == 31 ? 2 : 0)).Pool;
        byte[] free = NativeBattleQuezacotlFragmentPool.ReadTask(state.Memory, 31).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(free, 0);
        NativeBattleEffectMemory memory = NativeBattleQuezacotlFragmentPool.WriteTask(state.Memory, 31, free.ToImmutableArray()).SeedSharedEffectRandom7059D0(0);
        memory = NativeBattleQuezacotlFragmentPool.WriteDword(memory, NativeBattleQuezacotlFragmentPool.Bank + 30 * 44 + 4, 0);
        NativeBattleQuezacotlFragmentWrite spawned = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(memory, state.Effect with { Pools = [pool] });
        Assert.Equal(32498u, spawned.Memory.SharedEffectRandom!.State);
        Assert.Equal(32, spawned.Effect.Pools[0].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(31, spawned.Effect.Pools[0].Tail);
    }

    [Fact]
    public void Producer_preserves_current_padding_that_it_does_not_write_after_the_real_bank_clear()
    {
        NativeBattleQuezacotlFragmentWrite state = Fixture();
        byte[] raw = NativeBattleQuezacotlFragmentPool.ReadTask(state.Memory, 0).ToArray();
        int[] retained = [14, 15, 22, 23, 30, 31, 38, 39, 43];
        foreach (int offset in retained)
        {
            raw[offset] = 0xA5;
        }
        NativeBattleEffectMemory memory = NativeBattleQuezacotlFragmentPool.WriteTask(state.Memory, 0, raw.ToImmutableArray());
        NativeBattleEffectTaskPool currentPool = state.Effect.Pools[0];
        currentPool = currentPool with { Slots = currentPool.Slots.SetItem(0, currentPool.Slots[0] with { Payload = raw.AsSpan(12).ToArray().ToImmutableArray() }) };
        NativeBattleQuezacotlFragmentWrite spawned = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(memory, state.Effect with { Pools = [currentPool] });
        Assert.All(retained, offset => Assert.Equal((byte)0xA5, NativeBattleQuezacotlFragmentPool.ReadTask(spawned.Memory, 0)[offset]));
    }

    [Fact]
    public void Motion_uses_signed_angular_BYTES_and_wrapping_WORD_positions_angles_and_age()
    {
        byte[] raw = new byte[44];
        Set(raw, 12, 32767);
        Set(raw, 16, 32760);
        Set(raw, 18, 32760);
        Set(raw, 20, -32760);
        Set(raw, 24, 20);
        Set(raw, 26, 20);
        Set(raw, 28, -20);
        Set(raw, 32, 32760);
        Set(raw, 34, -32760);
        Set(raw, 36, 32760);
        raw[40] = 128;
        raw[41] = 127;
        raw[42] = 255;
        NativeBattleQuezacotlFragmentMotion moved = NativeBattleQuezacotlFragmentPhysics.Advance6C6707(raw.ToImmutableArray(), false);
        Assert.Equal(new short[] { -32768, -32756, -32746, 32756, 30, 32632, -32633, 32759 }, Words(moved.Task44, [12, 16, 18, 20, 26, 32, 34, 36]));
        Assert.Equal(0, moved.Returned);
    }

    [Theory]
    [InlineData(-5, 10, 0, -20)]
    [InlineData(-20, 10, 0, 20)]
    [InlineData(0, -20, -10, -10)]
    public void Gravity_precedes_motion_and_only_strictly_positive_resulting_Y_bounces(short y, short velocityY, short expectedY, short expectedVelocity)
    {
        byte[] raw = new byte[44];
        Set(raw, 18, y);
        Set(raw, 26, velocityY);
        NativeBattleQuezacotlFragmentMotion moved = NativeBattleQuezacotlFragmentPhysics.Advance6C6707(raw.ToImmutableArray(), false);
        Assert.Equal(expectedY, Words(moved.Task44, [18])[0]);
        Assert.Equal(expectedVelocity, Words(moved.Task44, [26])[0]);
    }

    [Fact]
    public void Full_thirty_callback_lifetime_survives_midpoint_JSON_and_preserves_every_untouched_byte()
    {
        byte[] raw = Enumerable.Repeat((byte)0xA5, 44).ToArray();
        foreach (int offset in new[] { 12, 16, 18, 20, 24, 26, 28, 32, 34, 36 })
        {
            Set(raw, offset, 0);
        }
        raw[40] = raw[41] = raw[42] = 0;
        ImmutableArray<byte> task = raw.ToImmutableArray();
        int[] written = [12, 13, 16, 17, 18, 19, 20, 21, 26, 27, 32, 33, 34, 35, 36, 37];
        for (int age = 1; age <= 30; age++)
        {
            NativeBattleQuezacotlFragmentMotion moved = NativeBattleQuezacotlFragmentPhysics.Advance6C6707(task, false);
            Assert.Equal(age < 30 ? 0 : 2, moved.Returned);
            Assert.Equal((short)age, Words(moved.Task44, [12])[0]);
            foreach (int offset in Enumerable.Range(0, 44).Except(written))
            {
                Assert.Equal(raw[offset], moved.Task44[offset]);
            }
            task = age == 15 ? JsonSerializer.Deserialize<NativeBattleQuezacotlFragmentMotion>(JsonSerializer.Serialize(moved))!.Task44 : moved.Task44;
        }
    }

    [Fact]
    public void Paused_post_draw_motion_preserves_all_bytes_and_does_not_complete_age_thirty()
    {
        byte[] raw = Enumerable.Repeat((byte)0xA5, 44).ToArray();
        Set(raw, 12, 30);
        NativeBattleQuezacotlFragmentMotion paused = NativeBattleQuezacotlFragmentPhysics.Advance6C6707(raw.ToImmutableArray(), true);
        Assert.Equal(raw, paused.Task44.ToArray());
        Assert.Equal(0, paused.Returned);
    }

    [Fact]
    public void Missing_EXE_wrong_bank_or_task_extent_are_strict()
    {
        NativeBattleQuezacotlFragmentWrite state = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFragmentPool.ReadTask(state.Memory with { QuezacotlExecutableMemory = null }, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleQuezacotlFragmentPool.ReadTask(state.Memory, 32));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPhysics.Advance6C6707(new byte[43].ToImmutableArray(), false));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPool.Validate(state.Effect.Pools[0] with { BankAddress = 0x2108954 }));
    }

    [Fact]
    public void Divergent_JSON_pool_metadata_or_EXE_bytes_cannot_select_a_different_free_slot_or_callback()
    {
        NativeBattleQuezacotlFragmentWrite start = Fixture();
        NativeBattleQuezacotlFragmentWrite state = NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(start.Memory, start.Effect);
        NativeBattleQuezacotlFragmentWrite restored = JsonSerializer.Deserialize<NativeBattleQuezacotlFragmentWrite>(JsonSerializer.Serialize(state))!;
        NativeBattleEffectTaskPool pool = restored.Effect.Pools[0];
        NativeBattleQuezacotlFragmentPool.Validate(restored.Memory, pool);
        byte[] raw = NativeBattleQuezacotlFragmentPool.ReadTask(restored.Memory, 0).ToArray();
        raw[12] ^= 1;
        NativeBattleEffectMemory divergent = NativeBattleQuezacotlFragmentPool.WriteTask(restored.Memory, 0, raw.ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPool.SpawnTwo6C4732(divergent, restored.Effect));
        NativeBattleEffectTaskPool wrongCallback = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Callback = 0x6C7800 }) };
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPool.Validate(restored.Memory, wrongCallback));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPool.Validate(restored.Memory with { QuezacotlSecondaryHeads = [0, 0, 0, 0, 0] }, pool));
    }

    private static NativeBattleQuezacotlFragmentWrite Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { QuezacotlExecutableMemory = new(new byte[NativeBattleQuezacotlExecutableMemory.Length].ToImmutableArray()) };
        return NativeBattleQuezacotlFragmentPool.Initialize6C4725(memory, Effect());
    }

    private static NativeBattleOriginalEffectState Effect() => new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Quezacotl = new(1, 3690, 0) };

    private static short[] Words(ImmutableArray<byte> bytes, int[] offsets) => offsets.Select(offset => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset))).ToArray();

    private static void Set(byte[] raw, int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(offset), value);
    }
}
