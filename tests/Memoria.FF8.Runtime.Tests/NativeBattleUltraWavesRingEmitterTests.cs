using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWavesRingEmitterTests
{
    [Theory]
    [InlineData(154)]
    [InlineData(184)]
    public void Birth_uses_native_768_and_overwrites_angle_without_reading_unknown_anchor_W(ushort effect)
    {
        NativeBattleUltraWavesEmitterStep input = Fixture() with { Task = Task(0).MarkPayloadUnknown(22, 2) };
        NativeBattleUltraWavesEmitterStep step = Advance(input, effect);
        Assert.Equal((short)1, Word(step.Particles, 0));
        Assert.Equal((short)768, Word(step.Particles, 6));
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0 }, step.Particles.Read(8, 6));
        Assert.Equal((short)512, Word(step.Particles, 14));
        Assert.Equal((short)-150, Word(step.Particles, 20));
        Assert.Equal((ushort)1, step.Task.ReadWord(12));
        Assert.Equal((ushort)768, step.Task.ReadWord(32));
        Assert.Equal(0, step.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Theory]
    [InlineData(154, 896)]
    [InlineData(184, 877)]
    public void Existing_particle_uses_selected_signed_growth_and_releases_actual_temporary_scope(ushort effect, short expected)
    {
        NativeBattleUltraWavesEmitterStep input = Fixture();
        input = input with { Particles = Particle(input.Particles, 0, 0, 768), Task = Task(21) };
        int delivered = 0;
        NativeBattleUltraWavesEmitterStep step = Advance(input, effect, false, (current, batch) =>
        {
            delivered++;
            Assert.Empty(batch.Attempts);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 244, BitConverter.ToUInt32(current.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            NativeBattleEffectArena matrix = NativeBattleTemporaryMemory.ReadView(current.Staging, NativeBattleTemporaryMemory.Address + 108, 136);
            Assert.Throws<NotSupportedException>(() => matrix.Read(0x1A, 2));
            Assert.Equal(768, BitConverter.ToInt32(matrix.Read(0x48, 4)));
            return current;
        });
        Assert.Equal(1, delivered);
        Assert.Equal(expected, Word(step.Particles, 6));
        Assert.Equal((short)1, Word(step.Particles, 4));
        Assert.Equal((short)522, Word(step.Particles, 14));
        Assert.Equal((short)110, Word(step.Particles, 22));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Native_row_birth_generation_survives_transient_particle_arena_replacement_and_pause()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2404BD0, 0x2404BE0, 36, 100);
        (pool, int slot) = pool.Append(0x608530);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteBytes(12, Task(0).ReadBytes(12, 24));
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(154, task);
        NativeBattleUltraWavesEmitterStep input = Fixture() with { Task = task };
        NativeBattlePilotVisualIdentity before = NativeBattleUltraWavesVisualLifetimes.Identity(owner, task, 0);
        NativeBattleUltraWavesEmitterStep born = Advance(input, 154, pilotIdentity: owner);
        NativeBattlePilotVisualIdentity first = NativeBattleUltraWavesVisualLifetimes.Identity(owner, born.Task, 0);
        Assert.NotEqual(before.ResourceGeneration, first.ResourceGeneration);
        NativeBattleUltraWavesEmitterStep copy = born with { Particles = NativeBattleEffectArena.Clear().Write(0, born.Particles.Read(0, 2400)) };
        NativeBattleUltraWavesEmitterStep paused = Advance(copy, 154, true, pilotIdentity: owner);
        Assert.Equal(first, NativeBattleUltraWavesVisualLifetimes.Identity(owner, paused.Task, 0));
        Assert.Equal(NativeBattleTestSnapshots.Json(copy.Particles), NativeBattleTestSnapshots.Json(paused.Particles));
        Assert.Equal(copy.Task.ReadWord(12), paused.Task.ReadWord(12));
        NativeBattleUltraWavesVisualLifetimes.Allocated(paused.Task, 0);
        Assert.NotEqual(first, NativeBattleUltraWavesVisualLifetimes.Identity(owner, paused.Task, 0));
    }

    [Fact]
    public void Fade_flag_is_sticky_across_particles_and_pause_retains_task_and_particle_bytes()
    {
        NativeBattleUltraWavesEmitterStep input = Fixture();
        input = input with { Particles = Particle(Particle(input.Particles, 0, 7, 768), 24, 0, 768) };
        List<uint> flags = [];
        NativeBattleUltraWavesEmitterStep step = Advance(input, 184, true, (current, _) =>
        {
            NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(current.Staging, NativeBattleTemporaryMemory.Address, 108);
            flags.Add(BitConverter.ToUInt32(context.Read(0x1C, 4)));
            Assert.Equal(682, BitConverter.ToInt32(context.Read(12, 4)));
            return current;
        });
        Assert.Equal(new uint[] { 0xF3, 0xF3 }, flags);
        Assert.Equal(input.Particles.Read(0, 2400), step.Particles.Read(0, 2400));
        Assert.Equal(input.Task, step.Task);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Terminal_uses_actual_survivor_count_and_JSON_next_retains_released_temporary_bytes()
    {
        NativeBattleUltraWavesEmitterStep input = Fixture() with { Task = Task(21) };
        input = input with { Particles = Particle(input.Particles, 0, 11, 768) };
        NativeBattleUltraWavesEmitterStep step = Advance(input, 184);
        Assert.Equal(2, step.Result);
        Assert.Equal((short)0, Word(step.Particles, 0));
        Assert.Equal((short)12, Word(step.Particles, 4));
        NativeBattleUltraWavesEmitterStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleUltraWavesEmitterStep>(NativeBattleTestSnapshots.Json(step))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(Advance(step, 184)), NativeBattleTestSnapshots.Json(Advance(restored, 184)));
    }

    private static NativeBattleUltraWavesEmitterStep Advance(NativeBattleUltraWavesEmitterStep step, ushort effect, bool paused = false, Func<NativeBattleUltraWavesEmitterStep, NativeBattleGfGeometryBatch, NativeBattleUltraWavesEmitterStep>? deliver = null, NativeBattlePilotVisualIdentity? pilotIdentity = null) => NativeBattleUltraWavesRingEmitter.Advance(step, effect, Mesh(), Enumerable.Repeat(0x123456u, 16).ToImmutableArray(), NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, paused, deliver ?? ((current, _) => current), pilotIdentity: pilotIdentity);
    private static NativeBattleUltraWavesEmitterStep Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(0, 0, 0, 0, 0) } };
        return new(memory, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), Task(0), NativeBattleEffectArena.Clear(), 0);
    }
    private static NativeBattleModulePoolTask Task(ushort age) => new NativeBattleModulePoolTask(1, 0, null, 0x5C8930, new byte[68].ToImmutableArray()).WriteWord(12, age).WriteWord(14, 1).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30).WriteWord(28, unchecked((ushort)-4096)).WriteWord(32, 512).WriteWord(34, 3);
    private static NativeBattleEffectArena Particle(NativeBattleEffectArena arena, int at, short age, short scale)
    {
        foreach ((int offset, short value) in new (int, short)[] { (0, 1), (2, 0), (4, age), (6, scale), (8, 0), (10, 0), (12, 2000), (14, 10), (16, 1), (18, 2), (20, 3), (22, 100) })
        {
            arena = arena.Write(at + offset, BitConverter.GetBytes(value));
        }
        return arena;
    }
    private static short Word(NativeBattleEffectArena arena, int at) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(at, 2));
    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] source = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(source, 8);
        return new(0, 0xD34450, source.ToImmutableArray());
    }
}
