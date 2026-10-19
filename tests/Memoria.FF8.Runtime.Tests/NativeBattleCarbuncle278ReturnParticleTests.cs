using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278ReturnParticleTests
{
    [Fact]
    public void Spawn_uses_four_ordered_shared_draws_signed_velocities_and_original_phase_window()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508168, 0x20DFAB8 + 0x22C4, 0x1C, 8);
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), new(0, 0, 3, 0, 0, 0), null) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() with { Base81CC = -200, Pause8114 = 1 } };
        NativeBattleCarbuncle278Frame frame = new(memory, effect, NativeCrtRandomState.FromColdProcess());
        NativeBattleCarbuncle278Frame spawned = NativeBattleCarbuncle278ReturnParticles.Spawn681C31(frame, 231);
        NativeBattleSharedEffectRandomState random = memory.SharedEffectRandom!.Draw7059E0(out ushort x);
        random = random.Draw7059E0(out ushort z);
        random = random.Draw7059E0(out ushort speed);
        random = random.Draw7059E0(out ushort kind);
        NativeBattleModulePoolTask task = spawned.Effect.Pools[0].Slots[0];
        Assert.Equal(unchecked((ushort)((x & 127) - 64)), task.ReadWord(0xC));
        Assert.Equal(unchecked((ushort)(((x & 127) - 64) >> 2)), task.ReadWord(0x14));
        Assert.Equal(unchecked((ushort)((z & 127) - 64 - 200)), task.ReadWord(0x10));
        Assert.Equal(unchecked((ushort)(((z & 127) - 64) >> 2)), task.ReadWord(0x18));
        Assert.Equal(unchecked((ushort)(-64 - (speed & 31))), task.ReadWord(0x16));
        Assert.Equal((ushort)(kind & 1), task.ReadWord(0x1A));
        Assert.Equal((ushort)0, task.ReadWord(0x12));
        Assert.Equal(random, spawned.Memory.SharedEffectRandom);
        Assert.Equal(task.Payload, spawned.Memory.Arena!.Read(0x22C4 + 12, 16));
        Assert.Same(frame, NativeBattleCarbuncle278ReturnParticles.Spawn681C31(frame, 230));
        Assert.Same(frame, NativeBattleCarbuncle278ReturnParticles.Spawn681C31(frame, 235));
        for (int index = 0; index < 8; index++)
        {
            (pool, _) = pool.Append(0x683DC0);
        }
        NativeBattleCarbuncle278Frame full = frame with { Memory = frame.Memory with { SharedEffectRandom = null }, Effect = frame.Effect with { Pools = [pool] } };
        Assert.Same(full, NativeBattleCarbuncle278ReturnParticles.Spawn681C31(full, 231));
    }

    [Fact]
    public void Particle_draw_precedes_velocity_and_release_after_the_fourteenth_frame()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x683DC0, new byte[16].ToImmutableArray());
        task = task.WriteWord(0xC, 32760).WriteWord(0xE, 1).WriteWord(0x10, 2).WriteWord(0x12, 13).WriteWord(0x14, 16).WriteWord(0x16, unchecked((ushort)-64)).WriteWord(0x18, 3);
        Assert.Same(task, NativeBattleCarbuncle278ReturnParticles.Finish683DC0(task, true).Task);
        NativeBattleCarbuncle278ChildStep finished = NativeBattleCarbuncle278ReturnParticles.Finish683DC0(task, false);
        Assert.Equal(2, finished.Result);
        Assert.Equal(unchecked((ushort)-32760), finished.Task.ReadWord(0xC));
        Assert.Equal(unchecked((ushort)-63), finished.Task.ReadWord(0xE));
        Assert.Equal((ushort)5, finished.Task.ReadWord(0x10));
        Assert.Equal((ushort)14, finished.Task.ReadWord(0x12));
    }
}
