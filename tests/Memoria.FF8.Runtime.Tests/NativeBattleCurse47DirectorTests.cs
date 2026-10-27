using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47DirectorTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(5, 1, 6)]
    [InlineData(8, 2, 9)]
    public void Mode_requests_require_the_exact_predecessor_and_do_not_observe_the_new_mode_in_the_same_pass(byte phase, ushort before, byte after)
    {
        Fixture fixture = new(phase);
        NativeBattleCurse47Frame frame = fixture.Mode(fixture.Frame, before);
        NativeBattleCurse47Frame branch = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleCurse47Frame result = fixture.Step(frame).Frame;
        Assert.Equal(after, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(before, result.State.SharedWord(0x40));
        Assert.Equal(before, result.State.SharedWord(0x42));
        Assert.Equal((ushort)(before + 1), result.State.SharedWord(0x44));
        Assert.Empty(fixture.Operations);
        NativeBattleCurse47Frame failed = fixture.Step(fixture.Mode(branch, unchecked((ushort)(before - 1)))).Frame;
        Assert.Equal(phase, fixture.Task(failed).ReadByte(0x29));
    }

    [Fact]
    public void Observed_modes_publish_before_spawn_and_preserve_callback_changes_before_latching_the_next_request()
    {
        Fixture fixture = new(11);
        NativeBattleCurse47Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x40, 1).WithSharedWord(0x42, 2).WithSharedWord(0x44, 3).WithSharedWord(0x46, 0xFFFF) };
        fixture.OnExecute = (current, operation) =>
        {
            Assert.Equal(new NativeBattleCurse47ObservedMode(fixture.Address, 2), operation);
            Assert.Equal((ushort)2, current.State.SharedWord(0x40));
            Assert.Equal((ushort)0, current.State.SharedWord(0x46));
            return current with { State = current.State.WithSharedWord(0x44, 4).WithSharedWord(8, 0xA5A5), Random = new(999) };
        };
        NativeBattleCurse47Frame result = fixture.Step(frame).Frame;
        Assert.Equal((ushort)2, result.State.SharedWord(0x40));
        Assert.Equal((ushort)4, result.State.SharedWord(0x42));
        Assert.Equal((ushort)0xA5A5, result.State.SharedWord(8));
        Assert.Equal(new NativeCrtRandomState(999), result.Random);
        Assert.Single(fixture.Operations);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(65535, 1)]
    public void First_observed_gate_is_signed_and_sound_uses_the_supplied_resolved_value(ushort observed, byte phase)
    {
        Fixture fixture = new(1);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Mode(fixture.Frame, observed)).Frame;
        Assert.Equal(phase, fixture.Task(result).ReadByte(0x29));
        if (phase == 2)
        {
            Assert.Equal(new NativeBattleCurse47Sound(0x64221, 128), Assert.Single(fixture.Operations));
        }
        else
        {
            Assert.Empty(fixture.Operations);
        }
    }

    [Fact]
    public void IO_release_advances_the_real_wait_record_once_and_retains_the_director_phase_until_released()
    {
        Fixture fixture = new(2);
        NativeBattleCurse47Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(NativeBattleCurse47Io.ControlAddress + 0x166, 1) };
        NativeBattleCurse47Frame blocked = fixture.Step(frame).Frame;
        Assert.Equal((byte)2, fixture.Task(blocked).ReadByte(0x29));
        frame = blocked with { State = blocked.State.WriteWord(NativeBattleCurse47Io.ControlAddress + 0x166, 0) };
        NativeBattleCurse47Frame result = fixture.Step(frame).Frame;
        Assert.Equal((byte)3, fixture.Task(result).ReadByte(0x29));
        Assert.Equal((ushort)1, result.State.ReadIoControlWord(0x166));
        Assert.Equal(NativeBattleCurse47Io.ScriptAddress + 16, result.State.ReadDword(NativeBattleCurse47Io.CursorGlobal));
    }

    [Theory]
    [InlineData(51, 1u, -1)]
    [InlineData(52, 0u, 1)]
    [InlineData(52, 1u, -1)]
    [InlineData(52, 2u, -1)]
    [InlineData(32767, 0u, -1)]
    public void Busy_IO_pause_uses_the_post_observe_signed_age_and_exact_cached_boolean(ushort age, uint cached, int expectedOperation)
    {
        Fixture fixture = new(3);
        NativeBattleCurse47Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x46, age).WriteWord(NativeBattleCurse47Io.ControlAddress + 0x166, 1).WriteDword(0x264EAE4, cached).WriteDword(0x264D82C, 0xCAFE) };
        fixture.OnExecute = (current, operation) =>
        {
            Assert.Equal(new NativeBattleCurse47Pause(1), operation);
            Assert.Equal(1u, current.State.ReadDword(0x264EAE4));
            Assert.Equal(1u, current.State.ReadDword(0x264D82C));
            return current with { Random = new(1234) };
        };
        NativeBattleCurse47Frame result = fixture.Step(frame).Frame;
        Assert.Equal((byte)3, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(unchecked((ushort)(age + 1)), result.State.SharedWord(0x46));
        Assert.Equal(expectedOperation >= 0 ? 1 : 0, fixture.Operations.Count);
        Assert.Equal(expectedOperation >= 0 ? 1u : cached, result.State.ReadDword(0x264EAE4));
        Assert.Equal(expectedOperation >= 0 ? 1u : 0xCAFEu, result.State.ReadDword(0x264D82C));
    }

    [Fact]
    public void Finished_IO_releases_cached_pause_before_advancing_and_preserves_nonboolean_cache()
    {
        Fixture fixture = new(3);
        NativeBattleCurse47Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x264EAE4, 1).WriteDword(0x264D82C, 1) };
        NativeBattleCurse47Frame branch = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleCurse47Frame result = fixture.Step(frame).Frame;
        Assert.Equal(new NativeBattleCurse47Pause(0), Assert.Single(fixture.Operations));
        Assert.Equal(0u, result.State.ReadDword(0x264D82C));
        Assert.Equal((byte)4, fixture.Task(result).ReadByte(0x29));
        fixture.Operations.Clear();
        result = fixture.Step(branch with { State = branch.State.WriteDword(0x264EAE4, 2) }).Frame;
        Assert.Empty(fixture.Operations);
        Assert.Equal(1u, result.State.ReadDword(0x264D82C));
    }

    [Theory]
    [InlineData(4, 52, 4)]
    [InlineData(4, 53, 5)]
    [InlineData(7, 49, 7)]
    [InlineData(7, 50, 8)]
    [InlineData(7, 32767, 7)]
    public void Signed_timer_thresholds_use_the_actual_observer_increment_before_the_phase(byte phase, ushort age, byte after)
    {
        Fixture fixture = new(phase);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x46, age) }).Frame;
        Assert.Equal(after, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(phase == 7 && after == 8 ? (ushort)1 : (ushort)0, result.State.SharedWord(0x48));
    }

    [Theory]
    [InlineData(6, 1, 6)]
    [InlineData(6, 2, 7)]
    [InlineData(9, 2, 9)]
    [InlineData(9, 3, 10)]
    public void Later_observed_mode_gates_do_not_run_the_following_phase_in_the_same_pass(byte phase, ushort observed, byte after)
    {
        Fixture fixture = new(phase);
        NativeBattleCurse47Frame result = fixture.Step(fixture.Mode(fixture.Frame, observed)).Frame;
        Assert.Equal(after, fixture.Task(result).ReadByte(0x29));
        Assert.Equal((byte)0, fixture.Task(result).ReadByte(0x26));
    }

    [Fact]
    public void Terminal_child_wait_and_cold_next_preserve_actual_task_and_frame_owners()
    {
        Fixture fixture = new(10);
        NativeBattleCurse47Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x28, 1).WriteWord(0x24, 0xFFFF));
        NativeBattleCurse47Step waiting = fixture.Step(frame);
        Assert.Equal(0, waiting.Result);
        Assert.Equal((byte)11, fixture.Task(waiting.Frame).ReadByte(0x29));
        Assert.Equal((ushort)0, fixture.Task(waiting.Frame).ReadWord(0x24));
        NativeBattleCurse47Frame cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCurse47Frame>(SnapshotJson(waiting.Frame))!);
        Assert.Equal(SnapshotJson(fixture.Step(waiting.Frame)), SnapshotJson(fixture.Step(cold)));
        NativeBattleCurse47Step completed = fixture.Step(fixture.Put(waiting.Frame, fixture.Task(waiting.Frame).WriteByte(0x28, 0)));
        Assert.Equal(2, completed.Result);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(completed.Frame.Effect.Pools).Read(fixture.Parent).ReadByte(0x28));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Put(frame, fixture.Task(frame).WriteByte(0x29, 0xFF))));
    }

    private sealed class Fixture
    {
        public NativeBattleCurse47Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public List<NativeBattleCurse47Operation> Operations { get; } = [];
        public Func<NativeBattleCurse47Frame, NativeBattleCurse47Operation, NativeBattleCurse47Frame> OnExecute { get; set; } = (frame, _) => frame;
        public Fixture(byte phase)
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(0, 0x82A970, 100);
            Parent = parent.Address;
            NativeBattleEffectHierarchyAppend child = parent.Hierarchy.Append8DC540(2, NativeBattleCurse47Director.Callback, 72, Parent, (_, _, _) => new(0, 3));
            Address = child.Address;
            hierarchy = child.Hierarchy.Write(Address, child.Hierarchy.Read(Address).WriteByte(0x29, phase));
            NativeBattleOriginalEffectState effect = new(47, 47, new(47, 0x82A7F0, 0x82A7D0), null, null) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleCurse47IoTests.Initialized());
        }
        public NativeBattleModulePoolTask Task(NativeBattleCurse47Frame frame) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(Address);
        public NativeBattleCurse47Frame Put(NativeBattleCurse47Frame frame, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(Address, task).Pools } };
        public NativeBattleCurse47Frame Mode(NativeBattleCurse47Frame frame, ushort mode) => frame with { State = frame.State.WithSharedWord(0x40, mode).WithSharedWord(0x42, mode).WithSharedWord(0x44, mode) };
        public NativeBattleCurse47Step Step(NativeBattleCurse47Frame frame) => NativeBattleCurse47Director.Advance(frame, Address, new(0x64221, (current, operation) =>
        {
            Operations.Add(operation);
            return OnExecute(current, operation);
        }));
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
