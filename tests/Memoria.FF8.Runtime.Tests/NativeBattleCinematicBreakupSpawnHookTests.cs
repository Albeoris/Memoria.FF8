using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupSpawnHookTests
{
    [Fact]
    public void Fallback_emits_base_particles_then_hook_then_extra_particles_with_one_random_stream()
    {
        NativeCrtRandomState initial = new(123);
        List<string> calls = [];
        NativeBattleControl18SpawnServices services = new(1, (random, x, y, z, count) =>
        {
            Assert.Equal(Advance(initial, calls.Count == 0 ? 7 : 20), random);
            Assert.Equal((3 << 8, 1 << 8, 5 << 8), (x, y, z));
            Assert.Equal(2, count);
            calls.Add("auxiliary");
            return Advance(random, count * 6);
        })
        {
            EmitFragmentHook = (random, x, y, z) =>
            {
                Assert.Equal(new[] { "auxiliary" }, calls);
                Assert.Equal(Advance(initial, 19), random);
                Assert.Equal((3 << 8, 1 << 8, 5 << 8), (x, y, z));
                calls.Add("hook");
                return Advance(random, 1);
            }
        };
        NativeBattleControl18SpawnStep result = Spawn(Controls(2), initial, services);
        Assert.Equal(new[] { "auxiliary", "hook", "auxiliary" }, calls);
        Assert.Equal(Advance(initial, 32), result.Random);
        Assert.Equal((ushort)1, result.LiveCount);
    }

    [Fact]
    public void Zero_auxiliary_count_skips_both_loops_but_keeps_the_fragment_hook()
    {
        NativeCrtRandomState initial = new(123);
        int hooks = 0;
        NativeBattleControl18SpawnServices services = new(1, (_, _, _, _, _) => throw new InvalidOperationException())
        {
            EmitFragmentHook = (random, _, _, _) =>
            {
                hooks++;
                Assert.Equal(Advance(initial, 7), random);
                return Advance(random, 1);
            }
        };
        NativeBattleControl18SpawnStep result = Spawn(Controls(0), initial, services);
        Assert.Equal(1, hooks);
        Assert.Equal(Advance(initial, 8), result.Random);
        Assert.Equal((ushort)1, result.LiveCount);
    }

    [Fact]
    public void A_selected_hook_requires_its_service_before_fragment_initialization()
    {
        NativeBattleControl18SpawnServices services = new(1, (_, _, _, _, _) => throw new InvalidOperationException());
        Assert.Throws<NotSupportedException>(() => Spawn(Controls(0), new(123), services));
    }

    private static NativeBattleControl18SpawnStep Spawn(byte[] controls, NativeCrtRandomState random, NativeBattleControl18SpawnServices services)
    {
        NativeBattleControl18Face face = new(0x123456, false, [new(3, 1, 5), new(3, 1, 5), new(3, 1, 5)]);
        return NativeBattleControl18Spawn.Advance(new byte[68], new byte[1], 0, controls, [face], 0, random, services);
    }

    private static byte[] Controls(ushort emissions)
    {
        byte[] result = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0xE), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x2E), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x30), emissions);
        result[0xC] = 15;
        return result;
    }

    private static NativeCrtRandomState Advance(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
