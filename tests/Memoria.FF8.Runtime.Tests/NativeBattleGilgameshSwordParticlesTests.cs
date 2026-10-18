using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSwordParticlesTests
{
    [Fact]
    public void Initializer_appends_exact_model_and_particle_order_with_only_sprite_delays()
    {
        NativeBattleGilgameshSwordParticleInitialization initialized = NativeBattleGilgameshSwordParticles.Initialize5947F0(Pool(0x21FFD58, 0x21FFD68, 100), Pool(0x21FF458, 0x21FF3C8, 4), 3, Resources());
        Assert.Equal(new uint[] { 0x594FD0, 0x594CA0, 0x5948A0, 0x594CA0, 0x5948A0, 0x594CA0, 0x5948A0, 0x594CA0, 0x5948A0 }, initialized.Children.Slots.Take(9).Select(task => task.Callback));
        Assert.Equal((ushort)3, initialized.Children.Slots[0].ReadWord(14));
        Assert.Equal(0x595D50u, initialized.Particles.Slots[0].Callback);
        Assert.Equal((ushort)105, initialized.Particles.Slots[0].ReadWord(14));
        ushort[] order = [0, 3, 1, 2];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModulePoolTask sprite = initialized.Children.Slots[1 + index * 2];
            NativeBattleModulePoolTask mesh = initialized.Children.Slots[2 + index * 2];
            Assert.Equal((ushort)index, sprite.ReadWord(14));
            Assert.Equal((ushort)0xA5A5, mesh.ReadWord(14));
            Assert.Equal((ushort)(1 << index), sprite.ReadWord(28));
            Assert.Equal((ushort)(1 << index), mesh.ReadWord(28));
            Assert.Equal(order[index], sprite.ReadWord(30));
            Assert.Equal(order[index], mesh.ReadWord(30));
            Assert.Equal((ushort)0xA5A5, mesh.ReadWord(32));
            Assert.Equal((ushort)0xA5A5, sprite.ReadWord(32));
        }
        NativeBattleEffectTaskPool full = initialized.Particles;
        for (int index = 1; index < 4; index++)
        {
            (full, _) = full.Append(1);
        }
        Assert.Throws<InvalidOperationException>(() => NativeBattleGilgameshSwordParticles.Initialize5947F0(initialized.Children, full, 3, Resources()));
    }

    [Theory]
    [InlineData(0x5948A0u, 6, 60, 0x2F800, 32)]
    [InlineData(0x594CA0u, 4, 20, 0x2E000, 24)]
    public void Spawns_use_exact_random_count_and_shared_first_free_records_without_hydrating_suffix(uint callback, int count, int draws, int bank, int stride)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(bank, 200 * stride);
        for (int index = 0; index < 200; index++)
        {
            arena = PutDword(arena, bank + index * stride, 0);
        }
        NativeCrtRandomState expected = new(123);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        int bones = 0;
        NativeBattleGilgameshSwordParticleStep step = Run(Task(callback, 0), arena, new(123), 0, (index, selector, fraction) =>
        {
            Assert.Equal(2, index);
            Assert.Equal((byte)1, selector);
            Assert.Equal((short)4096, fraction);
            bones++;
            return new(10, 20000, 30);
        });
        Assert.Equal(1, bones);
        Assert.Equal(expected, step.Random);
        Assert.Equal(count, step.Survivors);
        Assert.Equal(callback == 0x5948A0 ? count : 0, step.MeshPackets.Length);
        Assert.Equal(callback == 0x594CA0 ? count : 0, step.SpritePackets.Length);
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(1u, Dword(step.Arena, bank + index * stride));
            Assert.Equal((short)1, Word(step.Arena, bank + index * stride + 4));
            Assert.Throws<NotSupportedException>(() => step.Arena.Read(bank + index * stride + 22, 2));
            if (callback == 0x594CA0)
            {
                Assert.Throws<NotSupportedException>(() => step.Arena.Read(bank + index * stride + 14, 2));
                Assert.Throws<NotSupportedException>(() => step.Arena.Read(bank + index * stride + 18, 2));
            }
        }
    }

    [Theory]
    [InlineData(0x5948A0u, 0x2F800, 32, 10)]
    [InlineData(0x594CA0u, 0x2E000, 24, 5)]
    public void Full_bank_and_single_hole_spend_no_failed_allocation_random(uint callback, int bank, int stride, int draws)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 200; index++)
        {
            arena = PutDword(arena, bank + index * stride, 2);
        }
        NativeBattleGilgameshSwordParticleStep full = Run(Task(callback, 0), arena, new(1));
        Assert.Equal(new NativeCrtRandomState(1), full.Random);
        Assert.Equal(2, full.Result);
        arena = PutDword(arena, bank + 199 * stride, 0);
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < draws; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleGilgameshSwordParticleStep one = Run(Task(callback, 0), arena, new(1));
        Assert.Equal(expected, one.Random);
        Assert.Equal(1, one.Survivors);
        Assert.Equal(2u, Dword(one.Arena, bank));
    }

    [Fact]
    public void Sprite_delay_has_no_bone_draw_spawn_or_counter_and_pause_retains_delay()
    {
        NativeBattleModulePoolTask task = Task(0x594CA0, 7).WriteWord(14, 2);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattleGilgameshSwordParticleStep paused = Run(task, arena, new(1), 0x201, (_, _, _) => throw new InvalidOperationException());
        Assert.Same(task, paused.UpdatedTask);
        Assert.Empty(paused.SpritePackets);
        NativeBattleGilgameshSwordParticleStep next = Run(task, arena, new(1), 0, (_, _, _) => throw new InvalidOperationException());
        Assert.Equal((ushort)1, next.UpdatedTask.ReadWord(14));
        Assert.Equal((ushort)7, next.UpdatedTask.ReadWord(12));
        Assert.Equal(new NativeCrtRandomState(1), next.Random);
        Assert.Same(arena, next.Arena);
    }

    [Fact]
    public void Mesh_size_fades_even_paused_but_current_packet_uses_old_size_and_no_physics()
    {
        NativeBattleEffectArena arena = Record(0x2F800, 1, 9, 300);
        arena = PutWord(arena, 0x2F80E, 400);
        NativeBattleModulePoolTask task = Task(0x5948A0, 2);
        NativeBattleGilgameshSwordParticleStep paused = Run(task, arena, new(1), 0x201);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal((short)350, Word(paused.Arena, 0x2F806));
        Assert.Equal((short)9, Word(paused.Arena, 0x2F804));
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), 300, 300, 300) with { X = 100, Y = -20, Z = 300 };
        NativeBattleActionMatrix baseView = NativeBattleGfMatrices.Compose56C2F0(View(), NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = 10, Y = 0, Z = 30 });
        NativeBattleGfGeometryBatch expected = NativeBattleModulePackets.EmitBatch572200(Resources().Mesh.Bytes, 0, default, NativeBattleGfMatrices.Compose56C2F0(baseView, local), Projection(), new(0, 0, null, null, null, null, 17, 2), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Assert.Single(paused.MeshPackets)));
        NativeBattleGilgameshSwordParticleStep dying = Run(task, PutWord(arena, 0x2F804, 16), new(1));
        Assert.Single(dying.MeshPackets);
        Assert.Equal(0u, Dword(dying.Arena, 0x2F800));
        Assert.Equal(2, dying.Result);
    }

    [Fact]
    public void Mesh_motion_uses_signed_damping_and_wrapped_positions_angles_while_sprite_moves_only_XZ()
    {
        NativeBattleEffectArena meshArena = Record(0x2F800, 1, 0, 300);
        meshArena = PutWord(PutWord(PutWord(meshArena, 0x2F810, -7), 0x2F812, -17), 0x2F814, -9);
        meshArena = PutWord(PutWord(meshArena, 0x2F818, 32760), 0x2F81A, 20);
        NativeBattleGilgameshSwordParticleStep mesh = Run(Task(0x5948A0, 2), meshArena, new(1));
        Assert.Equal((short)93, Word(mesh.Arena, 0x2F808));
        Assert.Equal((short)-37, Word(mesh.Arena, 0x2F80A));
        Assert.Equal((short)291, Word(mesh.Arena, 0x2F80C));
        Assert.Equal((short)-6, Word(mesh.Arena, 0x2F810));
        Assert.Equal((short)-14, Word(mesh.Arena, 0x2F812));
        Assert.Equal((short)-7, Word(mesh.Arena, 0x2F814));
        Assert.Equal(unchecked((short)32780), Word(mesh.Arena, 0x2F818));
        NativeBattleEffectArena spriteArena = Record(0x2E000, 1, 0, 1024);
        spriteArena = PutWord(PutWord(PutWord(spriteArena, 0x2E010, -7), 0x2E012, -17), 0x2E014, -9);
        NativeBattleGilgameshSwordParticleStep sprite = Run(Task(0x594CA0, 2), spriteArena, new(1));
        Assert.Equal((short)93, Word(sprite.Arena, 0x2E008));
        Assert.Equal((short)-20, Word(sprite.Arena, 0x2E00A));
        Assert.Equal((short)291, Word(sprite.Arena, 0x2E00C));
        Assert.Equal((short)-7, Word(sprite.Arena, 0x2E010));
        Assert.Equal((short)-17, Word(sprite.Arena, 0x2E012));
        NativeBattleGilgameshSwordParticleStep dead = Run(Task(0x594CA0, 2), Record(0x2E000, 1, 14, 1024), new(1));
        Assert.Single(dead.SpritePackets);
        Assert.Equal(0u, Dword(dead.Arena, 0x2E000));
        Assert.Equal(2, dead.Result);
    }

    [Theory]
    [InlineData(0x5948A0u, 0x2F800)]
    [InlineData(0x594CA0u, 0x2E000)]
    public void Signed_high_mask_and_task_counter_wrap_keep_independent_JSON_next(uint callback, int bank)
    {
        NativeBattleModulePoolTask task = Task(callback, 32767).WriteWord(28, 0x8000);
        NativeBattleEffectArena arena = Record(bank, 0x80000000, 0, 300);
        NativeBattleGilgameshSwordParticleStep first = Run(task, arena, new(1));
        Assert.Equal(1, first.Survivors);
        Assert.Equal((ushort)32768, first.UpdatedTask.ReadWord(12));
        NativeBattleGilgameshSwordParticleStep expected = Run(first.UpdatedTask, first.Arena, first.Random, 0x201);
        NativeBattleGilgameshSwordParticleStep actual = Run(RoundTrip(first.UpdatedTask), RoundTrip(first.Arena), RoundTrip(first.Random), 0x201);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private static NativeBattleGilgameshSwordParticleStep Run(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, uint flags = 0, Func<int, byte, short, NativeBattleVector>? bone = null) => NativeBattleGilgameshSwordParticles.Step(task, arena, random, Resources(), View(), Projection(), flags, bone ?? ((_, _, _) => new(10, 20000, 30)));
    private static NativeBattleModulePoolTask Task(uint callback, int counter) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(12, unchecked((ushort)counter)).WriteWord(28, 1).WriteWord(30, 2);
    private static NativeBattleEffectTaskPool Pool(uint descriptor, uint bank, int count)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, bank, 36, checked((ushort)count));
        return pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
    }
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
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
    private static NativeBattleEffectArena Record(int bank, uint mask, int age, int size) => PutWord(PutWord(PutWord(PutWord(PutWord(PutDword(NativeBattleEffectArena.Clear(), bank, mask), bank + 4, age), bank + 6, size), bank + 8, 100), bank + 10, -20), bank + 12, 300);
    private static NativeBattleGilgameshSwordParticleResources Resources()
    {
        byte[] mesh = new byte[0xF8];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 56);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 6);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(72), 8);
        byte[] sprite = new byte[0x194];
        sprite[3] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 15);
        for (int phase = 0; phase < 15; phase++)
        {
            int offset = 44 + phase * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + phase * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(offset), 1);
            sprite[offset + 4] = 16;
            sprite[offset + 5] = 128;
            sprite[offset + 6] = 16;
            sprite[offset + 7] = 0x2E;
        }
        BinaryPrimitives.WriteInt16LittleEndian(sprite.AsSpan(40), -1);
        return new(new(0, 0, mesh.ToImmutableArray()), new(0, 0, 0, 0, sprite.ToImmutableArray()), [0, 3, 1, 2]);
    }
}
