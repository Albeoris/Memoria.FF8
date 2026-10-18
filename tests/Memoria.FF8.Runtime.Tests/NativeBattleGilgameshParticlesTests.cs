using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshParticlesTests
{
    [Theory]
    [InlineData(0x5915C0u, 2, 8)]
    [InlineData(0x591850u, 6, 36)]
    [InlineData(0x591B80u, 4, 24)]
    public void Allocated_records_consume_exact_CRT_count_and_spawn_then_draw_in_physical_order(uint callback, int count, int draws)
    {
        NativeCrtRandomState random = new(123);
        NativeCrtRandomState expected = random;
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleGilgameshParticleStep step = Run(Task(callback, 0), NativeBattleEffectArena.Clear(), random);
        Assert.Equal(expected, step.Random);
        Assert.Equal(count, step.Survivors);
        Assert.Equal(count, step.Packets.Length);
        Assert.Equal((ushort)1, step.UpdatedTask.ReadWord(12));
        int bank = callback == 0x591B80 ? 0x2F800 : 0x2E000;
        int stride = callback == 0x591B80 ? 32 : 24;
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(1u, Dword(step.Arena, bank + index * stride));
            Assert.Equal((short)1, Word(step.Arena, bank + index * stride + 4));
        }
        Assert.Equal(0u, Dword(step.Arena, bank + count * stride));
    }

    [Fact]
    public void Allocation_uses_first_physical_holes_retains_unknown_suffix_and_does_not_randomize_failed_records()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x2E000, 200 * 24);
        for (int index = 0; index < 200; index++)
        {
            arena = PutDword(arena, 0x2E000 + index * 24, index is 13 or 37 ? 0u : 2u);
        }
        NativeBattleGilgameshParticleStep step = Run(Task(0x5915C0, 0), arena, new(1));
        Assert.Equal(2, step.Survivors);
        Assert.Equal(1u, Dword(step.Arena, 0x2E000 + 13 * 24));
        Assert.Equal(1u, Dword(step.Arena, 0x2E000 + 37 * 24));
        Assert.Throws<NotSupportedException>(() => step.Arena.Read(0x2E000 + 13 * 24 + 14, 10));
        NativeBattleGilgameshParticleStep full = Run(Task(0x5915C0, 0), step.Arena, new(99));
        Assert.Equal(new NativeCrtRandomState(99), full.Random);
        Assert.Equal(2, full.Survivors);
    }

    [Theory]
    [InlineData(0x5915C0u)]
    [InlineData(0x591850u)]
    [InlineData(0x591B80u)]
    public void Paused_callbacks_draw_existing_records_without_spawn_or_CRT_or_counter_physics(uint callback)
    {
        int bank = callback == 0x591B80 ? 0x2F800 : 0x2E000;
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), bank, 1, 0);
        NativeBattleModulePoolTask task = Task(callback, 0);
        NativeBattleGilgameshParticleStep step = Run(task, arena, new(9), 0x201);
        Assert.Same(task, step.UpdatedTask);
        Assert.Equal(new NativeCrtRandomState(9), step.Random);
        Assert.Single(step.Packets);
        Assert.Equal(JsonSerializer.Serialize(arena), JsonSerializer.Serialize(step.Arena));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Sign_extended_task_mask_matches_high_DWORD_bits_and_clipped_records_still_survive()
    {
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), 0x2E000, 0x80000000, 0);
        NativeBattleModulePoolTask task = Task(0x5915C0, 3).WriteWord(22, 0x8000);
        NativeBattleGilgameshParticleStep step = NativeBattleGilgameshParticles.Step(task, arena, new(1), Resources(), View() with { Z = -3000 }, Projection(), 0);
        Assert.Single(step.Packets);
        Assert.Empty(step.Packets[0].Primitives);
        Assert.Equal(1, step.Survivors);
        Assert.Equal(0, step.Result);
        Assert.Equal((short)1, Word(step.Arena, 0x2E004));
        Assert.Equal(2, Run(task.WriteWord(22, 1), arena, new(1)).Result);
    }

    [Theory]
    [InlineData(0x5915C0u)]
    [InlineData(0x591850u)]
    public void Producer_terminal_frame_kills_record_without_fixed_task_lifetime(uint callback)
    {
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), 0x2E000, 1, 15);
        NativeBattleGilgameshParticleStep step = Run(Task(callback, 100), arena, new(1));
        Assert.Single(step.Packets);
        Assert.Equal(0u, Dword(step.Arena, 0x2E000));
        Assert.Equal((short)15, Word(step.Arena, 0x2E004));
        Assert.Equal(0, step.Survivors);
        Assert.Equal(2, step.Result);
    }

    [Theory]
    [InlineData(0x591850u)]
    [InlineData(0x591B80u)]
    public void Motion_adds_old_velocity_before_SAR_damping_and_WORD_wrap(uint callback)
    {
        int bank = callback == 0x591B80 ? 0x2F800 : 0x2E000;
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), bank, 1, 0);
        arena = PutWord(arena, bank + 8, 32767);
        arena = PutWord(arena, bank + 10, 0);
        arena = PutWord(arena, bank + 0x10, 1);
        arena = PutWord(arena, bank + 0x12, -1);
        arena = PutWord(arena, bank + 0x14, -9);
        NativeBattleGilgameshParticleStep step = Run(Task(callback, 3), arena, new(1));
        Assert.Equal(short.MinValue, Word(step.Arena, bank + 8));
        Assert.Equal((short)-1, Word(step.Arena, bank + 10));
        Assert.Equal((short)1, Word(step.Arena, bank + 0x10));
        Assert.Equal((short)0, Word(step.Arena, bank + 0x12));
        Assert.Equal((short)-7, Word(step.Arena, bank + 0x14));
    }

    [Fact]
    public void Streak_uses_old_size_for_current_draw_but_updates_size_even_paused_and_kills_at_old_phase12()
    {
        const int bank = 0x2F800;
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), bank, 1, 6);
        arena = PutWord(arena, bank + 0x0E, 8192);
        NativeBattleModulePoolTask task = Task(0x591B80, 3);
        NativeBattleGilgameshParticleStep step = Run(task, arena, new(1), 0x201);
        NativeBattleGilgameshParticleStep before = Run(task, PutWord(arena, bank + 4, 5), new(1), 0x201);
        Assert.Equal(JsonSerializer.Serialize(before.Packets), JsonSerializer.Serialize(step.Packets));
        Assert.Equal((short)8192, Word(step.Arena, bank + 6));
        Assert.Same(task, step.UpdatedTask);
        NativeBattleGilgameshParticleStep alive = Run(task, PutWord(arena, bank + 4, 11), new(1));
        Assert.Equal(1, alive.Survivors);
        Assert.Equal((short)12, Word(alive.Arena, bank + 4));
        NativeBattleGilgameshParticleStep dead = Run(task, PutWord(arena, bank + 4, 12), new(1));
        Assert.Equal(0u, Dword(dead.Arena, bank));
        Assert.Equal(2, dead.Result);
    }

    [Fact]
    public void Extended_sprite_E1_uses_original_zero_proxy_depth_and_insertion_order()
    {
        NativeBattleGilgameshParticleResources resources = Resources();
        byte[] source = resources.SourceA.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(44), 0x80000001);
        resources = resources with { SourceA = resources.SourceA with { Bytes = source.ToImmutableArray() } };
        NativeBattleEffectArena arena = Record(NativeBattleEffectArena.Clear(), 0x2E000, 1, 0);
        NativeBattleGilgameshParticleStep step = NativeBattleGilgameshParticles.Step(Task(0x5915C0, 3), arena, new(1), resources, View(), Projection(), 0x201);
        NativeBattleModulePacketBatch batch = Assert.Single(step.Packets);
        Assert.Equal(52, batch.AllocationBytes);
        Assert.Equal(new[] { 40, 0 }, batch.Primitives.Select(item => item.ByteOffset).ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0 }, batch.Primitives[0].Primitive.VertexPayload4.ToArray());
    }

    [Theory]
    [InlineData(0x5915C0u)]
    [InlineData(0x591850u)]
    [InlineData(0x591B80u)]
    public void Task_raw_banks_and_random_json_roundtrip_produce_the_same_next_callback(uint callback)
    {
        NativeBattleGilgameshParticleStep first = Run(Task(callback, 0), NativeBattleEffectArena.Clear(), new(101));
        NativeBattleGilgameshParticleStep restored = JsonSerializer.Deserialize<NativeBattleGilgameshParticleStep>(JsonSerializer.Serialize(first))!;
        Assert.Equal(JsonSerializer.Serialize(Run(first.UpdatedTask, first.Arena, first.Random)), JsonSerializer.Serialize(Run(restored.UpdatedTask, restored.Arena, restored.Random)));
    }

    [Theory]
    [InlineData(0x5915C0u)]
    [InlineData(0x591850u)]
    [InlineData(0x591B80u)]
    public void First_allocated_record_uses_exact_random_operands_and_normalizes_DWORD_direction_before_WORD_stores(uint callback)
    {
        NativeCrtRandomState random = new(0xFEDCBA98);
        int[] values = new int[6];
        NativeCrtRandomState sequence = random;
        for (int index = 0; index < values.Length; index++)
        {
            sequence = sequence.Draw(out ushort value);
            values[index] = value;
        }
        NativeBattleGilgameshParticleStep step = Run(Task(callback, 0), NativeBattleEffectArena.Clear(), random);
        int bank = callback == 0x591B80 ? 0x2F800 : 0x2E000;
        int size = values[0] % (callback == 0x5915C0 ? 8192 : callback == 0x591850 ? 1536 : 4096) + (callback == 0x591850 ? 768 : 1536);
        Assert.Equal(unchecked((short)size), Word(step.Arena, bank + 6));
        if (callback == 0x5915C0)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.Equal((short)(values[axis + 1] % 1200 - 600), Word(step.Arena, bank + 8 + axis * 2));
            }
            return;
        }
        int z = callback == 0x591850 ? -2048 - values[3] % 2048 : values[3] % 8192 - 4096;
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(values[1] % 8192 - 4096, values[2] % 8192 - 4096, z);
        int[] components = [direction.X, direction.Y, direction.Z];
        int distance = values[4] % (callback == 0x591850 ? 500 : 400) + (callback == 0x591850 ? 200 : 400);
        int speed = values[5] % (callback == 0x591850 ? 300 : 380) + (callback == 0x591850 ? 100 : 140);
        for (int axis = 0; axis < 3; axis++)
        {
            int point = unchecked((short)(components[axis] * distance >> 12));
            int velocity = unchecked((short)(components[axis] * speed >> 12));
            Assert.Equal(unchecked((short)(point + velocity)), Word(step.Arena, bank + 8 + axis * 2));
            Assert.Equal(unchecked((short)(velocity - (velocity >> 3))), Word(step.Arena, bank + 0x10 + axis * 2));
            if (callback == 0x591B80)
            {
                Assert.Equal(unchecked((short)components[axis]), Word(step.Arena, bank + 0x18 + axis * 2));
            }
        }
        if (callback == 0x591B80)
        {
            Assert.Equal((short)size, Word(step.Arena, bank + 14));
        }
    }
    private static NativeBattleGilgameshParticleStep Run(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, uint flags = 0) => NativeBattleGilgameshParticles.Step(task, arena, random, Resources(), View(), Projection(), flags);
    private static NativeBattleModulePoolTask Task(uint callback, ushort counter) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(12, counter).WriteWord(22, 1);
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, int offset, uint mask, short phase)
    {
        arena = PutDword(arena, offset, mask);
        arena = PutWord(arena, offset + 4, phase);
        arena = PutWord(arena, offset + 6, 4096);
        arena = PutWord(arena, offset + 0x0E, 4096);
        if (offset >= 0x2F800)
        {
            arena = PutWord(arena, offset + 0x1A, -4096);
        }
        return arena;
    }

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleGilgameshParticleResources Resources() => new(Sprite(9, 16, 0x224), Sprite(9, 16, 0x210), Sprite(6, 16, 0x1AC), Sprite(6, 1, 0x28));

    private static NativeBattleModuleResource Sprite(byte kind, int frames, int extent)
    {
        byte[] bytes = new byte[extent];
        bytes[3] = kind;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
        int first = frames == 1 ? 16 : 44;
        for (int phase = 0; phase < frames; phase++)
        {
            int offset = first + phase * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 16;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 16;
            bytes[offset + 7] = 0x2E;
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + frames * 2), -1);
        return new(0, 0, 0, 0, bytes.ToImmutableArray());
    }
}
