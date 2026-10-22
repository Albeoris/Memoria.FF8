using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ActTests
{
    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(12, false)]
    public void Primary_initializes_only_at_exact_age_eleven_and_draws_before_phase_and_age_advance(ushort age, bool starts)
    {
        Fixture fixture = new(NativeBattleDoom8Act.PrimaryCallback, 0);
        NativeBattleDoom8Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x24, age).WriteBytes(0x94, Enumerable.Repeat((byte)0xA5, 192).ToArray()));
        frame = frame with { State = frame.State.WriteDword(NativeBattleDoom8State.SharedAddress, 0x00200010).WriteDword(NativeBattleDoom8State.SharedAddress + 4, 0xABCD0030).WithSharedWord(0x4A, 0) };
        fixture.OnPrepare = (current, _) =>
        {
            NativeBattleModulePoolTask task = fixture.Task(current);
            Assert.Equal(age, task.ReadWord(0x24));
            Assert.Equal((byte)0, task.ReadByte(0x29));
            Assert.Equal((short)-1, unchecked((short)task.ReadWord(0x1C)));
            Assert.Equal((short)-1044, unchecked((short)task.ReadWord(0x1E)));
            Assert.Equal((short)-361, unchecked((short)task.ReadWord(0x20)));
            Assert.Equal((ushort)0xABCD, task.ReadWord(0x22));
            Assert.Equal((ushort)0xF00, task.ReadWord(0x60));
            Assert.Equal((ushort)0xFFF0, task.ReadWord(0x88));
            Assert.Equal(new uint[] { 4096, 4096, 4096 }, new[] { task.ReadDword(0x50), task.ReadDword(0x54), task.ReadDword(0x58) });
            Assert.Equal(0u, task.ReadDword(0x98));
            Assert.NotNull(current.Memory.GeometryRegisters);
            Assert.Null(current.Memory.GeometryRegisters!.Rotation20[18]);
            Assert.Null(current.Memory.GeometryRegisters.Rotation20[19]);
            Assert.Equal(new int?[] { 0, 0, 4096 }, current.Memory.ScriptedColorRegisters!.Ir);
            Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address + 0x20, 2)));
            Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(current.Staging.Read(NativeBattleTemporaryMemory.Address + 0x22, 2)));
            Assert.All(NativeBattleTemporaryMemory.ReadView(current.Staging, NativeBattleTemporaryMemory.Address, 36).ReadSnapshot(18, 2).Known, known => Assert.False(known));
            return new(current, Fixture.Context);
        };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal((ushort)(age + 1), fixture.Task(result).ReadWord(0x24));
        Assert.Equal(starts ? (byte)1 : (byte)0, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(starts ? 1 : 0, fixture.Preparations);
        Assert.Equal(starts ? 1 : 0, fixture.Emissions);
        Assert.Equal(starts ? 1u : 0xA5A5A5A5u, fixture.Task(result).ReadDword(0x98));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(65556)]
    public void Primary_spawns_child_on_LOWWORD_remaining_nineteen_with_current_packed_position(int duration)
    {
        Fixture fixture = new(NativeBattleDoom8Act.PrimaryCallback, 1, duration);
        fixture.OnEmit = (frame, _, _, _) => fixture.Put(frame, fixture.Task(frame).WriteDword(0x1C, 0x33441122).WriteDword(0x20, 0x77885566));
        NativeBattleDoom8Frame result = fixture.Step(fixture.Frame).Frame;
        NativeBattleModulePoolTask child = Assert.Single(result.Effect.Pools[4].Slots.Where(task => task.Callback == NativeBattleDoom8Act.MovingCallback && (task.Flags & 1) != 0));
        Assert.Equal(0x33441122u, child.ReadDword(0x1C));
        Assert.Equal(0x77885566u, child.ReadDword(0x20));
        Assert.Equal(Fixture.SourceAddress + 0x1000, child.ReadDword(0x74));
        Assert.Equal(0x8Cu, child.ReadDword(0x78));
        Assert.Equal((ushort)1, child.ReadWord(0x80));
        Assert.Equal((ushort)0, child.ReadWord(0x82));
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x28));
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x29));
    }

    [Theory]
    [InlineData(NativeBattleDoom8Act.PrimaryCallback, 2)]
    [InlineData(NativeBattleDoom8Act.AuxiliaryCallback, 1)]
    public void Primary_completion_tests_AX_while_auxiliary_tests_the_full_returned_DWORD(uint callback, byte phase)
    {
        Fixture fixture = new(callback, 1, 65537);
        NativeBattleDoom8Step result = fixture.Step(fixture.Frame);
        Assert.Equal(phase, fixture.Task(result.Frame).ReadByte(0x29));
        Assert.Equal(callback == NativeBattleDoom8Act.PrimaryCallback ? 2 : 0, result.Result);
        Assert.Equal(1, fixture.Emissions);
    }

    [Fact]
    public void Moving_initializer_selects_target_and_places_before_ACT_emission()
    {
        Fixture fixture = new(NativeBattleDoom8Act.MovingCallback, 0);
        NativeBattleDoom8Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x454, 7));
        fixture.OnExecute = (current, operation) =>
        {
            if (operation is NativeBattleDoom8ActInitializeTarget target)
            {
                Assert.Equal((byte)4, target.Actor);
                Assert.Equal(0u, fixture.Task(current).ReadDword(0x98));
                Assert.Equal(4096u, fixture.Task(current).ReadDword(0x58));
                return current with { Random = new(23) };
            }
            NativeBattleDoom8ActPlace place = Assert.IsType<NativeBattleDoom8ActPlace>(operation);
            Assert.Equal((short)7, place.Selector);
            Assert.Equal(new NativeCrtRandomState(23), current.Random);
            return fixture.Put(current, fixture.Task(current).WriteWord(0x1E, 987));
        };
        fixture.OnPrepare = (current, _) =>
        {
            Assert.Equal((ushort)987, fixture.Task(current).ReadWord(0x1E));
            return new(current, Fixture.Context);
        };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleDoom8ActInitializeTarget>(operation), operation => Assert.IsType<NativeBattleDoom8ActPlace>(operation));
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(1u, fixture.Task(result).ReadDword(0x98));
        Assert.Equal(1, fixture.Emissions);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(126, false)]
    [InlineData(127, true)]
    [InlineData(128, true)]
    public void Moving_selector_uses_signed_boundary_and_spawns_auxiliary_before_same_pass_place_ACT_and_strips(short selector, bool spawns)
    {
        Fixture fixture = new(NativeBattleDoom8Act.MovingCallback, 1);
        fixture.Selector = selector;
        NativeBattleDoom8Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x456, 0xFFFF).WriteDword(0x44C, 0x88776655).WriteDword(0x450, 0xCCBBAA99));
        frame = frame with { State = frame.State.WriteDword(0x275F548, 0x44332211).WriteDword(0x275F54C, 0x88776655) };
        fixture.OnExecute = (current, operation) =>
        {
            Assert.Equal((ushort)0, fixture.Task(current).ReadWord(0x456));
            Assert.Equal(spawns ? (short)17 : selector, Assert.IsType<NativeBattleDoom8ActPlace>(operation).Selector);
            Assert.Equal(spawns ? (byte)2 : (byte)1, fixture.Task(current).ReadByte(0x29));
            if (spawns)
            {
                Assert.Equal(0x88776655u, fixture.Task(current).ReadDword(0x1C));
                Assert.Equal(0xCCBBAA99u, fixture.Task(current).ReadDword(0x20));
                NativeBattleModulePoolTask child = Assert.Single(current.Effect.Pools[4].Slots.Where(task => task.Callback == NativeBattleDoom8Act.AuxiliaryCallback && (task.Flags & 1) != 0));
                Assert.Equal(0x44332211u, child.ReadDword(0x1C));
                Assert.Equal(0x88776655u, child.ReadDword(0x20));
                Assert.Equal(Fixture.SourceAddress + 0x2000, child.ReadDword(0x74));
                Assert.Equal(0x3B0u, child.ReadDword(0x78));
                Assert.Equal((ushort)2, child.ReadWord(0x80));
            }
            return current;
        };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal(new[] { "place", "prepare", "emit", "strips" }, fixture.Order);
        Assert.Equal(spawns ? (byte)2 : (byte)1, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(1, fixture.Emissions);
        Assert.Equal(1, fixture.Strips);
    }

    [Fact]
    public void Moving_animation_completion_runs_strips_then_waits_for_strip_completion_in_the_next_phase()
    {
        Fixture fixture = new(NativeBattleDoom8Act.MovingCallback, 2, 1);
        NativeBattleDoom8Step first = fixture.Step(fixture.Frame);
        Assert.Equal((byte)3, fixture.Task(first.Frame).ReadByte(0x29));
        Assert.Equal(0, first.Result);
        Assert.Equal((byte)0, fixture.Task(first.Frame).ReadByte(0x26));
        Assert.Equal(new[] { "prepare", "emit", "strips" }, fixture.Order);
        fixture.Order.Clear();
        NativeBattleDoom8Step second = fixture.Step(first.Frame);
        Assert.Equal(2, second.Result);
        Assert.Equal((byte)4, fixture.Task(second.Frame).ReadByte(0x29));
        Assert.Equal(new[] { "strips" }, fixture.Order);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(second.Frame.Effect.Pools).Read(fixture.Parent).ReadByte(0x28));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    public void Native_pause_freezes_workspace_but_still_prepares_and_emits_and_preserves_callback_changes(int pause, bool advances)
    {
        Fixture fixture = new(NativeBattleDoom8Act.AuxiliaryCallback, 1);
        NativeBattleDoom8Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x275ABDC, unchecked((uint)pause)) };
        fixture.OnEmit = (current, address, context, node) =>
        {
            Assert.Same(Fixture.Context, context);
            Assert.Equal(0u, fixture.Task(current).ReadDword(0x98));
            Assert.Equal((ushort)0, node.NodeIndex);
            return fixture.Put(current with { Random = new(777) }, fixture.Task(current).WriteDword(0x9C, 0xABCDEF12).WriteWord(0x128, 0xCAFE));
        };
        NativeBattleDoom8Frame result = fixture.Step(frame).Frame;
        Assert.Equal(advances ? 1u : 0u, fixture.Task(result).ReadDword(0x98));
        Assert.Equal(0xABCDEF12u, fixture.Task(result).ReadDword(0x9C));
        Assert.Equal((ushort)0xCAFE, fixture.Task(result).ReadWord(0x128));
        Assert.Equal(new NativeCrtRandomState(777), result.Random);
        Assert.Equal(1, fixture.Preparations);
        Assert.Equal(1, fixture.Emissions);
    }

    [Fact]
    public void Auxiliary_initializer_sets_only_required_scale_and_bias_then_uses_real_lifecycle()
    {
        Fixture fixture = new(NativeBattleDoom8Act.AuxiliaryCallback, 0);
        fixture.OnPrepare = (current, _) =>
        {
            Assert.Equal((ushort)0xFE00, fixture.Task(current).ReadWord(0x88));
            Assert.Equal(4096u, fixture.Task(current).ReadDword(0x50));
            Assert.Equal((byte)0, fixture.Task(current).ReadByte(0x29));
            return new(current, Fixture.Context);
        };
        NativeBattleDoom8Frame result = fixture.Step(fixture.Frame).Frame;
        Assert.Equal((byte)1, fixture.Task(result).ReadByte(0x29));
        Assert.Equal(1u, fixture.Task(result).ReadDword(0x98));
    }

    [Fact]
    public void Completed_ACT_still_prepares_without_emission_and_terminal_child_wait_is_preserved()
    {
        Fixture fixture = new(NativeBattleDoom8Act.PrimaryCallback, 1, 5);
        NativeBattleDoom8Frame frame = fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteDword(0x98, 5).WriteByte(0x28, 1));
        NativeBattleDoom8Step result = fixture.Step(frame);
        Assert.Equal(1, fixture.Preparations);
        Assert.Equal(0, fixture.Emissions);
        Assert.Equal((byte)2, fixture.Task(result.Frame).ReadByte(0x29));
        Assert.Equal((byte)1, fixture.Task(result.Frame).ReadByte(0x26));
        Assert.Equal(0, result.Result);
        NativeBattleDoom8Step terminal = fixture.Step(result.Frame);
        Assert.Equal(1, fixture.Preparations);
        Assert.Equal(0, terminal.Result);
        Assert.Equal((ushort)2, fixture.Task(terminal.Frame).ReadWord(0x24));
    }

    [Fact]
    public void Cold_task_workspace_continues_exactly_and_missing_draw_cannot_complete_a_live_ACT()
    {
        Fixture fixture = new(NativeBattleDoom8Act.AuxiliaryCallback, 1);
        NativeBattleDoom8Frame cold = JsonSerializer.Deserialize<NativeBattleDoom8Frame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(fixture.Step(fixture.Frame)), JsonSerializer.Serialize(fixture.Step(cold)));
        fixture.OnPrepare = (_, _) => throw new NotSupportedException("The actual ACT callback context is required.");
        Assert.Throws<NotSupportedException>(() => fixture.Step(fixture.Frame));
        fixture.OnPrepare = (frame, _) => new(frame, Fixture.Context);
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 0xFF))));
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Put(fixture.Frame, fixture.Task(fixture.Frame).WriteDword(0x78, 965))));
    }

    private sealed class Fixture
    {
        public const uint SourceAddress = 0x3000000;
        public static NativeBattleEffectArena Context { get; } = NativeBattleEffectArena.Clear().MarkUnknown(0, 92);
        public NativeBattleDoom8Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public NativeBattleGfAnimationSource Source { get; }
        public List<string> Order { get; } = [];
        public List<NativeBattleDoom8Operation> Operations { get; } = [];
        public int Preparations { get; private set; }
        public int Emissions { get; private set; }
        public int Strips { get; private set; }
        public short Selector { get; set; }
        public Func<NativeBattleDoom8Frame, uint, NativeBattleDoom8ActPreparation> OnPrepare { get; set; } = (frame, _) => new(frame, Context);
        public Func<NativeBattleDoom8Frame, uint, NativeBattleEffectArena, NativeBattleGfAnimationNode, NativeBattleDoom8Frame> OnEmit { get; set; } = (frame, _, _, _) => frame;
        public Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> OnExecute { get; set; } = (frame, _) => frame;
        public NativeBattleDoom8ActInputs Inputs => new((_, pointer) => Source with { Address = pointer }, (frame, address) =>
        {
            Preparations++;
            Order.Add("prepare");
            return OnPrepare(frame, address);
        }, (frame, address, context, node) =>
        {
            Emissions++;
            Order.Add("emit");
            return OnEmit(frame, address, context, node);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            Order.Add(operation is NativeBattleDoom8ActPlace ? "place" : "target");
            return OnExecute(frame, operation);
        }, frame =>
        {
            Strips++;
            Order.Add("strips");
            return new(frame, 0);
        }, index =>
        {
            Assert.Equal(0, index);
            return Selector;
        }, (_, _, _) => new(0, 4));

        public Fixture(uint callback, byte phase, int duration = 36)
        {
            byte[] bytes = new byte[64];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 0x20);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x20), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x24), duration);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x28), 0x10);
            Source = new(SourceAddress, bytes.ToImmutableArray());
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend parent = hierarchy.Append8DC540(2, NativeBattleDoom8Controller.DirectorCallback, 72);
            Parent = parent.Address;
            NativeBattleEffectHierarchyAppend child = parent.Hierarchy.Append8DC540(4, callback, 1112, Parent, (_, _, _) => new(0, 4));
            Address = child.Address;
            int size = callback == NativeBattleDoom8Act.PrimaryCallback ? 192 : callback == NativeBattleDoom8Act.MovingCallback ? 140 : 944;
            NativeBattleModulePoolTask task = child.Hierarchy.Read(Address).WriteByte(0x29, phase).WriteDword(0x74, SourceAddress).WriteDword(0x78, (uint)size);
            if (phase != 0)
            {
                NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[size].ToImmutableArray()).Initialize7016B0(Source, size);
                task = task.WriteBytes(0x94, workspace.Data.AsSpan());
            }
            hierarchy = child.Hierarchy.Write(Address, task);
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = hierarchy.Pools };
            NativeBattleDoom8State state = NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()).WriteDword(NativeBattleDoom8Io.ControlPointerGlobal, NativeBattleDoom8Io.ControlAddress).WriteDword(NativeBattleDoom8Io.ControlAddress + 0x128, SourceAddress + 0x1000).WriteDword(NativeBattleDoom8Io.ControlAddress + 0x12C, SourceAddress + 0x2000);
            Frame = new NativeBattleDoom8Frame(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        }

        public NativeBattleModulePoolTask Task(NativeBattleDoom8Frame frame) => NativeBattleDoom8Controller.Read(frame, Address);
        public NativeBattleDoom8Frame Put(NativeBattleDoom8Frame frame, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, Address, task);
        public NativeBattleDoom8Step Step(NativeBattleDoom8Frame frame) => NativeBattleDoom8Act.Advance(frame, Address, Inputs);
    }
}
