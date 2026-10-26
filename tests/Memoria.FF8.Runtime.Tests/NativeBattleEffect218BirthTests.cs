using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218BirthTests
{
    [Fact]
    public void Burst_birth_consumes_eight_actual_draws_and_keeps_each_native_task_payload_owner()
    {
        NativeBattleEffect218Frame frame = Fixture();
        NativeCrtRandomState expected = frame.Random;
        ushort[] draws = new ushort[8];
        for (int index = 0; index < draws.Length; index++)
        {
            expected = expected.Draw(out draws[index]);
        }
        frame = SpawnBurst6345E0(frame);
        Assert.Equal(expected, frame.Random);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        Assert.Equal(new uint[] { 0x634750, 0x634890, 0x634890, 0x634A10, 0x634B90 }, pool.Slots.Take(5).Select(task => task.Callback));
        Assert.Equal(4, pool.Tail);
        Assert.Equal((ushort)0x900, pool.Slots[0].ReadWord(28));
        Assert.Equal((ushort)0x900, pool.Slots[0].ReadWord(30));
        for (int index = 0; index < 2; index++)
        {
            NativeBattleModulePoolTask task = pool.Slots[index + 1];
            Assert.Equal(unchecked((ushort)((draws[index * 3] & 2047) - 1024)), task.ReadWord(24));
            Assert.Equal(unchecked((ushort)(-30 - draws[index * 3 + 1] % 90)), task.ReadWord(26));
            Assert.Equal((ushort)(((draws[index * 3 + 2] & 4095) + 0x1600) / 10), task.ReadWord(32));
            Assert.Equal(task.ReadWord(32), task.ReadWord(34));
            Assert.Equal((ushort)0xB80, task.ReadWord(28));
            Assert.Equal((ushort)0xB80, task.ReadWord(30));
        }
        Assert.Equal(unchecked((ushort)((draws[6] & 1023) - 512)), pool.Slots[3].ReadWord(24));
        Assert.Equal((ushort)(draws[7] % 120 + 15), pool.Slots[3].ReadWord(26));
        Assert.Equal((ushort)0x740, pool.Slots[3].ReadWord(28));
        Assert.Equal((ushort)0x3AA, pool.Slots[3].ReadWord(32));
        Assert.Equal((ushort)40, pool.Slots[4].ReadWord(14));
    }

    [Fact]
    public void Pair_and_spin_births_consume_their_actual_CRT_draws_and_keep_linked_append_order()
    {
        NativeBattleEffect218Frame frame = Fixture();
        NativeCrtRandomState expected = frame.Random.Draw(out ushort first).Draw(out ushort second);
        frame = SpawnPair632FF0(frame);
        Assert.Equal(expected, frame.Random);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        Assert.Equal(0, pool.Head);
        Assert.Equal(1, pool.Tail);
        Assert.Equal(1, pool.Slots[0].Next);
        Assert.Equal((ushort)0, pool.Slots[0].ReadWord(14));
        Assert.Equal(unchecked((ushort)((first & 2047) - 1024)), pool.Slots[0].ReadWord(24));
        Assert.Equal(unchecked((ushort)-16), pool.Slots[0].ReadWord(26));
        Assert.Equal((ushort)1, pool.Slots[1].ReadWord(14));
        Assert.Equal((ushort)(second & 511), pool.Slots[1].ReadWord(24));
        Assert.Equal(unchecked((ushort)-8), pool.Slots[1].ReadWord(26));
        expected = expected.Draw(out ushort angle).Draw(out ushort velocity);
        frame = SpawnSpin633500(frame);
        Assert.Equal(expected, frame.Random);
        Assert.Equal(2, frame.Effect.Pools[0].Tail);
        Assert.Equal((ushort)(angle & 511), frame.Effect.Pools[0].Slots[2].ReadWord(24));
        Assert.Equal((ushort)(velocity % 30 + 25), frame.Effect.Pools[0].Slots[2].ReadWord(26));
    }

    [Fact]
    public void Ray_birth_rotates_before_adding_signed_WORD_world_coordinates_and_preserves_unwritten_payload()
    {
        NativeBattleEffect218Frame frame = Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[0];
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(14, 0xAA55)) };
        NativeBattleEffectArena basis = NativeBattleEffectArena.Clear().Write(0, new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }.SelectMany(BitConverter.GetBytes).ToArray()).Write(20, BitConverter.GetBytes(32760)).Write(24, BitConverter.GetBytes(-32760)).Write(28, BitConverter.GetBytes(37));
        frame = frame with { Effect = frame.Effect with { Pools = ImmutableArray.Create(pool) }, State = frame.State.WriteView(CameraBasis, basis, 32) };
        NativeBattleEffect218Frame result = SpawnRay6331A0(frame);
        NativeBattleModulePoolTask task = result.Effect.Pools[0].Slots[0];
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal((ushort)0xAA55, task.ReadWord(14));
        Assert.Equal((ushort)32760, task.ReadWord(16));
        Assert.Equal(unchecked((ushort)(-32760 - 10000)), task.ReadWord(18));
        Assert.Equal((ushort)37, task.ReadWord(20));
    }

    private static NativeBattleEffect218Frame Fixture()
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = ImmutableArray.Create(pool) };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(0xBEEF), NativeBattleEffect218State.FromColdProcess());
    }
}
