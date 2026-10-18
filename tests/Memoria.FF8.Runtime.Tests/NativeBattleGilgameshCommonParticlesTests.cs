using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshCommonParticlesTests
{
    [Theory]
    [InlineData(0x593770u, 8, 40, 0x2E000, 24, 1u)]
    [InlineData(0x5939C0u, 12, 72, 0x2F800, 32, 16u)]
    public void Spawn_uses_exact_CRT_count_physical_bank_and_retains_unwritten_suffix(uint callback, int count, int draws, int bank, int stride, uint mask)
    {
        NativeCrtRandomState expected = new(123);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(bank, 200 * stride);
        for (int index = 0; index < 200; index++)
        {
            arena = Put(arena, bank + index * stride, 0u);
        }
        NativeBattleGilgameshParticleStep step = Run(Task(callback, 0), arena, new(123));
        Assert.Equal(expected, step.Random);
        Assert.Equal(count, step.Survivors);
        Assert.Equal(count, step.Packets.Length);
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(mask, Dword(step.Arena, bank + index * stride));
            Assert.Equal((short)1, Word(step.Arena, bank + index * stride + 4));
            Assert.Throws<NotSupportedException>(() => step.Arena.Read(bank + index * stride + 14, 2));
            if (stride == 32)
            {
                Assert.Throws<NotSupportedException>(() => step.Arena.Read(bank + index * stride + 0x12, 2));
            }
        }
        Assert.Equal(0u, Dword(step.Arena, bank + count * stride));
    }

    [Theory]
    [InlineData(0x593770u, 0x2E000, 24, 1u, 5)]
    [InlineData(0x5939C0u, 0x2F800, 32, 16u, 6)]
    public void First_free_scan_preserves_other_effect_flags_and_does_not_consume_failed_spawn_random(uint callback, int bank, int stride, uint mask, int draws)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 200; index++)
        {
            arena = Put(arena, bank + index * stride, index == 37 ? 0u : 2u);
        }
        NativeCrtRandomState expected = new(9);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleGilgameshParticleStep step = Run(Task(callback, 0), arena, new(9));
        Assert.Equal(expected, step.Random);
        Assert.Equal(mask, Dword(step.Arena, bank + 37 * stride));
        Assert.Equal(2u, Dword(step.Arena, bank));
        NativeBattleGilgameshParticleStep full = Run(Task(callback, 0), step.Arena, new(99));
        Assert.Equal(new NativeCrtRandomState(99), full.Random);
    }

    [Theory]
    [InlineData(0x593770u, 0x2E000, 1u)]
    [InlineData(0x5939C0u, 0x2F800, 16u)]
    public void Paused_draw_uses_current_world_view_directly_without_task_translation_or_roll(uint callback, int bank, uint mask)
    {
        NativeBattleEffectArena arena = Record(bank, mask, 0);
        NativeBattleModulePoolTask task = Task(callback, 0).WriteWord(16, 2300).WriteWord(18, 4500).WriteWord(20, 6700);
        NativeBattleGilgameshParticleStep step = Run(task, arena, new(5), 0x201);
        Assert.Same(task, step.UpdatedTask);
        Assert.Equal(JsonSerializer.Serialize(arena), JsonSerializer.Serialize(step.Arena));
        Assert.Equal(new NativeCrtRandomState(5), step.Random);
        NativeBattleModuleResource resource = Resources().Sprite;
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), Anchor = new(100, -20, 300, 0), Scale = 4096, DepthOffset = 0, SpriteFlags = 0 };
        NativeBattleModulePacketBatch expected = NativeBattleModulePackets.EmitSprite571DC0(draw, Projection() with { View = View() }, 0, 17, 2);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Assert.Single(step.Packets)));
    }

    [Fact]
    public void Moving_particles_update_only_XZ_with_WORD_wrap_and_arithmetic_shift_damping()
    {
        const int bank = 0x2F800;
        NativeBattleEffectArena arena = Record(bank, 0x10, 0);
        arena = Put(arena, bank + 8, (ushort)32760);
        arena = Put(arena, bank + 0x10, (ushort)20);
        arena = Put(arena, bank + 0x12, (ushort)1234);
        arena = Put(arena, bank + 0x14, unchecked((ushort)-9));
        NativeBattleGilgameshParticleStep step = Run(Task(0x5939C0, 9), arena, new(1));
        Assert.Equal(unchecked((short)32780), Word(step.Arena, bank + 8));
        Assert.Equal((short)291, Word(step.Arena, bank + 12));
        Assert.Equal((short)18, Word(step.Arena, bank + 0x10));
        Assert.Equal((short)-7, Word(step.Arena, bank + 0x14));
        Assert.Equal((short)-20, Word(step.Arena, bank + 10));
        Assert.Equal((short)1234, Word(step.Arena, bank + 0x12));
        Assert.Equal(1, step.Survivors);
        Assert.Equal(0, step.Result);
        NativeBattleGilgameshParticleStep expected = Run(step.UpdatedTask, step.Arena, step.Random);
        NativeBattleGilgameshParticleStep actual = Run(RoundTrip(step.UpdatedTask), RoundTrip(step.Arena), RoundTrip(step.Random));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Stationary_age_cleanup_clears_whole_matching_DWORD_and_is_not_survivor_based()
    {
        NativeBattleEffectArena arena = Record(0x2E000, 3, 0);
        arena = Put(arena, 0x2E018, 2u);
        NativeBattleGilgameshParticleStep terminal = Run(Task(0x593770, 67), arena, new(1));
        Assert.Single(terminal.Packets);
        Assert.Equal(0u, Dword(terminal.Arena, 0x2E000));
        Assert.Equal(2u, Dword(terminal.Arena, 0x2E018));
        Assert.Equal(2, terminal.Result);
        Assert.Equal(0, Run(Task(0x593770, 63), NativeBattleEffectArena.Clear(), new(1)).Result);
        NativeBattleGilgameshParticleStep dead = Run(Task(0x5939C0, 9), Record(0x2F800, 0x12, 15), new(1));
        Assert.Single(dead.Packets);
        Assert.Equal(0u, Dword(dead.Arena, 0x2F800));
        Assert.Equal(0, dead.Survivors);
        Assert.Equal(2, dead.Result);
        NativeBattleGilgameshParticleStep wrap = Run(Task(0x593770, 32767), NativeBattleEffectArena.Clear(), new(1));
        Assert.Equal((ushort)32768, wrap.UpdatedTask.ReadWord(12));
        Assert.Equal(0, wrap.Result);
    }

    [Fact]
    public void Nested_parent_appends_at_native_remainder_boundaries_preserving_child_suffix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        pool = pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
        NativeBattleModulePoolTask parent = Task(0x593C70, 0);
        NativeCrtRandomState random = new(1);
        NativeCrtRandomState expected = random;
        for (int frame = 0; frame < 30; frame++)
        {
            NativeBattleGilgameshNestedAppend step = NativeBattleGilgameshCommonParticles.Step593C70(parent, pool, random, 0);
            parent = step.UpdatedTask;
            pool = step.Pool;
            random = step.Random;
            Assert.Equal(frame == 29 ? 2 : 0, step.Result);
        }
        for (int index = 0; index < 3; index++)
        {
            expected = expected.Draw(out ushort angle);
            NativeBattleModulePoolTask child = pool.Slots[index];
            Assert.Equal(0x593D20u, child.Callback);
            Assert.Equal((ushort)(angle % 4096), child.ReadWord(24));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(14));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(30));
            Assert.Equal((ushort)186, child.ReadWord(32));
            Assert.Equal((ushort)186, child.ReadWord(34));
        }
        Assert.Equal(expected, random);
        NativeBattleGilgameshNestedAppend paused = NativeBattleGilgameshCommonParticles.Step593C70(Task(0x593C70, 1), pool, random, 1);
        Assert.Same(pool, paused.Pool);
        Assert.Equal(random, paused.Random);
        Assert.Equal((ushort)1, paused.UpdatedTask.ReadWord(12));
    }

    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(29)]
    public void Nested_mesh_draws_before_pause_then_integrates_signed_velocity_and_uses_current_fade(int counter)
    {
        NativeBattleModulePoolTask task = Task(0x593D20, counter).WriteWord(24, 2).WriteWord(26, 65532).WriteWord(28, 4096).WriteWord(32, 32760).WriteWord(34, unchecked((ushort)-13));
        NativeBattleGilgameshMeshAuxiliaryStep paused = NativeBattleGilgameshCommonParticles.Step593D20(task, Resources(), View(), Projection(), 0x200);
        Assert.Same(task, paused.UpdatedTask);
        NativeBattleGilgameshMeshAuxiliaryStep step = NativeBattleGilgameshCommonParticles.Step593D20(task, Resources(), View(), Projection(), 0);
        Assert.Equal(JsonSerializer.Serialize(paused.Batch), JsonSerializer.Serialize(step.Batch));
        Assert.Equal((ushort)65534, step.UpdatedTask.ReadWord(24));
        Assert.Equal((ushort)32747, step.UpdatedTask.ReadWord(32));
        Assert.Equal(unchecked((ushort)-14), step.UpdatedTask.ReadWord(34));
        Assert.Equal((ushort)4096, step.UpdatedTask.ReadWord(28));
        Assert.Equal(counter == 29 ? 2 : 0, step.Result);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 2, 0)), 4096, 32760, 4096);
        NativeBattleModuleMeshContext context = new(counter >= 18 ? 0xF3u : 0x33u, 0, counter >= 18 ? 341 * (counter - 18) : null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(Resources().NestedMesh.Bytes, 0, default, NativeBattleGfMatrices.Compose56C2F0(View(), local), Projection(), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(step.Batch));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshCommonParticles.Step593D20(step.UpdatedTask, Resources(), View(), Projection(), 0)), JsonSerializer.Serialize(NativeBattleGilgameshCommonParticles.Step593D20(RoundTrip(step.UpdatedTask), Resources(), View(), Projection(), 0)));
    }

    private static NativeBattleGilgameshParticleStep Run(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, uint flags = 0) => NativeBattleGilgameshCommonParticles.Step(task, arena, random, Resources(), View(), Projection(), flags);
    private static NativeBattleModulePoolTask Task(uint callback, int counter) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(12, unchecked((ushort)counter));
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Record(int bank, uint flags, ushort phase) => Put(Put(Put(Put(Put(Put(NativeBattleEffectArena.Clear(), bank, flags), bank + 4, phase), bank + 6, (ushort)4096), bank + 8, (ushort)100), bank + 10, unchecked((ushort)-20)), bank + 12, (ushort)300);
    private static NativeBattleGilgameshCommonParticleResources Resources()
    {
        byte[] sprite = new byte[0x1AC];
        sprite[3] = 7;
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
        for (int phase = 0; phase < 16; phase++)
        {
            int offset = 44 + phase * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + phase * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(offset), 1);
            sprite[offset + 4] = 16;
            sprite[offset + 5] = 128;
            sprite[offset + 6] = 16;
            sprite[offset + 7] = 0x2E;
        }
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(42), -1);
        byte[] mesh = new byte[0x12E8];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 8 + 168 * 8);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 168);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(8 + 168 * 8 + 12), 144);
        return new(new(0, 0, 0, 0, sprite.ToImmutableArray()), new(0, 0, mesh.ToImmutableArray()));
    }
}
