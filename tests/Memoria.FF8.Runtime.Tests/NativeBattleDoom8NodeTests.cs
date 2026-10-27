using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8NodeTests
{
    [Fact]
    public void Constructor_uses_real_hierarchy_and_preserves_signed_operands()
    {
        Fixture fixture = new();
        NativeBattleDoom8NodeAppend appended = NativeBattleDoom8Nodes.Append8C5810(fixture.Frame, fixture.Parent, NativeBattleDoom8Nodes.Callback, NativeBattleDoom8Nodes.SourceAddress, -2, short.MaxValue, 2, (pointer, group, target) =>
        {
            Assert.Equal(0u, pointer);
            Assert.Equal((sbyte)0, group);
            Assert.Equal((sbyte)0, target);
            return new(1, 4);
        });
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(appended.Frame, appended.Address);
        Assert.Equal(0x275DF50u, appended.Address);
        Assert.Equal(NativeBattleDoom8Nodes.SourceAddress, task.ReadDword(0x30));
        Assert.Equal(unchecked((ushort)-2), task.ReadWord(0x298));
        Assert.Equal((ushort)short.MaxValue, task.ReadWord(0x29E));
        Assert.Equal((ushort)2, task.ReadWord(0x29C));
        Assert.Equal((byte)1, task.ReadByte(0x2C));
        Assert.Equal((byte)4, task.ReadByte(0x2D));
        Assert.Equal((byte)1, NativeBattleDoom8Controller.Read(appended.Frame, fixture.Parent).ReadByte(0x28));
        Assert.Throws<InvalidOperationException>(() => NativeBattleDoom8Nodes.Append8C5810(appended.Frame, fixture.Parent, NativeBattleDoom8Nodes.Callback, NativeBattleDoom8Nodes.SourceAddress, 0, 45, 2, (_, _, _) => new(1, 4)));
    }

    [Fact]
    public void Signed_delay_increments_age_without_reading_unreached_source_or_models()
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = fixture.Append(delay: -1);
        frame = fixture.Write(frame, fixture.Task(frame).WriteWord(0x24, unchecked((ushort)-2)));
        frame = frame with { State = frame.State with { NodeProgram = null } };
        NativeBattleDoom8Step step = fixture.Step(frame);
        Assert.Equal(0, step.Result);
        Assert.Equal(unchecked((ushort)-1), fixture.Task(step.Frame).ReadWord(0x24));
        Assert.Equal((byte)0, fixture.Task(step.Frame).ReadByte(0x29));
        Assert.Null(step.Frame.State.NodeProgram);
        Assert.Equal(SnapshotJson(frame.Random), SnapshotJson(step.Frame.Random));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reached_initialization_requires_actual_program_without_recreating_a_fixed_bank(bool fixedFlag)
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = fixture.Append();
        frame = frame with { State = frame.State with { NodeProgram = fixedFlag ? new(1, null) { ResourceAddress = NativeBattleDoom8Nodes.SourceAddress } : null } };
        Assert.Throws<NotSupportedException>(() => fixture.Step(frame));
    }

    [Fact]
    public void Context_initial_phase_runs_before_wrapper_age_and_does_not_draw_or_read_allocations()
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = fixture.Append();
        frame = fixture.Write(frame, fixture.Task(frame).WriteByte(0x29, 1).WriteWord(0x34 + 0x20, 0));
        frame = frame with { State = NativeBattleDoom8State.FromUnknown().WriteWord(0x275F910, 7).WriteWord(0x275F3E8, 9) };
        NativeBattleDoom8Step step = fixture.Step(frame);
        Assert.Equal((ushort)1, fixture.Task(step.Frame).ReadWord(0x34 + 0x20));
        Assert.Equal((ushort)1, fixture.Task(step.Frame).ReadWord(0x24));
        Assert.Equal((ushort)7, step.Frame.State.ReadWord(0x275F910));
        Assert.Equal((ushort)9, step.Frame.State.ReadWord(0x275F3E8));
        Assert.Equal((byte)1, fixture.Task(step.Frame).ReadByte(0x29));
    }

    [Fact]
    public void Completion_waits_for_children_then_detaches_once_through_actual_parent()
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = fixture.Append();
        frame = fixture.Write(frame, fixture.Task(frame).WriteByte(0x29, 1).WriteWord(0x34 + 0x20, 2).WriteByte(0x28, 1));
        NativeBattleDoom8Step waiting = fixture.Step(frame);
        Assert.Equal(0, waiting.Result);
        Assert.Equal((byte)2, fixture.Task(waiting.Frame).ReadByte(0x29));
        Assert.Equal((byte)1, NativeBattleDoom8Controller.Read(waiting.Frame, fixture.Parent).ReadByte(0x28));
        frame = fixture.Write(waiting.Frame, fixture.Task(waiting.Frame).WriteByte(0x28, 0));
        NativeBattleDoom8Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleDoom8Frame>(SnapshotJson(frame))!);
        NativeBattleDoom8Step done = fixture.Step(frame);
        Assert.Equal(2, done.Result);
        Assert.Equal((byte)0, NativeBattleDoom8Controller.Read(done.Frame, fixture.Parent).ReadByte(0x28));
        Assert.Equal(SnapshotJson(done), SnapshotJson(fixture.Step(restored)));
    }

    [Fact]
    public void Invalid_signed_phase_and_unproved_placement_mode_fail_before_execution()
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = fixture.Append();
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Write(frame, fixture.Task(frame).WriteByte(0x29, 255))));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Write(frame, fixture.Task(frame).WriteWord(0x29C, 0))));
    }

    private sealed class Fixture
    {
        public NativeBattleDoom8Frame Frame { get; }
        public uint Parent { get; }
        private uint address;

        public Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8C4B30, 100);
            Parent = root.Address;
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = root.Hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(17), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()));
        }

        public NativeBattleDoom8Frame Append(short delay = 0)
        {
            NativeBattleDoom8NodeAppend appended = NativeBattleDoom8Nodes.Append8C5810(Frame, Parent, NativeBattleDoom8Nodes.Callback, NativeBattleDoom8Nodes.SourceAddress, delay, 45, 2, (_, _, _) => new(0, 3));
            address = appended.Address;
            return appended.Frame;
        }

        public NativeBattleModulePoolTask Task(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, address);
        public NativeBattleDoom8Frame Write(NativeBattleDoom8Frame frame, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, address, task);
        public NativeBattleDoom8Step Step(NativeBattleDoom8Frame frame) => NativeBattleDoom8Nodes.Advance8C88D0(frame, address, new(new byte[NativeBattleDoom8Nodes.SourceLength].ToImmutableArray()) { ResourceAddress = NativeBattleDoom8Nodes.SourceAddress }, new(NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]), _ => throw new InvalidOperationException("Unreached model"), _ => throw new InvalidOperationException("Unreached pose"), new(null, 100, 160, 108), 0, new(0, 0, 1f / 4096, BitConverter.Int64BitsToDouble(0x4330000080000000L)), (_, _) => throw new InvalidOperationException("Unreached draw")));
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
