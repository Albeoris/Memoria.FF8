using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191CreatureTests
{
    [Fact]
    public void First_callback_constructs_native_model_and_caches_original_signed_scale_before_quarter_write()
    {
        NativeBattleDoomtrain191Frame frame = Task(0, false);
        int draws = 0;
        NativeBattleDoomtrain191LeafStep result = Step(frame, 0, draw: current =>
        {
            draws++;
            Assert.Equal((short)178, Scale(current));
            Assert.Equal(25000, World(current).Z);
            Assert.Equal((short)8192, World(current).Rotation[0]);
            return NativeBattleDoomtrain191Model.Restore642830(current);
        });
        Assert.Equal(1, draws);
        Assert.Equal((ushort)713, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0x1C));
        Assert.Equal((ushort)1, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal(0, result.Result);
        Assert.Equal((byte)1, NativeBattleDoomtrain191Model.Require(result.Frame).Model.Read(0x72, 1)[0]);
    }

    [Fact]
    public void Paused_zero_counter_repeats_the_native_constructor_scale_cache_and_still_draws_without_clock_advance()
    {
        NativeBattleDoomtrain191Frame frame = Task(0, false);
        frame = Step(frame, 1).Frame;
        Assert.Equal((short)178, Scale(frame));
        frame = Step(frame, 1).Frame;
        Assert.Equal((short)44, Scale(frame));
        Assert.Equal((ushort)178, frame.Effect.Pools[1].Slots[0].ReadWord(0x1C));
        Assert.Equal((ushort)0, frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
    }

    [Theory]
    [InlineData(35)]
    [InlineData(69)]
    [InlineData(169)]
    [InlineData(189)]
    [InlineData(211)]
    [InlineData(242)]
    public void Phase_boundary_skips_render_but_always_publishes_current_world_bone_anchor(ushort age)
    {
        NativeBattleDoomtrain191Frame frame = Task(age, true);
        int draws = 0;
        NativeBattleDoomtrain191LeafStep result = Step(frame, 1, draw: current =>
        {
            draws++;
            return current;
        });
        Assert.Equal(0, draws);
        Assert.All(NativeBattleDoomtrain191Model.Require(result.Frame).Anchor.Known.Where((_, index) => index is not (18 or 19)), Assert.True);
        Assert.False(NativeBattleDoomtrain191Model.Require(result.Frame).Anchor.Known[18]);
        Assert.Equal(age, result.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
    }

    [Fact]
    public void Negative_original_scale_uses_truncation_toward_zero_for_quarter_and_tenth_modes()
    {
        NativeBattleDoomtrain191Frame frame = Task(69, true);
        var pool = frame.Effect.Pools[1];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0x1C, unchecked((ushort)-719))) }) } };
        frame = Step(frame, 1).Frame;
        Assert.Equal((short)-71, Scale(frame));
        pool = frame.Effect.Pools[1];
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(0xC, 170)) }) } };
        Assert.Equal((short)-179, Scale(Step(frame, 1).Frame));
    }

    [Fact]
    public void Complete_native_timeline_preserves_all_phase_events_render_skips_half_camera_and_completion_bits()
    {
        NativeBattleDoomtrain191Frame frame = Task(0, false);
        List<(int Age, uint Address)> events = [];
        List<int> draws = [];
        List<int> camera = [];
        List<int> flashes = [];
        int age = 0;
        NativeBattleDoomtrain191CreatureServices services = new(() => 0, (current, address) =>
        {
            events.Add((age, address));
            return current;
        }, current =>
        {
            draws.Add(age);
            return NativeBattleDoomtrain191Model.Restore642830(current);
        }, current =>
        {
            camera.Add(age);
            return current;
        }, current =>
        {
            flashes.Add(age);
            return current;
        });
        for (age = 0; age < 279; age++)
        {
            NativeBattleDoomtrain191LeafStep result = NativeBattleDoomtrain191Creature.Advance641EA0(frame, 0, NativeBattleDoomtrain191ModelTests.SourceLength, services);
            frame = result.Frame;
            Assert.Equal(age == 278 ? 2 : 0, result.Result);
            if (age == 170)
            {
                NativeBattleDoomtrain191Frame restored = JsonSerializer.Deserialize<NativeBattleDoomtrain191Frame>(JsonSerializer.Serialize(frame))!;
                Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
            }
        }
        Assert.Equal(273, draws.Count);
        Assert.DoesNotContain(35, draws);
        Assert.DoesNotContain(69, draws);
        Assert.DoesNotContain(169, draws);
        Assert.DoesNotContain(189, draws);
        Assert.DoesNotContain(211, draws);
        Assert.DoesNotContain(242, draws);
        Assert.Equal(Enumerable.Range(170, 19), camera);
        Assert.Equal(new[] { 245 }, flashes);
        Assert.Equal(3u, frame.Memory.Doomtrain191!.LookupCounter);
        Assert.Equal((ushort)279, frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Contains((14, 0x6437F0u), events);
        Assert.Contains((35, 0x643FD0u), events);
        Assert.Contains((35, 0x644E10u), events);
        Assert.Contains((36, 0x6439C0u), events);
        Assert.Contains((70, 0x643C10u), events);
        Assert.Contains((169, 0x642EA0u), events);
        Assert.Contains((188, 0x63F930u), events);
        Assert.Contains((211, 0x642E40u), events);
        Assert.Contains((242, 0x643CD0u), events);
        Assert.Contains((244, 0x646570u), events);
        Assert.Contains((245, 0x646AA0u), events);
        foreach (uint callback in new uint[] { 0x643D10, 0x642EF0, 0x646180, 0x6435B0 })
        {
            Assert.Single(frame.Effect.Pools[1].Slots, task => (task.Flags & 1) != 0 && task.Callback == callback);
        }
    }

    internal static NativeBattleDoomtrain191Frame Task(ushort age, bool ready)
    {
        NativeBattleDoomtrain191Frame frame = ready ? NativeBattleDoomtrain191ModelTests.Ready() : NativeBattleDoomtrain191ModelTests.Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        var task = (pool.Slots[0] with { Callback = 0x641EA0 }).WriteWord(0xC, age).WriteWord(0x1C, 713);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(0, task) }) } };
    }
    private static NativeBattleDoomtrain191LeafStep Step(NativeBattleDoomtrain191Frame frame, uint flags, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191Frame>? draw = null)
    {
        return NativeBattleDoomtrain191Creature.Advance641EA0(frame, 0, NativeBattleDoomtrain191ModelTests.SourceLength, new(() => flags, (current, _) => current, draw ?? NativeBattleDoomtrain191Model.Restore642830, current => current, current => current));
    }
    private static short Scale(NativeBattleDoomtrain191Frame frame) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(26, 2));
    private static NativeBattleActionMatrix World(NativeBattleDoomtrain191Frame frame) => NativeBattleArenaPose.ReadMatrix(NativeBattleDoomtrain191Model.Require(frame).Model, 0x40);
}
