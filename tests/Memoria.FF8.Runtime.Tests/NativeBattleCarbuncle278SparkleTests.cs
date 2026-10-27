using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278SparkleTests
{
    [Fact]
    public void Spawning_consumes_four_shared_draws_even_paused_and_writes_actual_bank_xyz_and_age()
    {
        var fixture = Fixture();
        NativeBattleCarbuncle278Frame result = NativeBattleCarbuncle278Sparkles.Spawn681B3F(NativeBattleTestSnapshots.RestoreJson<NativeBattleCarbuncle278Frame>(NativeBattleTestSnapshots.Json(fixture.Frame)), fixture.Creature);
        NativeBattleEffectTaskPool pool = Assert.Single(result.Effect.Pools);
        Assert.Equal(4, pool.Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleSharedEffectRandomState random = new(0);
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw7059E0(out ushort value);
            int bone = value * 3 >> 15;
            NativeBattleModulePoolTask task = pool.Slots[index];
            Assert.Equal(0x683E80u, task.Callback);
            Assert.Equal((ushort)(100 + bone), task.ReadWord(0xC));
            Assert.Equal((ushort)(200 + bone), task.ReadWord(0xE));
            Assert.Equal((ushort)(300 + bone), task.ReadWord(0x10));
            Assert.Equal((ushort)0, task.ReadWord(0x12));
            Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload, result.Memory.Arena!.Read(0x23A4 + index * 20 + 12, 8));
        }
        Assert.Equal(random, result.Memory.SharedEffectRandom);
        Assert.Equal(7u, result.Memory.Carbuncle278!.Pause8114);
        Assert.Equal(0u, fixture.Frame.Memory.SharedEffectRandom!.State);
        Assert.All(fixture.Frame.Effect.Pools[0].Slots, task => Assert.Equal((ushort)0, task.Flags));
        NativeBattleCarbuncle278Frame independent = NativeBattleTestSnapshots.RestoreJson<NativeBattleCarbuncle278Frame>(NativeBattleTestSnapshots.Json(fixture.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCarbuncle278Sparkles.Spawn681B3F(independent, fixture.Creature)));
    }

    [Fact]
    public void Exhaustion_skips_draws_and_partial_capacity_consumes_only_successful_append()
    {
        var fixture = Fixture();
        NativeBattleEffectTaskPool pool = fixture.Frame.Effect.Pools[0];
        for (int index = 0; index < 63; index++)
        {
            (pool, _) = pool.Append(0x683E80);
        }
        NativeBattleCarbuncle278Frame source = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [pool] } };
        NativeBattleCarbuncle278Frame one = NativeBattleCarbuncle278Sparkles.Spawn681B3F(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(source)), fixture.Creature);
        Assert.Equal(14u, one.Memory.SharedEffectRandom!.State);
        Assert.Equal(64, one.Effect.Pools[0].Slots.Count(task => (task.Flags & 1) != 0));
        NativeBattleCarbuncle278Frame full = NativeBattleCarbuncle278Sparkles.Spawn681B3F(one, fixture.Creature);
        Assert.Equal(NativeBattleTestSnapshots.Json(one), NativeBattleTestSnapshots.Json(full));
        NativeBattleCarbuncle278Frame unknownFull = one with { Memory = one.Memory with { CarbuncleExecutableMemory = null, SharedEffectRandom = null } };
        NativeBattleCarbuncle278Frame skipped = NativeBattleCarbuncle278Sparkles.Spawn681B3F(unknownFull, fixture.Creature);
        Assert.Null(skipped.Memory.CarbuncleExecutableMemory);
        Assert.Null(skipped.Memory.SharedEffectRandom);
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Sparkles.Spawn681B3F(fixture.Frame with { Memory = fixture.Frame.Memory with { SharedEffectRandom = null } }, fixture.Creature));
    }

    [Fact]
    public void Age_finishes_after_eleven_draws_and_pause_retains_the_reused_payload()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x683E80, Enumerable.Repeat((byte)0xA5, 8).ToImmutableArray());
        task = task.WriteWord(0x12, 10);
        NativeBattleCarbuncle278SparkleStep paused = NativeBattleCarbuncle278Sparkles.Finish683E80(task, true);
        Assert.Same(task, paused.Task);
        Assert.Equal(0, paused.Result);
        NativeBattleCarbuncle278SparkleStep finished = NativeBattleCarbuncle278Sparkles.Finish683E80(task, false);
        Assert.Equal((ushort)11, finished.Task.ReadWord(0x12));
        Assert.Equal(2, finished.Result);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload.Take(6), NativeBattleStorageSnapshots.Capture(finished.Task).Payload.Take(6));
        Assert.Equal(0, NativeBattleCarbuncle278Sparkles.Finish683E80(task.WriteWord(0x12, 9), false).Result);
    }

    [Fact]
    public void Bone_fraction_uses_world_matrix_and_parent_length_without_modifying_local_pose()
    {
        var fixture = Fixture();
        NativeBattleCarbuncleExecutableMemory executable = fixture.Frame.Memory.CarbuncleExecutableMemory!;
        executable = executable.Write(NativeBattleCarbuncleExecutableMemory.Address + 16 + 48, new byte[] { 0, 0, 100, 0 });
        string before = NativeBattleTestSnapshots.Json(executable);
        NativeBattleVector result = NativeBattleCarbuncle278Anchors.ReadBone502170(fixture.Creature, executable, 1, 2048);
        Assert.Equal(new NativeBattleVector(101, 201, 351), result);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(executable));
        Assert.Throws<InvalidDataException>(() => NativeBattleCarbuncle278Anchors.ReadBone502170(fixture.Creature, executable, 3, 0));
        Assert.Equal(new NativeBattleVector(-1, 2, -3), NativeBattleCarbuncle278Anchors.ReadBone502170(fixture.Creature.WriteWord(0x20, 0).WriteWord(0x3C, ushort.MaxValue).WriteWord(0x3E, 2).WriteWord(0x40, unchecked((ushort)-3)), executable, 999, 0));
    }

    [Fact]
    public void Vertex_anchor_uses_selected_group_world_matrix_and_preserves_current_skeleton_bytes()
    {
        var fixture = Fixture();
        byte[] bytes = new byte[48];
        int[] header = [4, 24, 24, 48, 48, 48];
        for (int index = 0; index < header.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), header[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 7);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 9);
        NativeBattleEffectOwnedModelResource resource = new(0x10B6F94, bytes.ToImmutableArray(), new([], []), []);
        NativeBattleCarbuncleExecutableMemory executable = fixture.Frame.Memory.CarbuncleExecutableMemory!;
        string before = NativeBattleTestSnapshots.Json(executable);
        Assert.Equal(new NativeBattleVector(109, 194, 311), NativeBattleCarbuncle278Anchors.ReadVertex682270(fixture.Creature, executable, resource, 0, 0));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(executable));
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Anchors.ReadVertex682270(fixture.Creature, executable, resource, 0, 1));
    }

    private static (NativeBattleCarbuncle278Frame Frame, NativeBattleModulePoolTask Creature) Fixture()
    {
        NativeBattleEffectArena skeleton = NativeBattleEffectArena.Clear().Write(0, new byte[] { 3 });
        for (int index = 0; index < 3; index++)
        {
            skeleton = NativeBattleArenaPose.WriteMatrix(skeleton, 32 + index * 48, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], index, index, index, 4096), true);
        }
        NativeBattleCarbuncleExecutableMemory executable = new(skeleton);
        NativeBattleModulePoolTask creature = new(1, 0, null, 0x681630, new byte[0x5E0 - 12].ToImmutableArray());
        creature = creature.WriteWord(0x20, 2).WriteWord(0x60, 4096).WriteWord(0x68, 4096).WriteWord(0x70, 4096).WriteDword(0x74, 100).WriteDword(0x78, 200).WriteDword(0x7C, 300);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508178, NativeBattleEffectNodeMemory.ArenaAddress + 0x23A4, 0x14, 64);
        NativeBattleOriginalEffectState effect = new(278, 277, new(278, 0x680C50, 0x680C60), new(0, 0, 3, 0, 0, 0), null) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), CarbuncleExecutableMemory = executable, Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess() with { Pause8114 = 7 } };
        return (new(memory, effect, NativeCrtRandomState.FromColdProcess()), creature);
    }
}
