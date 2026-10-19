using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191RigTests
{
    [Fact]
    public void Initial_rig_writes_only_its_matrix_variant_and_spawns_all_five_native_children_in_order()
    {
        NativeBattleDoomtrain191Frame frame = Fixture(0x63FB40, 0);
        frame = frame with { Memory = frame.Memory with { Doomtrain191 = frame.Memory.Doomtrain191! with { Rigs = null } } };
        int streams = 0;
        NativeBattleDoomtrain191RigServices services = new(() => 0, (_, _, _) => throw new InvalidOperationException(), current =>
        {
            streams++;
            return current;
        });
        NativeBattleDoomtrain191LeafStep result = NativeBattleDoomtrain191Rigs.Advance(frame, 0, services);
        NativeBattleEffectTaskPool children = result.Frame.Effect.Pools[1];
        Assert.Equal(new uint[] { 0x63FB40, 0x63FDB0, 0x63FEC0, 0x63FFD0, 0x640280, 0x640140 }, Active(children).Select(task => task.Callback));
        Assert.Equal((ushort)1, children.Slots[0].ReadWord(0xC));
        Assert.Equal(1, streams);
        Assert.Equal(0, result.Result);
        NativeBattleDoomtrain191RigBank bank = result.Frame.Memory.Doomtrain191!.Rigs!;
        Assert.False(bank.World[0].Known[18]);
        Assert.All(bank.World[1].Known, value => Assert.False(value));
        Assert.False(bank.View[0].Known[18]);
        Assert.Equal(-100, bank.World[0].ReadMatrix().X);
        Assert.Equal((ushort)0xA5A5, children.Slots[1].ReadWord(0x18));
        Assert.Equal((ushort)0xA5A5, children.Slots[5].ReadWord(0x16));
        Assert.Equal((ushort)1536, children.Slots[3].ReadWord(0x20));
        Assert.Equal((ushort)0xFC00, children.Slots[3].ReadWord(0x18));
        Assert.Equal((ushort)0xFFCD, children.Slots[3].ReadWord(0x1A));
        streams = 0;
        NativeBattleDoomtrain191LeafStep restored = NativeBattleDoomtrain191Rigs.Advance(RoundTrip(frame), 0, services);
        Assert.Equal(1, streams);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(14, -180)]
    [InlineData(21, 180)]
    public void Rig_emission_uses_signed_division_parity_and_actual_counter_gate(int age, int expectedX)
    {
        NativeBattleDoomtrain191Frame frame = Fixture(0x63FB40, age);
        int streams = 0;
        NativeBattleDoomtrain191LeafStep step = NativeBattleDoomtrain191Rigs.Advance(frame, 0, new(() => 0, (_, _, _) => throw new InvalidOperationException(), current =>
        {
            streams++;
            return current;
        }));
        NativeBattleModulePoolTask child = Assert.Single(Active(step.Frame.Effect.Pools[1]), task => task.Callback == 0x640140);
        Assert.Equal(unchecked((ushort)expectedX), child.ReadWord(0x10));
        Assert.Equal((ushort)0xA5A5, child.ReadWord(0x16));
        Assert.Equal((ushort)1920, child.ReadWord(0x1C));
        Assert.Equal(1, streams);
    }

    [Fact]
    public void Paused_rig_composes_its_retained_matrix_without_initialization_or_counter_change()
    {
        NativeBattleDoomtrain191Frame frame = Fixture(0x63FB40, 0);
        NativeBattleDoomtrain191LeafStep step = NativeBattleDoomtrain191Rigs.Advance(frame, 0, new(() => 1, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException()));
        Assert.Single(Active(step.Frame.Effect.Pools[1]));
        Assert.Equal((ushort)0, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.False(step.Frame.Memory.Doomtrain191!.Rigs!.View[0].Known[18]);
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(25, -973)]
    [InlineData(45, -1004)]
    [InlineData(46, -1043)]
    [InlineData(51, 0)]
    public void Wheel_draws_the_old_angle_before_its_native_rotation_correction(int age, int expectedAngle)
    {
        NativeBattleDoomtrain191Frame frame = Fixture(0x63FFD0, age);
        NativeBattleModulePoolTask original = frame.Effect.Pools[1].Slots[0];
        int draws = 0;
        NativeBattleDoomtrain191LeafStep step = NativeBattleDoomtrain191Rigs.Advance(frame, 0, new(() => 0, (current, task, matrix) =>
        {
            draws++;
            Assert.Equal(original, task);
            Assert.NotEmpty(matrix.Rotation);
            return current;
        }, _ => throw new InvalidOperationException()));
        Assert.Equal(1, draws);
        Assert.Equal(unchecked((ushort)expectedAngle), step.Frame.Effect.Pools[1].Slots[0].ReadWord(0x18));
        Assert.Equal(unchecked((ushort)(age + 1)), step.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
    }

    [Theory]
    [InlineData(0x63FDB0u)]
    [InlineData(0x63FEC0u)]
    [InlineData(0x63FFD0u)]
    [InlineData(0x640280u)]
    [InlineData(0x640140u)]
    public void Every_child_draws_while_paused_without_advancing_its_counter_or_completion(uint callback)
    {
        NativeBattleDoomtrain191Frame frame = Fixture(callback, 200);
        int draws = 0;
        NativeBattleDoomtrain191LeafStep step = NativeBattleDoomtrain191Rigs.Advance(frame, 0, new(() => 1, (current, _, _) =>
        {
            draws++;
            return current;
        }, _ => throw new InvalidOperationException()));
        Assert.Equal(1, draws);
        Assert.Equal((ushort)200, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal(0, step.Result);
    }

    private static NativeBattleDoomtrain191Frame Fixture(uint callback, int age)
    {
        var fixture = NativeBattleDoomtrain191Tests.Fixture();
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Tests.Initialize(fixture);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        ImmutableArray<NativeBattleModulePoolTask> tasks = children.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray();
        NativeBattleModulePoolTask selected = tasks[0] with { Callback = callback };
        selected = selected.WriteWord(0xC, unchecked((ushort)age)).WriteWord(0xE, 0).WriteWord(0x10, unchecked((ushort)-100)).WriteWord(0x12, 200).WriteWord(0x14, 300).WriteWord(0x16, 103).WriteWord(0x18, 0xFC00).WriteWord(0x1A, 0xFFCD).WriteWord(0x1C, 4096).WriteWord(0x20, callback == 0x63FB40 ? (ushort)1 : (ushort)4096);
        children = children with { Slots = tasks.SetItem(0, selected) };
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
    }

    private static IEnumerable<NativeBattleModulePoolTask> Active(NativeBattleEffectTaskPool pool) => pool.Slots.Where(task => (task.Flags & 1) != 0);
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
