using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71ControllerTests
{
    [Fact]
    public void Entry_uses_executed_nonzero_source_and_registers_camera_before_TIM_without_resetting_source()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame result = Initialize(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [] } }, fixture.Resources, fixture.Inputs);
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleLaserEye71RegisterCamera>(operation), operation => Assert.Equal(new NativeBattleLaserEye71EnqueueTim(0x800), operation));
        Assert.Equal(new[] { 1, 1 }, fixture.OperationPoolCounts);
        Assert.Equal(ArenaAddress + 0x800u, result.State.ReadDword(InitializerGlobal));
        Assert.Equal(ArenaAddress + 0x800u + 2 * DrawBankLength, result.State.ReadDword(0x25FCDD0));
        Assert.Null(result.State.ExecutableBytes);
        Assert.Equal(new ushort[] { 100, 88, 676, 148, 96, 64 }, result.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal((byte)3, result.Effect.Pools[0].Slots[0].ReadByte(0x2D));
    }

    [Fact]
    public void Target_constructor_stores_delay18_duration45_mode0_and_two_real_children()
    {
        Fixture fixture = new();
        (NativeBattleLaserEye71Frame frame, uint target) = fixture.Child(TargetCallback, 1);
        NativeBattleLaserEye71TaskStep result = Advance(frame, target, fixture.Resources, fixture.Inputs);
        Assert.Equal(new[] { "target" }, fixture.Services);
        NativeBattleModulePoolTask node = result.Frame.Effect.Pools[2].Slots[0];
        Assert.Equal(NodeCallback, node.Callback);
        Assert.Equal(SourceAddress, node.ReadDword(0x30));
        Assert.Equal((ushort)18, node.ReadWord(0x298));
        Assert.Equal((ushort)45, node.ReadWord(0x29E));
        Assert.Equal((ushort)0, node.ReadWord(0x29C));
        Assert.Equal((byte)2, Read(result.Frame, target).ReadByte(0x28));
        Assert.Equal(BindingCallback, result.Frame.Effect.Pools[3].Slots[0].Callback);
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, true)]
    [InlineData(65535, false)]
    public void Target_contact_uses_signed_age_threshold_and_waits_for_actual_children(int age, bool contact)
    {
        Fixture fixture = new();
        (NativeBattleLaserEye71Frame frame, uint target) = fixture.Child(TargetCallback, 1);
        frame = Write(frame, target, Read(frame, target).WriteByte(0x29, 2).WriteWord(0x24, (ushort)age).WriteByte(0x28, 1));
        NativeBattleLaserEye71TaskStep result = Advance(frame, target, fixture.Resources, fixture.Inputs);
        if (contact)
        {
            Assert.Single(fixture.Operations.OfType<NativeBattleLaserEye71Contact>());
        }
        else
        {
            Assert.Empty(fixture.Operations.OfType<NativeBattleLaserEye71Contact>());
        }
        Assert.Equal(0, result.Result);
        Assert.Equal(unchecked((ushort)(age + 1)), Read(result.Frame, target).ReadWord(0x24));
    }

    [Fact]
    public void Binding_age14_sound_preserves_flags0_pan128_and_the_native_wrapper_volume()
    {
        Fixture fixture = new();
        (NativeBattleLaserEye71Frame frame, uint task) = fixture.Child(BindingCallback, 3);
        frame = Write(frame, task, Read(frame, task).WriteByte(0x29, 1).WriteWord(0x24, 14));
        NativeBattleLaserEye71TaskStep result = Advance(frame, task, fixture.Resources, fixture.Inputs);
        Assert.Equal(new NativeBattleLaserEye71Sound(fixture.Resources.Sound, 0, 128), Assert.Single(fixture.Operations));
        Assert.Equal((byte)2, Read(result.Frame, task).ReadByte(0x29));
    }

    [Fact]
    public void Binding_births_two_opposite_beams_and_preserves_hierarchical_wait()
    {
        Fixture fixture = new();
        (NativeBattleLaserEye71Frame frame, uint task) = fixture.Child(BindingCallback, 3);
        frame = Write(frame, task, Read(frame, task).WriteByte(0x29, 2).WriteWord(0x24, 18));
        NativeBattleLaserEye71TaskStep result = Advance(frame, task, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask[] children = result.Frame.Effect.Pools[3].Slots.Where(slot => slot.Callback == BeamCallback).ToArray();
        Assert.Equal(new ushort[] { 64, unchecked((ushort)-64) }, children.Select(slot => slot.ReadWord(0x90)));
        Assert.Equal((byte)2, Read(result.Frame, task).ReadByte(0x28));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Completing_beam_still_draws_emits_and_publishes_unknown_packed_W_before_finish()
    {
        Fixture fixture = new();
        (NativeBattleLaserEye71Frame frame, uint task) = fixture.Child(BeamCallback, 3);
        frame = Write(frame, task, Read(frame, task).WriteByte(0x29, 4).WriteWord(0x24, 16).WriteWord(0x8C, 2048).WriteDword(0x7C, 0x11223344).WriteWord(0x80, 123).MarkPayloadUnknown(0x82, 2));
        string json = JsonSerializer.Serialize(frame);
        NativeBattleLaserEye71TaskStep result = Advance(JsonSerializer.Deserialize<NativeBattleLaserEye71Frame>(json)!, task, fixture.Resources, fixture.Inputs);
        Assert.Equal(new[] { "prepare", "draw", "particles" }, fixture.Services);
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)17, Read(result.Frame, task).ReadWord(0x24));
        Assert.Equal(0x11223344u, Read(result.Frame, task).ReadDword(0x1C));
        Assert.Throws<NotSupportedException>(() => Read(result.Frame, task).ReadWord(0x22));
        Assert.Equal(3u, result.Frame.State.ReadDword(0x25FCDCC));
    }

    [Theory]
    [InlineData(0, 24576)]
    [InlineData(1, 0)]
    public void Root_uses_old_parity_and_the_selected_end_pointer_before_child_scans(int parity, int offset)
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, Fixture.Root, Read(fixture.Frame, Fixture.Root).WriteByte(0x29, 10).WriteWord(0x5C, (ushort)parity));
        NativeBattleLaserEye71TaskStep result = Advance(frame, Fixture.Root, fixture.Resources, fixture.Inputs);
        Assert.Equal(ArenaAddress + 0x800u + (uint)offset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal((uint)DrawBankLength, result.Frame.State.ReadDword(PacketEndGlobal) - result.Frame.State.ReadDword(CursorGlobal));
        Assert.Same(fixture.Camera, result.Frame.Memory.SharedEffectView2793E58);
        Assert.Equal((ushort)(parity + 1), Read(result.Frame, Fixture.Root).ReadWord(0x5C));
    }

    [Fact]
    public void Stage_WORD_addition_wraps_before_the_signed_completion_comparison()
    {
        Fixture fixture = new();
        (NativeBattleLaserEye71Frame frame, uint task) = fixture.Child(StageInCallback, 5);
        frame = Write(frame, task, Read(frame, task).WriteByte(0x29, 1).WriteWord(0x1C, 32700));
        NativeBattleLaserEye71TaskStep result = Advance(frame, task, fixture.Resources, fixture.Inputs);
        Assert.Equal(new NativeBattleLaserEye71Stage(unchecked((short)(32700 + 256)), false), Assert.Single(fixture.Operations));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Root_auxiliary_clear_is_bounded_and_stage_initialization_preserves_the_RGB_write_policy()
    {
        Fixture fixture = new();
        int begin = 0x800 + EmittersOffset;
        int end = 0x800 + WorkingBytes;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(begin, EmitterBytes + GeometryBytes).Write(begin - 1, new byte[] { 0xAA }).Write(end, new byte[] { 0xBB });
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } }, Fixture.Root, Read(fixture.Frame, Fixture.Root).WriteByte(0x29, 2));
        NativeBattleLaserEye71TaskStep result = Advance(frame, Fixture.Root, fixture.Resources, fixture.Inputs);
        Assert.Equal(new byte[EmitterBytes + GeometryBytes], result.Frame.Memory.Arena!.Read(begin, EmitterBytes + GeometryBytes));
        Assert.Equal((byte)0xAA, result.Frame.Memory.Arena.Read(begin - 1, 1)[0]);
        Assert.Equal((byte)0xBB, result.Frame.Memory.Arena.Read(end, 1)[0]);
        Assert.Equal(new NativeBattleLaserEye71Stage(0, true), Assert.Single(fixture.Operations));
        Assert.Equal(ArenaAddress + (uint)end, result.Frame.State.ReadDword(0x25FCDD0));
    }

    private sealed class Fixture
    {
        public const uint Root = 0x25FA430;
        public NativeBattleLaserEye71Frame Frame { get; }
        public List<NativeBattleLaserEye71Operation> Operations { get; } = [];
        public List<string> Services { get; } = [];
        public List<int> OperationPoolCounts { get; } = [];
        public NativeBattlePackedMatrix32 Camera { get; } = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 1, 2, 3, 4096), null);
        public NativeBattleLaserEye71Resources Resources { get; } = CreateResources();
        public NativeBattleLaserEye71Inputs Inputs { get; }
        public Fixture()
        {
            byte[] header = new byte[20];
            header[16] = 1;
            NativeBattleContactHalf half = new(3, 0, 0, 0, 0, 7, 0);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            packets = packets with { Headers = packets.Headers.SetItem(0, new(header.ToImmutableArray(), 0, 0, null)), Contacts = packets.Contacts.SetItem(0, new(half, half)) };
            Inputs = new(packets, (_, _, _) => new(0, 3), _ => Camera, Execute)
            {
                RefreshRoot = (frame, _) => frame,
                PrepareTarget = (frame, _) => Service(frame, "target"),
                AdvanceNodes = (_, _) => throw new NotSupportedException(),
                PrepareBeam148 = (frame, _) => Service(frame, "prepare"),
                DrawBeam148 = (frame, _) => Service(frame, "draw"),
                EmitBeamParticles = (frame, _) => Service(frame, "particles"),
                AdvanceParticles96 = (_, _) => throw new NotSupportedException()
            };
            NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, RootCallback, 100);
            NativeBattleOriginalEffectState effect = new(71, 71, new(71, Entry, Initializer), new(0, 0, 0, 0, 0, 0), Pools[0].Descriptor) { Pools = appended.Hierarchy.Pools, ResourcePointers = [new(InitializerGlobal, 0x800)] };
            uint start = ArenaAddress + 0x800;
            NativeBattleLaserEye71State state = NativeBattleLaserEye71State.FromColdProcess().WriteDword(0x25FCD84, start).WriteDword(0x25FCD80, start + DrawBankLength).WriteDword(0x25FC17C, start + DrawBankLength).WriteDword(0x25FC178, start + 2 * DrawBankLength).WriteDword(0x25FCDD0, start + 2 * DrawBankLength);
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state);
        }
        public (NativeBattleLaserEye71Frame Frame, uint Address) Child(uint callback, int pool) => Append(Frame, pool, callback, Root, Inputs);
        private NativeBattleLaserEye71Frame Execute(NativeBattleLaserEye71Frame frame, NativeBattleLaserEye71Operation operation)
        {
            Operations.Add(operation);
            OperationPoolCounts.Add(frame.Effect.Pools.Length);
            return frame;
        }
        private NativeBattleLaserEye71Frame Service(NativeBattleLaserEye71Frame frame, string name)
        {
            Services.Add(name);
            return frame with { State = frame.State.WriteDword(0x25FCDCC, frame.State.ReadDword(0x25FCDCC) + 1) };
        }
        private static NativeBattleLaserEye71Resources CreateResources()
        {
            byte[] camera = new byte[340];
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6, 2), 340);
            byte[] dat = new byte[0x1B74];
            int[] directory = [3, 0x14, 0x1648, 0x1698, 0x1B74];
            for (int index = 0; index < directory.Length; index++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(dat.AsSpan(index * 4, 4), directory[index]);
            }
            byte[] sprite = new byte[196];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8, 2), 7);
            for (int index = 0; index < 8; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2, 2), 26);
            }
            return new(new("FF8_EN:158F534", camera.ToImmutableArray()), new(new byte[SourceLength].ToImmutableArray()) { ResourceAddress = SourceAddress }, new("FF8_EN:1590024", dat.ToImmutableArray()), [new(0, 0x158F688, new byte[1816].ToImmutableArray()), new(0, 0x158FDA0, new byte[168].ToImmutableArray()), new(0, 0x158FE48, new byte[280].ToImmutableArray())], new(0, 0x158FF60, 0, 0, sprite.ToImmutableArray()), Enumerable.Repeat(new NativeBattleLaserEye71EmissionRow(0, 1, 0, 0), 19).ToImmutableArray(), 123);
        }
    }
}
