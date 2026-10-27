using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79StackTests
{
    [Fact]
    public void Waiting_body_consumes_current_bounds_saved_pointer_and_its_four_actual_pushes()
    {
        var fixture = Fixture();
        NativeBattleStackBytes stack = NativeBattleEffect79Stack.ObserveTargetBounds(NativeBattleEffect79Stack.Begin(100), new(new(3, 0), null, 112), 2);
        stack = NativeBattleEffect79Stack.ObserveBody(stack, fixture.Pool, fixture.Address, fixture.Task, false);
        Assert.Equal(0x1939068u, stack.ReadUInt32(-0x50));
        Assert.Equal(0x7BD921u, stack.ReadUInt32(-0x4C));
        Assert.Equal(2u, stack.ReadUInt32(-0x48));
        Assert.Equal(fixture.Address, stack.ReadUInt32(-0x44));
        Assert.Equal(0x1D97530u, stack.ReadUInt32(-0x40));
        Assert.Equal(9, NativeBattleEffect79Stack.ReadRotation(stack).Length);
        Assert.Equal(new uint[] { 0x460865, 0x7BD900, 0x7BD907, 0x7BD90B, 0x7BD91C }, stack.Observations.Select(item => item.Instruction));
        NativeBattleStackBytes restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleStackBytes>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(stack)))!);
        Assert.Equal(NativeBattleEffect79Stack.ReadRotation(stack), NativeBattleEffect79Stack.ReadRotation(restored));
    }

    [Fact]
    public void Body_draw_overwrites_all_twenty_bytes_with_current_saved_pointer_registers()
    {
        var fixture = Fixture();
        NativeBattleStackBytes stack = NativeBattleEffect79Stack.ObserveBody(NativeBattleEffect79Stack.Begin(100), fixture.Pool, fixture.Address, fixture.Task, true);
        Assert.Equal(new uint[] { 0x1D97590, fixture.Address, 0x1D97530, 0x25E5100, 0 }, Enumerable.Range(0, 5).Select(index => stack.ReadUInt32(-0x50 + 4 * index)));
        Assert.Equal(20, stack.Known.Count(known => known));
        Assert.Equal(9, NativeBattleEffect79Stack.ReadRotation(stack).Length);
        Assert.Equal(new uint[] { 0x7BD020, 0x7BD025, 0x7BD026, 0x7BD02E, 0x7BD0FC }, stack.Observations.Select(item => item.Instruction));
    }

    [Fact]
    public void Unobserved_bounds_unknown_pointer_and_actual_intervening_queue_writer_are_not_zero_filled()
    {
        var fixture = Fixture();
        NativeBattleStackBytes unknown = NativeBattleEffect79Stack.ObserveBody(NativeBattleEffect79Stack.Begin(1), fixture.Pool, fixture.Address, fixture.Task, false);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(unknown));
        NativeBattleStackBytes bounds = NativeBattleEffect79Stack.ObserveTargetBounds(NativeBattleEffect79Stack.Begin(1), new(null, 0x2000000, 160), 3);
        Assert.Equal(0x2000080u, bounds.ReadUInt32(-0x50));
        NativeBattleStackBytes queued = NativeBattleEffect79Stack.Invalidate(bounds, 0x48D0A0, "Actual file queue call");
        queued = NativeBattleEffect79Stack.ObserveBody(queued, fixture.Pool, fixture.Address, fixture.Task, false);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(queued));
        Assert.Equal(9, NativeBattleEffect79Stack.ReadRotation(NativeBattleEffect79Stack.ObserveBody(queued, fixture.Pool, fixture.Address, fixture.Task, true)).Length);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ObserveTargetBounds(bounds, new(null, null, 112) { CpuRow = 0 }, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect79Stack.ObserveTargetBounds(bounds, new(new(3, 0), null, 80), 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(NativeBattleEffect79Stack.ObserveBody(NativeBattleEffect79Stack.ObserveTargetBounds(bounds, new(new(3, 0), null, 64), 1), fixture.Pool, fixture.Address, fixture.Task, false)));
    }

    [Fact]
    public void A_different_frame_or_unproved_body_order_starts_unknown()
    {
        var fixture = Fixture();
        NativeBattleStackBytes known = NativeBattleEffect79Stack.ObserveBody(NativeBattleEffect79Stack.Begin(1), fixture.Pool, fixture.Address, fixture.Task, true);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(NativeBattleEffect79Stack.Begin(2)));
        NativeBattleEffectTaskPool changed = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Pool));
        changed.UpdateLinks(null, changed.Tail);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(NativeBattleEffect79Stack.ObserveBody(known, changed, fixture.Address, fixture.Task, true)));
        NativeBattleModulePoolTask otherPhase = fixture.Task.WriteByte(0x29, 9);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(NativeBattleEffect79Stack.ObserveBody(known, fixture.Pool, fixture.Address, otherPhase, false)));
    }

    [Fact]
    public void Body_controller_observes_stack_after_native_draw_branch_and_before_counter_increment()
    {
        var fixture = Fixture();
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null) { Pools = [fixture.Pool] };
        NativeBattleEffect79Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect79State.FromColdProcess().WriteWord(NativeBattleEffect79Profile.TargetAddress + 0x40, 1));
        int calls = 0;
        NativeBattleEffect79Inputs inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            NativeBattleEffect79ObserveBodyStack observed = Assert.IsType<NativeBattleEffect79ObserveBodyStack>(operation);
            Assert.Equal(fixture.Address, observed.TaskAddress);
            Assert.False(observed.DrewBody);
            Assert.Equal(1, NativeBattleEffect79Controller.Read(current, fixture.Address).ReadWord(0x24));
            calls++;
            return current;
        });
        NativeBattleEffect79Step result = NativeBattleEffect79Controller.AdvanceBody(frame, fixture.Address, null!, inputs);
        Assert.Equal(1, calls);
        Assert.Equal(2, NativeBattleEffect79Controller.Read(result.Frame, fixture.Address).ReadWord(0x24));
    }

    [Fact]
    public void Actual_node_expiration_overwrites_bounds_before_waiting_body_pushes_in_instruction_order()
    {
        var fixture = Fixture();
        NativeBattleStackBytes stack = NativeBattleEffect79Stack.ObserveTargetBounds(NativeBattleEffect79Stack.Begin(99), new(new(3, 0), null, 112), 2);
        stack = NativeBattleEffect79Stack.ObserveNode(stack, 0x7B9250, 0x7B9255);
        stack = NativeBattleEffect79Stack.ObserveBody(stack, fixture.Pool, fixture.Address, fixture.Task, false);
        Assert.Equal(0x7B9255u, stack.ReadUInt32(-0x50));
        Assert.Equal(new uint[] { 0x460865, 0x7B9250, 0x7BD900, 0x7BD907, 0x7BD90B, 0x7BD91C }, stack.Observations.Select(item => item.Instruction));
        Assert.All(stack.Observations, observation => Assert.Equal(4, observation.Length));
        Assert.Equal(9, NativeBattleEffect79Stack.ReadRotation(stack).Length);
        NativeBattleStackBytes raster = NativeBattleEffect79Stack.ObserveNode(stack, 0x7B6020, null);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect79Stack.ReadRotation(raster));
    }

    [Fact]
    public void Geometry_expiration_observer_follows_actual_state_store_and_unlink_preserves_the_previous_stack_writer()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 676, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(0x34 + 0x16, 1);
        pool.WriteSlot(0, task);
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleEffectNodeMemory memory = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, 0x200, 0, 0x870, 0x870, 0x9D80);
        byte[] source = new byte[344];
        source[0x16] = 4;
        source[0x1E] = 1;
        int calls = 0;
        NativeBattleEffectNodeInputs inputs = new(NativeBattleCuragaMatrices.Identity8DD770(), _ => throw new InvalidOperationException(), (_, _, _) => null, (_, _) => null)
        {
            GeometryAdvanced = (node, complete) =>
            {
                Assert.Equal(0x870, node);
                Assert.True(complete);
                Assert.Equal(1, memory.Word(node + 0x1CC));
                Assert.Equal(0u, memory.Dword(node + 0x170));
                calls++;
            }
        };
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, new(0x10000000, new byte[344].ToImmutableArray()), inputs);
        lifecycle.AdvanceGeometry(0x870, source);
        lifecycle.AdvanceGeometry(0x870, source);
        Assert.Equal(1, calls);
        Assert.Equal(0, memory.ContextWord(0x16));
    }

    private static (NativeBattleEffectTaskPool Pool, uint Address, NativeBattleModulePoolTask Task) Fixture()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25E5100, 0x25E5110, 180, 2);
        NativeBattleModulePoolTask task = pool.Slots[0].UpdateHeader(pool.Slots[0].Flags, pool.Slots[0].State, pool.Slots[0].Next, 0x7BCF10).WriteByte(0x29, 1).WriteByte(0x2C, 4).WriteWord(0x24, 1);
        pool.WriteSlot(0, task);
        pool.UpdateLinks(0, 0);
        return (pool, pool.BankAddress, task);
    }
}
