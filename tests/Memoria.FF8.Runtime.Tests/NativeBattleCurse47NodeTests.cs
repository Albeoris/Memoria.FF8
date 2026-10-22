using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurse47NodeTests
{
    [Fact]
    public void Addressed_program_materializes_once_and_retains_current_bytes_across_JSON()
    {
        NativeBattleCurse47State cold = NativeBattleCurse47State.FromColdProcess(new byte[80].ToImmutableArray());
        Assert.Equal(NativeBattleCurse47Nodes.SourceAddress, cold.NodeProgram!.ResourceAddress);
        Assert.Equal(0u, cold.NodeProgram.InitializationFlag);
        Assert.Null(cold.NodeProgram.Data);
        NativeBattleCuragaExecutableSource source = new(new byte[6544].ToImmutableArray()) { ResourceAddress = NativeBattleCurse47Nodes.SourceAddress };
        NativeBattleCuragaExecutableState bank = cold.NodeProgram.Initialize87EA20(source);
        byte[] current = bank.Data!.Value.ToArray();
        current[^1] = 0xA5;
        NativeBattleCurse47State state = cold with { NodeProgram = bank with { Data = current.ToImmutableArray() } };
        NativeBattleCurse47State restored = JsonSerializer.Deserialize<NativeBattleCurse47State>(JsonSerializer.Serialize(state))!;
        NativeBattleCuragaExecutableState warm = restored.NodeProgram!.Initialize87EA20(source);
        Assert.Equal(6544, warm.ByteLength);
        Assert.Equal(1u, warm.InitializationFlag);
        Assert.Equal((byte)0xA5, warm.Data!.Value[^1]);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        Assert.Null(NativeBattleCurse47State.FromUnknown().NodeProgram);
        Assert.Throws<NotSupportedException>(() => (new NativeBattleCuragaExecutableState(1, null) { ResourceAddress = NativeBattleCurse47Nodes.SourceAddress }).Initialize87EA20(source));
        Assert.Throws<InvalidDataException>(() => (cold with { NodeProgram = new(0, null) }).Validate());
    }

    [Fact]
    public void Constructor_uses_real_hierarchy_and_preserves_signed_operands()
    {
        Fixture fixture = new();
        NativeBattleCurse47NodeAppend appended = NativeBattleCurse47Nodes.Append82B5A0(fixture.Frame, fixture.Parent, NativeBattleCurse47Nodes.Callback, NativeBattleCurse47Nodes.SourceAddress, -2, short.MaxValue, 2, (pointer, group, target) =>
        {
            Assert.Equal(0u, pointer);
            Assert.Equal((sbyte)0, group);
            Assert.Equal((sbyte)0, target);
            return new(1, 4);
        });
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(appended.Frame, appended.Address);
        Assert.Equal(0x264DFE0u, appended.Address);
        Assert.Equal(NativeBattleCurse47Nodes.SourceAddress, task.ReadDword(0x30));
        Assert.Equal(unchecked((ushort)-2), task.ReadWord(0x298));
        Assert.Equal((ushort)short.MaxValue, task.ReadWord(0x29E));
        Assert.Equal((ushort)2, task.ReadWord(0x29C));
        Assert.Equal((byte)1, task.ReadByte(0x2C));
        Assert.Equal((byte)4, task.ReadByte(0x2D));
        Assert.Equal((byte)1, NativeBattleCurse47Controller.Read(appended.Frame, fixture.Parent).ReadByte(0x28));
        Assert.Throws<InvalidOperationException>(() => NativeBattleCurse47Nodes.Append82B5A0(appended.Frame, fixture.Parent, NativeBattleCurse47Nodes.Callback, NativeBattleCurse47Nodes.SourceAddress, 0, 0, 0, (_, _, _) => new(1, 4)));
    }

    [Fact]
    public void Signed_delay_increments_age_without_reading_unreached_source_or_models()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Append(delay: -1);
        frame = fixture.Write(frame, fixture.Task(frame).WriteWord(0x24, unchecked((ushort)-2)));
        frame = frame with { State = frame.State with { NodeProgram = null } };
        NativeBattleCurse47Step step = fixture.Step(frame);
        Assert.Equal(0, step.Result);
        Assert.Equal(unchecked((ushort)-1), fixture.Task(step.Frame).ReadWord(0x24));
        Assert.Equal((byte)0, fixture.Task(step.Frame).ReadByte(0x29));
        Assert.Null(step.Frame.State.NodeProgram);
        Assert.Equal(JsonSerializer.Serialize(frame.Random), JsonSerializer.Serialize(step.Frame.Random));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reached_initialization_requires_actual_program_without_recreating_a_fixed_bank(bool fixedFlag)
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Append();
        frame = frame with { State = frame.State with { NodeProgram = fixedFlag ? new(1, null) { ResourceAddress = NativeBattleCurse47Nodes.SourceAddress } : null } };
        Assert.Throws<NotSupportedException>(() => fixture.Step(frame));
    }

    [Fact]
    public void Context_initial_phase_runs_before_wrapper_age_and_does_not_draw_or_read_allocations()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Append();
        frame = fixture.Write(frame, fixture.Task(frame).WriteByte(0x29, 1).WriteWord(0x34 + 0x20, 0));
        frame = frame with { State = NativeBattleCurse47State.FromUnknown().WriteWord(0x264EAC0, 7).WriteWord(0x264E704, 9) };
        NativeBattleCurse47Step step = fixture.Step(frame);
        Assert.Equal((ushort)1, fixture.Task(step.Frame).ReadWord(0x34 + 0x20));
        Assert.Equal((ushort)1, fixture.Task(step.Frame).ReadWord(0x24));
        Assert.Equal((ushort)7, step.Frame.State.ReadWord(0x264EAC0));
        Assert.Equal((ushort)9, step.Frame.State.ReadWord(0x264E704));
        Assert.Equal((byte)1, fixture.Task(step.Frame).ReadByte(0x29));
    }

    [Fact]
    public void Completion_waits_for_children_then_detaches_once_through_actual_parent()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Append();
        frame = fixture.Write(frame, fixture.Task(frame).WriteByte(0x29, 1).WriteWord(0x34 + 0x20, 2).WriteByte(0x28, 1));
        NativeBattleCurse47Step waiting = fixture.Step(frame);
        Assert.Equal(0, waiting.Result);
        Assert.Equal((byte)2, fixture.Task(waiting.Frame).ReadByte(0x29));
        Assert.Equal((byte)1, NativeBattleCurse47Controller.Read(waiting.Frame, fixture.Parent).ReadByte(0x28));
        frame = fixture.Write(waiting.Frame, fixture.Task(waiting.Frame).WriteByte(0x28, 0));
        NativeBattleCurse47Frame restored = JsonSerializer.Deserialize<NativeBattleCurse47Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleCurse47Step done = fixture.Step(frame);
        Assert.Equal(2, done.Result);
        Assert.Equal((byte)0, NativeBattleCurse47Controller.Read(done.Frame, fixture.Parent).ReadByte(0x28));
        Assert.Equal(JsonSerializer.Serialize(done), JsonSerializer.Serialize(fixture.Step(restored)));
    }

    [Fact]
    public void Invalid_signed_phase_and_unproved_placement_mode_fail_before_execution()
    {
        Fixture fixture = new();
        NativeBattleCurse47Frame frame = fixture.Append();
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Write(frame, fixture.Task(frame).WriteByte(0x29, 255))));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Write(frame, fixture.Task(frame).WriteWord(0x29C, 2))));
    }

    private sealed class Fixture
    {
        public NativeBattleCurse47Frame Frame { get; }
        public uint Parent { get; }
        private uint address;

        public Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleCurse47State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleCurse47Controller.RootCallback, 100);
            Parent = root.Address;
            NativeBattleOriginalEffectState effect = new(47, 47, new(47, NativeBattleCurse47Controller.Entry, 0x82A7D0), null, null) { Pools = root.Hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(17), NativeBattleCurse47State.FromColdProcess(new byte[80].ToImmutableArray()));
        }

        public NativeBattleCurse47Frame Append(short delay = 0)
        {
            NativeBattleCurse47NodeAppend appended = NativeBattleCurse47Nodes.Append82B5A0(Frame, Parent, NativeBattleCurse47Nodes.Callback, NativeBattleCurse47Nodes.SourceAddress, delay, 0, 0, (_, _, _) => new(0, 3));
            address = appended.Address;
            return appended.Frame;
        }

        public NativeBattleModulePoolTask Task(NativeBattleCurse47Frame frame) => NativeBattleCurse47Controller.Read(frame, address);
        public NativeBattleCurse47Frame Write(NativeBattleCurse47Frame frame, NativeBattleModulePoolTask task) => NativeBattleCurse47Controller.Write(frame, address, task);
        public NativeBattleCurse47Step Step(NativeBattleCurse47Frame frame) => NativeBattleCurse47Nodes.Advance82DF60(frame, address, new(new byte[NativeBattleCurse47Nodes.SourceLength].ToImmutableArray()) { ResourceAddress = NativeBattleCurse47Nodes.SourceAddress }, new(NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]), _ => throw new InvalidOperationException("Unreached model"), _ => throw new InvalidOperationException("Unreached pose"), new(null, 100, 160, 108), 0, (_, _) => throw new InvalidOperationException("Unreached draw")));
    }
}
