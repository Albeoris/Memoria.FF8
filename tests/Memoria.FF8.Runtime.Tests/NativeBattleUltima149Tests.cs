using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltima149Tests
{
    [Fact]
    public void Entry_clears_only_particle_type_DWORDs_and_retains_warm_module_owners()
    {
        NativeBattleUltima149State original = NativeBattleUltima149State.FromColdProcess() with { ParticlesA = Enumerable.Repeat((byte)0xA5, 2400).ToImmutableArray(), ParticlesB = Enumerable.Repeat((byte)0x5A, 2400).ToImmutableArray(), RingScale = 12345, VibrationHandle = 17, SideFlags = [1, 0, 0, 0, 0, 0, 0] };
        NativeBattleUltima149State entered = original.Initialize();
        for (int index = 0; index < 100; index++)
        {
            Assert.Equal(new byte[4], entered.ParticlesA.AsSpan().Slice(index * 24, 4).ToArray());
            Assert.Equal(original.ParticlesA.AsSpan().Slice(index * 24 + 4, 20).ToArray(), entered.ParticlesA.AsSpan().Slice(index * 24 + 4, 20).ToArray());
            Assert.Equal(new byte[4], entered.ParticlesB.AsSpan().Slice(index * 24, 4).ToArray());
            Assert.Equal(original.ParticlesB.AsSpan().Slice(index * 24 + 4, 20).ToArray(), entered.ParticlesB.AsSpan().Slice(index * 24 + 4, 20).ToArray());
        }
        Assert.Equal(original.RingScale, entered.RingScale);
        Assert.Equal(original.VibrationHandle, entered.VibrationHandle);
        Assert.Equal(original.SideFlags, entered.SideFlags);
        Assert.Equal(original.CoreVertices, entered.CoreVertices);
        NativeBattleUltima149State cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleUltima149State>(NativeBattleTestSnapshots.Json(entered))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(entered.Initialize()), NativeBattleTestSnapshots.Json(cold.Initialize()));
    }

    [Fact]
    public void Signed_particle_motion_keeps_shifts_distinct_from_truncating_shard_growth()
    {
        NativeBattleUltima149Particle particle = new(new byte[24].ToImmutableArray());
        particle = particle.Word(6, -7).SetVector(8, new(32760, 1, 0)).SetVector(16, new(20, -1, -17));
        NativeBattleUltima149Particle streak = NativeBattleUltima149Motion.Move(particle, 2);
        Assert.Equal((short)-32756, streak.Position.X);
        Assert.Equal((short)0, streak.Position.Y);
        Assert.Equal((short)18, streak.Velocity.X);
        Assert.Equal((short)0, streak.Velocity.Y);
        Assert.Equal((short)-14, streak.Velocity.Z);
        Assert.Equal((short)-6, streak.Scale);
        NativeBattleUltima149Particle shard = NativeBattleUltima149Motion.Move(particle.Word(14, 32760).Word(22, 20), 4);
        Assert.Equal((short)-8, shard.Scale);
        Assert.Equal((short)-32756, shard.Signed(14));
        NativeBattleUltima149Particle spark = NativeBattleUltima149Motion.Move(particle, 16);
        Assert.Equal((short)15, spark.Velocity.X);
        Assert.Equal((short)-12, spark.Velocity.Z);
        NativeBattleUltima149Particle cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleUltima149Particle>(NativeBattleTestSnapshots.Json(streak))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleUltima149Motion.Move(streak, 2)), NativeBattleTestSnapshots.Json(NativeBattleUltima149Motion.Move(cold, 2)));
    }

    [Fact]
    public void Energy_saturates_wrapped_scale_and_changes_acceleration_after_frame33()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 36, 1);
        (pool, int slot) = pool.Append(0x610700);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 33).WriteWord(24, 32760).WriteWord(26, 40).WriteWord(28, 32760).WriteWord(30, 64);
        NativeBattleModulePoolTask original = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(task));
        NativeBattleModulePoolTask first = NativeBattleUltima149Motion.Energy(task);
        Assert.Equal((ushort)32767, first.ReadWord(28));
        Assert.Equal(unchecked((ushort)-32736), first.ReadWord(24));
        Assert.Equal((ushort)66, first.ReadWord(30));
        NativeBattleModulePoolTask second = NativeBattleUltima149Motion.Energy(original.WriteWord(12, 34));
        Assert.Equal((ushort)69, second.ReadWord(30));
    }

    [Fact]
    public void Core_morph_interpolates_exact120_vertices_and_retains_each_padding_WORD()
    {
        byte[] core = new byte[968];
        byte[] shape = new byte[960];
        for (int vertex = 0; vertex < 120; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(vertex * 8 + axis * 2), 4096);
            }
        }
        NativeBattleStaticMeshResource source = new(0, 0x1000, core.ToImmutableArray());
        ImmutableArray<byte> retained = Enumerable.Repeat((byte)0xA5, 960).ToImmutableArray();
        ImmutableArray<byte> morphed = NativeBattleUltima149Motion.MorphCore(source, shape.ToImmutableArray(), retained, 40);
        for (int vertex = 0; vertex < 120; vertex++)
        {
            Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(morphed.AsSpan().Slice(vertex * 8)));
            Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(morphed.AsSpan().Slice(vertex * 8 + 6)));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleUltima149Motion.MorphCore(source, [], retained, 0));
    }
}
