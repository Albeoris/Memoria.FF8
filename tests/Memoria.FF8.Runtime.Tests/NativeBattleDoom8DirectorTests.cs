using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8DirectorTests
{
    [Fact]
    public void Mode_request_is_pending_one_callback_before_observation_and_services_see_published_mode()
    {
        Fixture fixture = new(1);
        NativeBattleDoom8Frame frame = fixture.Step(fixture.Frame).Frame;
        Assert.Equal((ushort)1, frame.State.SharedWord(0x44));
        Assert.Equal((ushort)0, frame.State.SharedWord(0x42));
        frame = fixture.Step(frame).Frame;
        Assert.Equal((ushort)1, frame.State.SharedWord(0x42));
        Assert.Equal((ushort)0, frame.State.SharedWord(0x40));
        Assert.Empty(fixture.Operations);
        fixture.OnExecute = (current, operation) =>
        {
            if (operation is NativeBattleDoom8ModeChanged change)
            {
                Assert.Equal((short)1, change.Mode);
                Assert.Equal((ushort)1, current.State.SharedWord(0x40));
                Assert.Equal((ushort)0, current.State.SharedWord(0x46));
                return current with { State = current.State.WithSharedWord(0x44, 2) };
            }
            return current;
        };
        frame = fixture.Step(frame).Frame;
        Assert.Equal((ushort)2, frame.State.SharedWord(0x42));
        Assert.Equal((byte)3, fixture.Task(frame).ReadByte(0x29));
        NativeBattleDoom8Sound sound = Assert.Single(fixture.Operations.OfType<NativeBattleDoom8Sound>());
        Assert.Equal(new NativeBattleModuleSound(0x494A2, 0, null) { Pan = 128, RawPan = 128, Volume = 127 }, sound.Sound);
    }

    [Fact]
    public void Phase_zero_upload_order_and_callback_mutations_preserve_actual_next_link()
    {
        Fixture fixture = new(0);
        fixture.OnExecute = (frame, _) =>
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            if (hierarchy.Pools[2].Slots.Count(task => (task.Flags & 1) != 0) == 1)
            {
                NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(2, 0x8C5350, 72);
                return frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Pools } };
            }
            return frame;
        };
        NativeBattleDoom8Frame frame = fixture.Step(fixture.Frame).Frame;
        Assert.Equal(new uint[] { 0x181CFAC, 0x18253CC }, fixture.Operations.OfType<NativeBattleDoom8UploadTim>().Select(upload => upload.Address));
        Assert.Equal((ushort)12, fixture.Task(frame).ReadWord(0x44));
        Assert.Equal((byte)1, fixture.Task(frame).ReadByte(0x29));
        Assert.NotNull(fixture.Task(frame).Next);
        Assert.Equal((ushort)1, fixture.Task(frame).ReadWord(0x24));
    }

    [Fact]
    public void Mode_two_requires_a_real_service_and_does_not_fabricate_child_completion()
    {
        Fixture fixture = new(7);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x42, 2).WithSharedWord(0x44, 2) };
        fixture.OnExecute = (_, operation) => throw new NotSupportedException($"Required service {operation.GetType().Name}");
        Assert.Throws<NotSupportedException>(() => fixture.Step(frame));
        Assert.Equal((ushort)0, frame.State.SharedWord(0x40));
        fixture.OnExecute = (current, _) => current;
        frame = fixture.Step(frame).Frame;
        Assert.Equal((byte)7, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal((ushort)2, frame.State.SharedWord(0x40));
    }

    [Fact]
    public void IO_release_publishes_cursor_once_then_waits_for_real_FF_gate_clear()
    {
        Fixture fixture = new(3);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(NativeBattleDoom8Io.NextAddressGlobal, 0x20FFAB8).InitializeIo() };
        frame = fixture.Step(frame).Frame;
        Assert.Equal((byte)4, fixture.Task(frame).ReadByte(0x29));
        Assert.Equal(0x1636C88u, frame.State.ReadDword(NativeBattleDoom8Io.CursorGlobal));
        Assert.Equal((ushort)1, frame.State.ReadIoControlWord(0x166));
        frame = NativeBattleDoom8Controller.Write(frame, fixture.Address, fixture.Task(frame).WriteByte(0x29, 5));
        Assert.Equal((byte)5, fixture.Task(fixture.Step(frame).Frame).ReadByte(0x29));
        frame = frame with { State = frame.State.WriteWord(NativeBattleDoom8Io.ControlAddress + 0x166, 0) };
        Assert.Equal((byte)6, fixture.Task(fixture.Step(frame).Frame).ReadByte(0x29));
    }

    [Theory]
    [InlineData(44, 8)]
    [InlineData(45, 9)]
    [InlineData(32767, 8)]
    [InlineData(65535, 8)]
    public void Elapsed_gate_is_signed_strict_45_after_wrapped_increment(int elapsed, byte expectedPhase)
    {
        Fixture fixture = new(8);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x46, (ushort)elapsed) };
        Assert.Equal(expectedPhase, fixture.Task(fixture.Step(frame).Frame).ReadByte(0x29));
    }

    [Fact]
    public void Countdown_contact_request_and_completion_preserve_live_child_wait_and_snapshot()
    {
        Fixture fixture = new(10);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x40, 5).WithSharedWord(0x42, 5).WithSharedWord(0x44, 5) };
        frame = fixture.Step(frame).Frame;
        Assert.Equal((ushort)1, fixture.Task(frame).ReadWord(0x44));
        frame = fixture.Step(frame).Frame;
        Assert.Equal((byte)12, fixture.Task(frame).ReadByte(0x29));
        NativeBattleDoom8State restored = JsonSerializer.Deserialize<NativeBattleDoom8State>(JsonSerializer.Serialize(frame.State))!;
        frame = frame with { State = restored };
        frame = fixture.Step(frame).Frame;
        Assert.Equal((ushort)1, frame.State.SharedWord(0x48));
        Assert.Equal((ushort)5, frame.State.SharedWord(0x40));
        Assert.Equal((ushort)6, frame.State.SharedWord(0x44));
        frame = NativeBattleDoom8Controller.Write(frame, fixture.Address, fixture.Task(frame).WriteByte(0x28, 1));
        NativeBattleDoom8Step waiting = fixture.Step(frame);
        Assert.Equal(0, waiting.Result);
        Assert.Equal((byte)14, fixture.Task(waiting.Frame).ReadByte(0x29));
        frame = NativeBattleDoom8Controller.Write(waiting.Frame, fixture.Address, fixture.Task(waiting.Frame).WriteByte(0x28, 0));
        NativeBattleDoom8Step done = fixture.Step(frame);
        Assert.Equal(2, done.Result);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(done.Frame.Effect.Pools).Read(fixture.Parent).ReadByte(0x28));
    }

    [Fact]
    public void Invalid_signed_phase_and_unexecuted_IO_owner_fail_fast()
    {
        Fixture fixture = new(255);
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Frame));
        fixture = new(5);
        Assert.Throws<NotSupportedException>(() => fixture.Step(fixture.Frame));
    }

    private sealed class Fixture
    {
        public NativeBattleDoom8Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public List<NativeBattleDoom8Operation> Operations { get; } = [];
        public Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> OnExecute { get; set; } = (frame, _) => frame;

        public Fixture(byte phase)
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8C4B30, 100);
            Parent = root.Address;
            NativeBattleEffectHierarchyAppend director = root.Hierarchy.Append8DC540(2, NativeBattleDoom8Controller.DirectorCallback, 72, Parent, (_, _, _) => new(0, 4));
            Address = director.Address;
            hierarchy = director.Hierarchy.Write(Address, director.Hierarchy.Read(Address).WriteByte(0x29, phase));
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()));
        }

        public NativeBattleModulePoolTask Task(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, Address);

        public NativeBattleDoom8Step Step(NativeBattleDoom8Frame frame)
        {
            return NativeBattleDoom8Director.Advance(frame, Address, new(0x494A2, (current, operation) =>
            {
                Operations.Add(operation);
                return OnExecute(current, operation);
            }));
        }
    }
}
