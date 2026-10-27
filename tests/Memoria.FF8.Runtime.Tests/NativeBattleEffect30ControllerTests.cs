using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect30Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect30ControllerTests
{
    [Fact]
    public void Entry_registers_camera_before_TIM_and_preserves_the_executed_source_pointer()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame result = Initialize(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [] } }, fixture.Resources, fixture.Inputs);
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleEffect30RegisterCamera>(operation), operation => Assert.Equal(new NativeBattleEffect30EnqueueTim(0x800), operation));
        Assert.Equal(new ushort[] { 100, 88, 676, 144, 96, 64 }, result.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(ArenaAddress + 0x800u, result.State.ReadDword(InitializerGlobal));
        Assert.Equal(ArenaAddress + 0x800u + 2 * DrawStep, result.State.ReadDword(0x26D6A34));
        Assert.Null(result.State.ExecutableBytes);
    }

    [Fact]
    public void Target_births_delay_zero_nodes_and_one_144_byte_beam_then_publishes_sound_once()
    {
        Fixture fixture = new();
        (NativeBattleEffect30Frame frame, uint address) = fixture.Child();
        NativeBattleEffect30TaskStep result = Advance(frame, address, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask nodes = result.Frame.Effect.Pools[2].Slots[0];
        Assert.Equal(NodeCallback, nodes.Callback);
        Assert.Equal(SourceAddress, nodes.ReadDword(0x30));
        Assert.Equal((ushort)0, nodes.ReadWord(0x298));
        Assert.Equal((ushort)45, nodes.ReadWord(0x29E));
        Assert.Equal((ushort)0, nodes.ReadWord(0x29C));
        Assert.Equal(BeamCallback, result.Frame.Effect.Pools[3].Slots[0].Callback);
        Assert.Equal((byte)2, Read(result.Frame, address).ReadByte(0x28));
        Assert.Equal(new NativeBattleEffect30Sound(fixture.Resources.Sound, 0, 128), Assert.Single(fixture.Operations));
        Advance(result.Frame, address, fixture.Resources, fixture.Inputs);
        Assert.Single(fixture.Operations);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(65535, false)]
    public void Contact_uses_the_signed_age_and_retains_parent_ownership_while_children_are_live(int age, bool expected)
    {
        Fixture fixture = new();
        (NativeBattleEffect30Frame frame, uint address) = fixture.Child();
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 2).WriteWord(0x24, (ushort)age).WriteByte(0x28, 1));
        NativeBattleEffect30TaskStep result = Advance(frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(expected ? 1 : 0, fixture.Operations.OfType<NativeBattleEffect30Contact>().Count());
        Assert.Equal(0, result.Result);
        Assert.Equal(unchecked((ushort)(age + 1)), Read(result.Frame, address).ReadWord(0x24));
    }

    [Fact]
    public void Root_clears_only_its_ten_emitter_and_fifty_geometry_records()
    {
        Fixture fixture = new();
        int first = 0x800 + 2 * DrawStep;
        int end = first + EmitterBytes + GeometryBytes;
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!.MarkUnknown(first, EmitterBytes + GeometryBytes).Write(first - 1, new byte[] { 0xAA }).Write(end, new byte[] { 0xBB });
        NativeBattleEffect30Frame frame = Write(fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } }, Pools[0].Bank, Read(fixture.Frame, Pools[0].Bank).WriteByte(0x29, 2));
        NativeBattleEffect30TaskStep result = Advance(frame, Pools[0].Bank, fixture.Resources, fixture.Inputs);
        Assert.Equal(new byte[EmitterBytes + GeometryBytes], result.Frame.Memory.Arena!.Read(first, EmitterBytes + GeometryBytes));
        Assert.Equal(new byte[] { 0xAA }, result.Frame.Memory.Arena.Read(first - 1, 1));
        Assert.Equal(new byte[] { 0xBB }, result.Frame.Memory.Arena.Read(end, 1));
        Assert.Equal(ArenaAddress + (uint)end, result.Frame.State.ReadDword(0x26D6A34));
        Assert.Equal(new NativeBattleEffect30Stage(0, true), Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Stage_entry_clears_RGB_then_clamps_the_four_step_depth_ramp()
    {
        Fixture fixture = new();
        (NativeBattleEffect30Frame frame, uint address) = Append(fixture.Frame, 5, FadeInCallback, Pools[0].Bank, fixture.Inputs);
        frame = Advance(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(new NativeBattleEffect30Stage(0, true), Assert.Single(fixture.Operations));
        NativeBattleEffect30TaskStep step = new(frame, 0);
        for (int index = 0; index < 4; index++)
        {
            step = Advance(frame, address, fixture.Resources, fixture.Inputs);
            frame = step.Frame;
        }
        Assert.Equal(new NativeBattleEffect30Stage(1024, false), fixture.Operations[^1]);
        Assert.Equal(5, fixture.Operations.Count);
        Assert.Equal(2, step.Result);
        Assert.Equal((byte)0, Read(frame, Pools[0].Bank).ReadByte(0x28));
        Assert.Equal(FadeInCallback, Read(frame, address).Callback);
    }

    [Theory]
    [InlineData(0, 0x6800)]
    [InlineData(1, 0x800)]
    public void Root_publishes_the_executed_double_buffer_as_the_physical_draw_owner(int parity, int expected)
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, Pools[0].Bank, Read(fixture.Frame, Pools[0].Bank).WriteByte(0x29, 10).WriteWord(0x5C, (ushort)parity));
        NativeBattleEffect30TaskStep result = Advance(frame, Pools[0].Bank, fixture.Resources, fixture.Inputs);
        Assert.Equal(expected, result.Frame.Effect.DrawArenaOffset);
        Assert.Equal(ArenaAddress + (uint)expected, result.Frame.State.ReadDword(CursorGlobal));
    }

    [Fact]
    public void Missing_warm_source_is_not_recreated_from_original_assets()
    {
        Fixture fixture = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect30State.FromUnknown().InitializeSource(fixture.Resources));
        NativeBattleEffect30State initialized = NativeBattleEffect30State.FromColdProcess().InitializeSource(fixture.Resources);
        Assert.Equal(1u, initialized.ExecutableBytes!.ReadUInt32(0));
        NativeBattleEffect30State warm = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(initialized));
        warm.ExecutableBytes!.Arena!.Write(0x200, [0x77]);
        Assert.Equal((byte)0x77, warm.InitializeSource(fixture.Resources).ExecutableBytes!.ReadByte(0x200));
        Assert.False(initialized.ColdSourcePending);
    }

    [Fact]
    public void Prepared_fixups_remain_private_until_context_initialization_accepts_them()
    {
        Fixture fixture = new();
        NativeBattleNodeSource bytes = new(fixture.Resources.Executable.Data);
        NativeBattleEffect30State state = NativeBattleEffect30State.FromColdProcess() with { ExecutableBytes = bytes, ColdSourcePending = false };
        uint directory = bytes.ReadUInt32(4);
        using (NativeBattleNodeInitialization<NativeBattleEffect30State> abandoned = state.PrepareInitializeSource(fixture.Resources))
        {
            Assert.Equal(0u, bytes.ReadUInt32(0));
            Assert.Equal(directory, bytes.ReadUInt32(4));
        }
        Assert.Equal(0u, bytes.ReadUInt32(0));
        using NativeBattleNodeInitialization<NativeBattleEffect30State> accepted = state.PrepareInitializeSource(fixture.Resources);
        NativeBattleEffect30State result = accepted.Publish();
        Assert.Same(bytes, result.ExecutableBytes);
        Assert.Equal(1u, bytes.ReadUInt32(0));
    }

    private sealed class Fixture
    {
        public NativeBattleEffect30Frame Frame { get; }
        public NativeBattleEffect30Resources Resources { get; }
        public NativeBattleEffect30Inputs Inputs { get; }
        public List<NativeBattleEffect30Operation> Operations { get; } = [];

        public Fixture()
        {
            byte[] header = new byte[20];
            header[16] = 1;
            NativeBattleContactHalf half = new(3, 0, 0, 0, 0, 7, 0);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            packets = packets with { Headers = packets.Headers.SetItem(0, new(header.ToImmutableArray(), 0, 0, null)), Contacts = packets.Contacts.SetItem(0, new(half, half)) };
            NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            Inputs = new(packets, (_, _, _) => new(0, 3), _ => camera, (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            })
            {
                RefreshRoot = (frame, _) => frame,
                PrepareTarget = (frame, _) => frame,
                AdvanceNodes = (_, _) => throw new InvalidOperationException(),
                PrepareBeam144 = (frame, _) => frame,
                DrawBeam144 = (frame, _) => frame,
                EmitBeamParticles = (frame, _) => frame,
                AdvanceParticles96 = (_, _) => throw new InvalidOperationException()
            };
            NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
            NativeBattleOriginalEffectState effect = new(30, 30, new(30, Entry, Initializer), new(0, 0, 0, 0, 0, 0), Pools[0].Descriptor) { Pools = root.Hierarchy.Pools, ResourcePointers = [new(InitializerGlobal, 0x800)] };
            uint start = ArenaAddress + 0x800;
            NativeBattleEffect30State state = NativeBattleEffect30State.FromColdProcess().WriteDword(0x26D6A0C, start).WriteDword(0x26D6A08, start + DrawStep).WriteDword(0x26D5E04, start + DrawStep).WriteDword(0x26D5E00, start + 2 * DrawStep).WriteDword(0x26D6A34, start + 2 * DrawStep);
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1), state);
            byte[] animation = new byte[CameraLength];
            BinaryPrimitives.WriteUInt16LittleEndian(animation, 2);
            BinaryPrimitives.WriteUInt16LittleEndian(animation.AsSpan(6), CameraLength);
            byte[] sprite = new byte[196];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 7);
            ImmutableDictionary<uint, ImmutableArray<byte>> meshes = new uint[] { 0x160D538, 0x160DCF8, 0x160DC50 }.ToImmutableDictionary(address => address, _ =>
            {
                byte[] bytes = new byte[40];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 8);
                return bytes.ToImmutableArray();
            });
            Resources = new(new("fixture", animation.ToImmutableArray()), new(new byte[SourceLength].ToImmutableArray()) { ResourceAddress = SourceAddress }, meshes, new(0, 0x160DE10, 0, 0, sprite.ToImmutableArray()), Enumerable.Repeat(new NativeBattleEffect30Scatter(0, 0, 0), 19).ToImmutableArray(), 0x61AC3);
        }

        public (NativeBattleEffect30Frame Frame, uint Address) Child() => Append(Frame, 1, TargetCallback, Pools[0].Bank, Inputs);
    }
}
