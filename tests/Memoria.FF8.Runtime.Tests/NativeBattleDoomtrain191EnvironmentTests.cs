using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191EnvironmentTests
{
    [Theory]
    [InlineData(0x63F7A0u, 0x63F7D0u)]
    [InlineData(0x63F850u, 0x63F880u)]
    public void Flash_factory_writes_only_its_native_counter_duration_amount_and_visibility_flag(uint helper, uint callback)
    {
        NativeBattleDoomtrain191Frame frame = Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(1, NativeBattleTaskFixture.EditTask(pool.Slots[1], taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })) });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        var result = NativeBattleDoomtrain191Environment.Invoke(frame, helper, Services());
        NativeBattleModulePoolTask task = result.Effect.Pools[1].Slots[1];
        Assert.Equal(callback, task.Callback);
        Assert.Equal((ushort)0, task.ReadWord(0xC));
        Assert.Equal((ushort)16, task.ReadWord(0xE));
        Assert.Equal((ushort)4096, task.ReadWord(0x18));
        Assert.Equal((ushort)1, task.ReadWord(0x1A));
        Assert.Equal((ushort)0xA5A5, task.ReadWord(0x10));
        Assert.Equal((ushort)0xA5A5, task.ReadWord(0x22));
    }

    [Fact]
    public void Fade_in_last_tick_publishes_3840_then_unhides_without_synthesizing_an_endpoint_color()
    {
        NativeBattleDoomtrain191Frame frame = Flash(0x63F7D0, 15);
        List<string> writes = [];
        NativeBattleDoomtrain191EnvironmentServices services = Services() with
        {
            PublishDepth = (current, depth) =>
            {
                writes.Add("depth:" + depth);
                return current;
            },
            HideStage = (current, hidden) =>
            {
                writes.Add("hidden:" + hidden);
                return current;
            }
        };
        string replay = NativeBattleTestSnapshots.Json(frame);
        NativeBattleDoomtrain191LeafStep step = NativeBattleDoomtrain191Environment.AdvanceFlash(frame, 0, services);
        Assert.Equal(new[] { "depth:3840", "hidden:False" }, writes);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)16, step.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        writes.Clear();
        var restored = NativeBattleDoomtrain191Environment.AdvanceFlash(NativeBattleTestSnapshots.RestoreJson<NativeBattleDoomtrain191Frame>(replay)!, 0, services);
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(new[] { "depth:3840", "hidden:False" }, writes);
    }

    [Fact]
    public void Fade_out_publishes_and_hides_before_its_pause_gate_and_last_tick_resets_depth()
    {
        List<string> writes = [];
        NativeBattleDoomtrain191EnvironmentServices services = Services() with
        {
            ReadFlags = () => 1,
            PublishDepth = (current, depth) =>
            {
                writes.Add("depth:" + depth);
                return current;
            },
            HideStage = (current, hidden) =>
            {
                writes.Add("hidden:" + hidden);
                return current;
            }
        };
        var paused = NativeBattleDoomtrain191Environment.AdvanceFlash(Flash(0x63F880, 0), 0, services);
        Assert.Equal(new[] { "depth:4096", "hidden:True" }, writes);
        Assert.Equal((ushort)0, paused.Frame.Effect.Pools[1].Slots[0].ReadWord(0xC));
        Assert.Equal(0, paused.Result);
        writes.Clear();
        var last = NativeBattleDoomtrain191Environment.AdvanceFlash(Flash(0x63F880, 15), 0, services with { ReadFlags = () => 0 });
        Assert.Equal(new[] { "depth:256", "depth:0" }, writes);
        Assert.Equal(2, last.Result);
    }

    [Theory]
    [InlineData(0x63F780u, "stage:True")]
    [InlineData(0x63F830u, "stage:False")]
    [InlineData(0x63F930u, "target:True")]
    [InlineData(0x63F8F0u, "target:False")]
    public void Visibility_helpers_route_the_correct_canonical_stage_or_captured_target_owner(uint helper, string expected)
    {
        List<string> writes = [];
        NativeBattleDoomtrain191EnvironmentServices services = Services() with
        {
            HideStage = (current, hidden) =>
            {
                writes.Add("stage:" + hidden);
                return current;
            },
            HideTargets = (current, hidden) =>
            {
                writes.Add("target:" + hidden);
                return current;
            }
        };
        NativeBattleDoomtrain191Environment.Invoke(Fixture(), helper, services);
        Assert.Equal(new[] { expected }, writes);
    }

    private static NativeBattleDoomtrain191Frame Fixture() => NativeBattleDoomtrain191Tests.Initialize(NativeBattleDoomtrain191Tests.Fixture());
    private static NativeBattleDoomtrain191EnvironmentServices Services() => new(() => 0, (current, _) => current, (current, _) => current, (current, _) => current);

    private static NativeBattleDoomtrain191Frame Flash(uint callback, ushort age)
    {
        NativeBattleDoomtrain191Frame frame = Fixture();
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = NativeBattleTaskFixture.EditTask(pool.Slots[0], taskData => taskData with { Callback = callback });
        task = task.WriteWord(0xC, age).WriteWord(0xE, 16).WriteWord(0x18, 4096).WriteWord(0x1A, 1);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, task) });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
    }
}
