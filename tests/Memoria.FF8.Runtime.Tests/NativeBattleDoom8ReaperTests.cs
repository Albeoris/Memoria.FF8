using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ReaperTests
{
    [Fact]
    public void Initializer_requires_its_real_model_service_without_fabricating_a_model_owner()
    {
        Fixture fixture = new(0);
        fixture.OnExecute = (frame, operation) => frame with { Random = new(123) };
        NativeBattleDoom8Frame result = NativeBattleDoom8Reaper.Initialize(fixture.Frame, fixture.Address, fixture.Inputs);
        NativeBattleDoom8ReaperInitializeModel operation = Assert.IsType<NativeBattleDoom8ReaperInitializeModel>(Assert.Single(fixture.Operations));
        Assert.Equal(new NativeBattleDoom8ReaperInitializeModel(fixture.Address, 0x1636CE8, 3, Fixture.ShadowColor), operation);
        Assert.Equal(new NativeCrtRandomState(123), result.Random);
        Assert.Equal((ushort)0, fixture.Task(result).ReadWord(0x24));
        fixture.OnExecute = (_, _) => throw new NotSupportedException("The actual model service is required.");
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Reaper.Initialize(fixture.Frame, fixture.Address, fixture.Inputs));
    }

    [Fact]
    public void Phase_zero_publishes_actual_scale_pointer_before_select_prepare_draw_and_age()
    {
        Fixture fixture = new(0);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(NativeBattleDoom8State.SharedAddress, 0x00220011).WriteDword(NativeBattleDoom8State.SharedAddress + 4, 0x00440033).WithSharedWord(0x4A, 0x678).WriteWord(0x2758568, 0x777) };
        fixture.OnExecute = (current, operation) =>
        {
            NativeBattleModulePoolTask task = fixture.Task(current);
            if (operation is NativeBattleDoom8ReaperSelect select)
            {
                Assert.Equal((short)0, select.Animation);
                Assert.Equal(fixture.Address + 0x114, task.ReadDword(0x60));
                Assert.Equal(new uint[] { 4096, 4096, 4096 }, new[] { task.ReadDword(0x114), task.ReadDword(0x118), task.ReadDword(0x11C) });
                return fixture.Write(current, task.WriteWord(0x128, 0xABCD));
            }
            if (operation is NativeBattleDoom8ReaperPrepareAppearance prepare)
            {
                Assert.Equal(new NativeBattleDoom8ReaperPrepareAppearance(0x2758510, 0x275ABF0, 0x275F9E0, fixture.Address + 0x30), prepare);
                Assert.Equal((ushort)0xABCD, task.ReadWord(0x128));
                return current with { State = current.State.Write(0x2758510, new byte[0x278]) };
            }
            if (operation is NativeBattleDoom8ReaperDrawModel)
            {
                Assert.Equal((byte)1, task.ReadByte(0x29));
                Assert.Equal((byte)8, task.ReadByte(0x26));
                Assert.Equal((ushort)0, task.ReadWord(0x24));
                Assert.Equal((ushort)0, current.State.ReadWord(0x2758568));
                Assert.Equal((ushort)256, current.State.ReadWord(0x275856A));
                return fixture.Write(current with { Random = new(456) }, task.WriteWord(0x24, 9));
            }
            throw new InvalidOperationException();
        };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleDoom8ReaperSelect>(operation), operation => Assert.IsType<NativeBattleDoom8ReaperPrepareAppearance>(operation), operation => Assert.IsType<NativeBattleDoom8ReaperDrawModel>(operation));
        Assert.Equal(0x00220011u, fixture.Task(result).ReadDword(0x4C));
        Assert.Equal(0x00440033u, fixture.Task(result).ReadDword(0x50));
        Assert.Equal((ushort)0x678, fixture.Task(result).ReadWord(0x3E));
        Assert.Equal((ushort)10, fixture.Task(result).ReadWord(0x24));
        Assert.Equal(new NativeCrtRandomState(456), result.Random);
    }

    [Theory]
    [InlineData(-1200, 1)]
    [InlineData(-1210, 2)]
    [InlineData(32767, 1)]
    [InlineData(-32768, 1)]
    public void Appearance_threshold_and_angle_use_wrapped_WORDs_and_the_signed_boundary(int old, byte phase)
    {
        Fixture fixture = new(1);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(0x2758564, unchecked((ushort)old)).WriteWord(0x2758568, 0xFFF).WriteWord(0x275856A, 256).WriteDword(0x275ABDC, 1) };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal(unchecked((ushort)(old - 72)), result.State.ReadWord(0x2758564));
        Assert.Equal((ushort)255, result.State.ReadWord(0x2758568));
        Assert.Equal(phase, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(phase == 2 ? (ushort)16 : (ushort)0, fixture.Task(result).ReadWord(0x134));
        Assert.IsType<NativeBattleDoom8ReaperDrawModel>(Assert.Single(fixture.Operations));
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(0, 3)]
    [InlineData(32768, 2)]
    [InlineData(32769, 3)]
    public void Appearance_delay_clears_only_bit8_after_signed_wrapped_decrement(int old, byte phase)
    {
        Fixture fixture = new(2);
        NativeBattleDoom8Frame frame = fixture.Write(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x134, (ushort)old).WriteByte(0x26, 0x88));
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal(unchecked((ushort)(old - 1)), fixture.Task(result).ReadWord(0x134));
        Assert.Equal(phase, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(phase == 3 ? (byte)0x80 : (byte)0x88, fixture.Task(result).ReadByte(0x26));
        Assert.IsType<NativeBattleDoom8ReaperDrawModel>(Assert.Single(fixture.Operations));
    }

    [Theory]
    [InlineData(false, 0u, 1)]
    [InlineData(false, 1u, -1)]
    [InlineData(false, 2u, -1)]
    [InlineData(true, 0u, -1)]
    [InlineData(true, 1u, 0)]
    [InlineData(true, 2u, -1)]
    public void Request_handshake_and_cached_pause_publish_before_service_and_never_gate_draw(bool succeeds, uint cached, int emitted)
    {
        Fixture fixture = new(3);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x40, succeeds ? (ushort)3 : (ushort)2).WithSharedWord(0x44, 3).WriteDword(0x275F9D4, cached).WriteDword(0x275ABDC, 0xBEEF) };
        fixture.OnExecute = (current, operation) =>
        {
            if (operation is NativeBattleDoom8ReaperPause5084E0 pause)
            {
                Assert.Equal(emitted, pause.Value);
                Assert.Equal((uint)pause.Value, current.State.ReadDword(0x275F9D4));
                Assert.Equal((uint)pause.Value, current.State.ReadDword(0x275ABDC));
                return current with { Random = new(789) };
            }
            return current;
        };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal(succeeds ? (byte)4 : (byte)3, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(succeeds ? (ushort)4 : (ushort)3, result.State.SharedWord(0x44));
        Assert.Equal(emitted >= 0 ? (uint)emitted : cached, result.State.ReadDword(0x275F9D4));
        Assert.Equal(emitted >= 0 ? (uint)emitted : 0xBEEFu, result.State.ReadDword(0x275ABDC));
        Assert.Equal(emitted >= 0 ? 1 : 0, fixture.Operations.OfType<NativeBattleDoom8ReaperPause5084E0>().Count());
        Assert.IsType<NativeBattleDoom8ReaperDrawModel>(fixture.Operations.Last());
        if (emitted >= 0)
        {
            Assert.Equal(new NativeCrtRandomState(789), result.Random);
        }
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    [InlineData(65535, 4)]
    public void Observed_mode_gate_is_signed_and_selects_real_animation_one(int observed, byte phase)
    {
        Fixture fixture = new(4);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WithSharedWord(0x40, (ushort)observed) };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal(phase, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(phase == 5 ? new short[] { 1 } : Array.Empty<short>(), fixture.Operations.OfType<NativeBattleDoom8ReaperSelect>().Select(operation => operation.Animation));
    }

    [Theory]
    [InlineData(5, -1)]
    [InlineData(5, 0)]
    [InlineData(5, 1)]
    [InlineData(5, 2)]
    [InlineData(7, 0)]
    [InlineData(7, 1)]
    [InlineData(7, 2)]
    public void Model_query_requires_exact_one_and_retains_frame_changes_before_select_and_draw(byte phase, int value)
    {
        Fixture fixture = new(phase);
        fixture.OnQuery = (current, address) => new(fixture.Write(current with { Random = new(99) }, fixture.Task(current).WriteWord(0x128, 0xCAFE)), value);
        NativeBattleDoom8Frame result = fixture.Step(fixture.Frame).Frame;
        Assert.Equal(value == 1 ? (byte)(phase + 1) : phase, fixture.Task(result).ReadByte(0x29));
        Assert.Equal((ushort)0xCAFE, fixture.Task(result).ReadWord(0x128));
        Assert.Equal(new NativeCrtRandomState(99), result.Random);
        Assert.Equal(value == 1 ? new short[] { phase == 5 ? (short)2 : (short)4 } : Array.Empty<short>(), fixture.Operations.OfType<NativeBattleDoom8ReaperSelect>().Select(operation => operation.Animation));
        Assert.Equal(phase == 7 && value == 1 ? (ushort)7 : (ushort)0, fixture.Task(result).ReadWord(0x134));
        Assert.Equal(1, fixture.Queries);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 7)]
    [InlineData(32768, 6)]
    public void Bone_observation_is_mandatory_before_loop_and_signed_loop_counter_test(int count, byte phase)
    {
        Fixture fixture = new(6);
        fixture.OnExecute = (current, operation) => operation is NativeBattleDoom8ReaperAdvanceLoop ? fixture.Write(current, fixture.Task(current).WriteWord(0x138, (ushort)count)) : current;
        NativeBattleDoom8Frame result = fixture.Step(fixture.Frame).Frame;
        Assert.Equal(new NativeBattleDoom8ReaperObserveBone502170(fixture.Address + 0x30, 0x1B, 0), fixture.Operations[0]);
        Assert.IsType<NativeBattleDoom8ReaperAdvanceLoop>(fixture.Operations[1]);
        Assert.Equal(phase, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(phase == 7 ? new short[] { 3 } : Array.Empty<short>(), fixture.Operations.OfType<NativeBattleDoom8ReaperSelect>().Select(operation => operation.Animation));
        Assert.IsType<NativeBattleDoom8ReaperDrawModel>(fixture.Operations.Last());
    }

    [Fact]
    public void Seven_tick_wait_does_not_query_animation_and_fade_uses_nonzero_secondary_until_terminal_draw_suppression()
    {
        Fixture fixture = new(8);
        NativeBattleDoom8Frame frame = fixture.Write(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x134, 7).WriteByte(0x5F, 0xAB));
        for (int index = 0; index < 7; index++)
        {
            frame = fixture.Step(frame).Frame;
            Assert.Equal(index == 6 ? (byte)9 : (byte)8, fixture.Task(frame).ReadByte(0x29));
        }
        Assert.Equal(0, fixture.Queries);
        fixture.Operations.Clear();
        for (int index = 1; index <= 16; index++)
        {
            NativeBattleDoom8Step step = fixture.Step(frame);
            frame = step.Frame;
            NativeBattleModulePoolTask task = fixture.Task(frame);
            Assert.Equal((ushort)(index * 256), task.ReadWord(0x13A));
            Assert.Equal(task.ReadWord(0x13A), task.ReadWord(0x13C));
            Assert.Equal((byte)2, task.ReadByte(0x5B));
            Assert.Equal((byte)0xAB, task.ReadByte(0x5F));
            Assert.Equal((byte)(64 - index * 4), task.ReadByte(0x37));
            Assert.Equal(index == 16 ? 2 : 0, step.Result);
        }
        Assert.Equal(15, fixture.Operations.OfType<NativeBattleDoom8ReaperDrawModel>().Count());
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Parent).ReadByte(0x28));
    }

    [Fact]
    public void Draw_mutations_child_wait_and_cold_next_are_retained_and_missing_draw_service_fails()
    {
        Fixture fixture = new(10);
        NativeBattleDoom8Frame frame = fixture.Write(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x26, 1).WriteByte(0x28, 1));
        NativeBattleDoom8Frame failureFrame = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleDoom8Step waiting = fixture.Step(frame);
        Assert.Equal(0, waiting.Result);
        Assert.IsType<NativeBattleDoom8ReaperDrawModel>(Assert.Single(fixture.Operations));
        NativeBattleDoom8Frame cold = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleDoom8Frame>(SnapshotJson(waiting.Frame))!);
        Assert.Equal(SnapshotJson(fixture.Step(waiting.Frame)), SnapshotJson(fixture.Step(cold)));
        fixture.OnExecute = (_, operation) => throw new NotSupportedException($"Required native draw {operation.GetType().Name}.");
        Assert.Throws<NotSupportedException>(() => fixture.Step(failureFrame));
        Assert.Equal((ushort)0, fixture.Task(failureFrame).ReadWord(0x24));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Write(failureFrame, fixture.Task(failureFrame).WriteByte(0x29, 255))));
    }

    private sealed class Fixture
    {
        public const uint DefaultColor = 0x00FFFFFF;
        public const uint ShadowColor = 0x00804020;
        public NativeBattleDoom8Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public List<NativeBattleDoom8Operation> Operations { get; } = [];
        public int Queries { get; private set; }
        public Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> OnExecute { get; set; } = (frame, _) => frame;
        public Func<NativeBattleDoom8Frame, uint, NativeBattleDoom8Step> OnQuery { get; set; } = (frame, _) => new(frame, 0);
        public NativeBattleDoom8ReaperInputs Inputs => new(DefaultColor, ShadowColor, (frame, address) =>
        {
            Queries++;
            return OnQuery(frame, address);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return OnExecute(frame, operation);
        });

        public Fixture(byte phase)
        {
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(2, NativeBattleDoom8Controller.DirectorCallback, 72);
            Parent = parent.Address;
            NativeBattleEffectHierarchyAppend child = parent.Hierarchy.Append8DC540(8, NativeBattleDoom8Reaper.Callback, 320, Parent, (_, _, _) => new(0, 4));
            Address = child.Address;
            hierarchy = child.Hierarchy.Write(Address, child.Hierarchy.Read(Address).WriteByte(0x29, phase));
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = hierarchy.Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()));
        }

        public NativeBattleModulePoolTask Task(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, Address);
        public NativeBattleDoom8Frame Write(NativeBattleDoom8Frame frame, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, Address, task);
        public NativeBattleDoom8Step Step(NativeBattleDoom8Frame frame) => NativeBattleDoom8Reaper.Advance(frame, Address, Inputs);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
