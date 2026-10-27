using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect320Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect320ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena;

    [Fact]
    public void Root_zero_selects_second_packet_bank_before_advancing_without_children()
    {
        NativeBattleEffect320Step step = AdvanceRoot65BCC0(Frame(0, false), RootBank, null!, new Harness().Inputs());
        Assert.Equal(Arena + PacketBank1Offset, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(1u, Read(step.Frame, RootBank).ReadDword(16));
        Assert.Equal(1, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Single(step.Frame.Effect.Pools);
    }

    [Fact]
    public void Root_retires_only_after_scans_and_keeps_final_clock_unchanged()
    {
        NativeBattleEffect320Frame frame = Frame(98);
        NativeBattleEffect320Step step = AdvanceRoot65BCC0(frame, RootBank, null!, new Harness().Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(98, Read(step.Frame, RootBank).ReadWord(12));
        Assert.Equal(99, Read(step.Frame, Main).ReadWord(12));
        Assert.Equal(Arena + PacketBank1Offset, step.Frame.State.ReadDword(CursorGlobal));
    }

    [Fact]
    public void Native_contact83_is_distinct_from_main_completion98_and_survives_JSON()
    {
        Harness harness = new();
        NativeBattleEffect320Frame frame = Frame(83);
        frame = Write(frame, Main, Read(frame, Main).WriteDword(0x340, Programme3Address));
        NativeBattleEffect320Step contacted = AdvanceMain65BE90(frame, Main, EmptyResources(), harness.Inputs());
        Assert.Equal(0, contacted.Result);
        Assert.Equal(new NativeBattleEffect320AllContacts506BA0(0, 1), Assert.Single(harness.Operations));
        Assert.Equal(84, Read(contacted.Frame, Main).ReadWord(12));
        frame = JsonSerializer.Deserialize<NativeBattleEffect320Frame>(JsonSerializer.Serialize(contacted.Frame))!;
        frame = Write(frame, Main, Read(frame, Main).WriteWord(12, 97));
        NativeBattleEffect320Step ended = AdvanceMain65BE90(frame, Main, EmptyResources(), harness.Inputs());
        Assert.Equal(2, ended.Result);
        Assert.Equal(98, Read(ended.Frame, Main).ReadWord(12));
        Assert.Single(harness.Operations);
    }

    [Fact]
    public void Early_births_use_three_CRT_draws_per_success_and_keep_mutable_source_X_on_exhaustion()
    {
        NativeBattleEffect320Frame frame = Frame(15);
        Harness harness = new();
        for (int batch = 0; batch < 32; batch++)
        {
            frame = BirthEarlyParticles(frame, Main, 15, EmptyResources(), harness.Inputs());
        }
        NativeCrtRandomState expected = new(42);
        for (int draw = 0; draw < 64 * 3; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(unchecked((ushort)-100), frame.State.ReadWord(MutableAnchorAddress));
        Assert.Equal(128, harness.Origins.Count);
        Assert.Equal(100, unchecked((short)harness.Origins[0][0]));
        Assert.All(frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ParticleDescriptor).Slots, task => Assert.Equal(0, task.ReadWord(12)));
        NativeBattleEffect320Frame full = BirthEarlyParticles(frame, Main, 15, EmptyResources(), harness.Inputs());
        Assert.Equal(expected, full.Random);
        Assert.Equal(128, harness.Origins.Count);
        Assert.Equal(unchecked((ushort)-100), full.State.ReadWord(MutableAnchorAddress));
    }

    [Fact]
    public void Last_available_slot_stops_before_second_birth_and_retains_positive_source_X()
    {
        NativeBattleEffect320Frame frame = Frame(21);
        for (int index = 0; index < 63; index++)
        {
            (frame, _) = Append(frame, ParticleDescriptor, ParticleCallback);
        }
        Harness harness = new();
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 3; draw++)
        {
            expected = expected.Draw(out _);
        }
        frame = BirthEarlyParticles(frame, Main, 21, EmptyResources(), harness.Inputs());
        Assert.Equal(expected, frame.Random);
        Assert.Equal((ushort)100, frame.State.ReadWord(MutableAnchorAddress));
        NativeBattleModulePoolTask child = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ParticleDescriptor).Slots[63];
        Assert.Equal(1, child.ReadWord(12));
        Assert.Equal(2, harness.Origins.Count);
    }

    [Fact]
    public void Caller48_preserves_unwritten_phase_and_exact_depth_morph_and_material_fields()
    {
        NativeBattleEffect320Frame frame = Frame(0);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(new(0, 0, 0)) with { X = 10, Y = 20, Z = 30 };
        NativeBattleEffectArena early = BuildCaller(frame, matrix, 0x11111111, 0x22222222, -16, null);
        Assert.Throws<NotSupportedException>(() => early.Read(0x2A, 2));
        Assert.Equal(unchecked((ushort)-16), BinaryPrimitives.ReadUInt16LittleEndian(early.Read(0x28, 2)));
        Assert.Equal(Arena + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(early.Read(0x2C, 4)));
        NativeBattleEffectArena late = BuildCaller(frame, matrix, 0x33333333, 0x44444444, -512, 832);
        Assert.Equal((ushort)832, BinaryPrimitives.ReadUInt16LittleEndian(late.Read(0x2A, 2)));
        Assert.Equal(0x33333333u, BinaryPrimitives.ReadUInt32LittleEndian(late.Read(0x20, 4)));
        Assert.Throws<NotSupportedException>(() => early.Read(18, 2));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 409)]
    [InlineData(5, 2048)]
    [InlineData(9, 3686)]
    [InlineData(10, 4096)]
    public void Interpolation_uses_unsigned_magic_multiply_before_shift(int elapsed, int expected)
    {
        Assert.Equal(expected, InterpolationFactor(elapsed));
    }

    private static NativeBattleEffect320Frame Frame(short phase, bool children = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, unchecked((ushort)phase)).WriteByte(14, children ? (byte)1 : (byte)0).WriteDword(16, 0)) };
        NativeBattleEffect320State state = NativeBattleEffect320State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena) with { MutableAnchorSourceBound = true };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (children)
        {
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, Main, MainStride, 1);
            (main, _) = main.Append(MainCallback);
            main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteBytes(12, new byte[MainStride - 12]).WriteWord(12, unchecked((ushort)phase))) };
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, Arena + ParticleBankOffset, ParticleStride, ParticleCapacity);
            pools = [root, main, particles];
            state = state.DetachCommonQueue(MainDescriptor).DetachCommonQueue(ParticleDescriptor);
        }
        NativeBattleOriginalEffectState effect = new(320, 319, new(320, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleEffect320Resources EmptyResources()
    {
        byte[] act = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(4), 8);
        NativeBattleGfAnimationSource source = new(Programme3Address, act.ToImmutableArray());
        byte[] vector = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(2), 7);
        BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(4), -41);
        BinaryPrimitives.WriteInt16LittleEndian(vector.AsSpan(6), 3);
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(Programme3Address, source), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, null!, 0, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0xFF12A8, vector.ToImmutableArray()));
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect320Operation> Operations { get; } = [];
        public List<ushort[]> Origins { get; } = [];

        public NativeBattleEffect320Inputs Inputs()
        {
            return new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets(), (_, actor) => new(actor, 2), (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Euler701310(new(0, 0, 0)), null), _ => new(null, 512, 160, 108), _ => 0, (frame, actor, retained) => new(frame, retained), (frame, input, retained) =>
            {
                Origins.Add(Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(input.Read(index * 2, 2))).ToArray());
                return new(frame, CopyBytes(input, 0, retained, 0, 6));
            }, (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            });
        }

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 11, -1, 305, 320, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
