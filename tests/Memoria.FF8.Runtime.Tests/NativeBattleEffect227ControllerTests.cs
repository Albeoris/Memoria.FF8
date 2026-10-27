using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227ControllerTests
{
    private const uint Arena = 0x20DFAB8;
    private const uint Main = Arena + 0x180;

    [Fact]
    public void Root_first_callback_selects_second_bank_and_does_not_touch_children_or_anchor()
    {
        Harness harness = new();
        NativeBattleEffect227Step first = AdvanceRoot693840(Frame(0, false), RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + SecondPacketOffset, first.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(Arena + ThirdPacketOffset, first.Frame.State.ReadDword(SecondCursorGlobal));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadByte(14));
        Assert.Equal(1, Read(first.Frame, RootBank).ReadWord(12));
        Assert.Single(first.Frame.Effect.Pools);
        Assert.Equal(0, harness.BoneQueries);
        NativeBattleEffect227Frame later = Write(first.Frame, RootBank, Read(first.Frame, RootBank).WriteWord(12, 2));
        NativeBattleEffect227Step second = AdvanceRoot693840(later, RootBank, null!, harness.Inputs());
        Assert.Equal(Arena + FirstPacketOffset, second.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(Arena + SecondPacketOffset, second.Frame.State.ReadDword(SecondCursorGlobal));
        Assert.Equal(0, Read(second.Frame, RootBank).ReadByte(14));
    }

    [Fact]
    public void Initialized_root_publishes_current_anchor_frame_and_retires_without_increment()
    {
        Harness harness = new();
        NativeBattleEffect227Frame frame = Frame(17, true, false);
        frame = frame with { State = frame.State.WriteView(AnchorAddress, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xCAFE)), 8) };
        NativeBattleEffect227Step result = AdvanceRoot693840(frame, RootBank, Resources(), harness.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(17, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(1, harness.BoneQueries);
        Assert.Equal((short)67, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.State.Read(AnchorAddress, 2)));
        Assert.Equal((ushort)0xCAFE, result.Frame.State.ReadWord(AnchorAddress + 6));
        Assert.Equal(999u, result.Frame.State.ReadDword(InitializerGlobal));
    }

    [Fact]
    public void Main_drain_returns_particle_orphans_to_common_without_forcing_their_flags_clear()
    {
        NativeBattleEffect227Frame frame = Frame(17, true, false);
        NativeBattleEffectTaskPool particles = frame.Effect.Pools[2];
        (particles, int slot) = particles.Append(ParticleCallback);
        particles = particles with { Head = null, Tail = null, Slots = particles.Slots.SetItem(slot, particles.Slots[slot].WriteBytes(12, new byte[12]).WriteWord(14, 0x4321)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, particles) } };
        NativeBattleEffect227Frame result = Advance(frame, Resources(), new Harness().Inputs());
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Empty(result.Effect.Pools);
        Assert.NotNull(result.State.FixedBanks[RootBank]);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena!.Read(0, 2)));
        Assert.Equal((ushort)0x4321, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena.Read(14, 2)));
    }

    [Fact]
    public void Record_initializer_changes_only_the128_life_bytes_and_current_cursor()
    {
        NativeBattleEffect227Frame frame = Frame(0, false);
        byte[] retained = Enumerable.Repeat((byte)0xAB, RecordStride * RecordCount).ToArray();
        frame = WriteBytes(frame, Arena + RecordOffset, NativeBattleEffectArena.Clear().Write(0, retained), retained.Length);
        frame = InitializeRecords693960(frame);
        byte[] result = ReadBytes(frame, Arena + RecordOffset, retained.Length).Read(0, retained.Length);
        for (int offset = 0; offset < result.Length; offset++)
        {
            Assert.Equal(offset % RecordStride == 15 ? (byte)0 : (byte)0xAB, result[offset]);
        }
        Assert.Equal(Arena + RecordOffset, frame.State.ReadDword(RecordCursorGlobal));
    }

    [Theory]
    [InlineData(38, 0, 42)]
    [InlineData(85, 1, 30)]
    [InlineData(115, 2, 30)]
    [InlineData(165, 3, 15)]
    public void Text_uses_actual_caster_index_and_original_duration(int age, int text, int duration)
    {
        Harness harness = new();
        EmitMainText(Frame(age), age, harness.Inputs());
        Assert.Equal(new NativeBattleEffect227Text4876F0(3, text, duration), Assert.Single(harness.Operations));
    }

    [Theory]
    [InlineData(0, 0x112DAA0)]
    [InlineData(212, 0x112DAA4)]
    public void Sound_uses_actual_descriptor_and_flags_pan(int age, uint source)
    {
        Harness harness = new();
        EmitMainEvents(Frame(age), Main, age, harness.Inputs());
        Assert.Equal(new NativeBattleEffect227Sound501330(source, 0, 128), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Vibration_stop_reads_current_handle_not_a_constant()
    {
        NativeBattleEffect227Frame frame = Frame(280);
        frame = Write(frame, Main, Read(frame, Main).WriteDword(16, unchecked((uint)-123)));
        Harness harness = new();
        EmitMainEvents(frame, Main, 280, harness.Inputs());
        Assert.Equal(new NativeBattleEffect227VibrationStop4A2940(-123), Assert.Single(harness.Operations));
    }

    [Fact]
    public void Successful_optional_birth_draws_four_crt_values_in_order_and_clears_exact_payload()
    {
        NativeBattleEffect227Frame frame = Frame(222);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort third).Draw(out ushort fourth);
        frame = BirthParticle(frame);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        NativeBattleModulePoolTask child = pool.Slots[pool.Head!.Value];
        Assert.Equal(expected, frame.Random);
        Assert.Equal(first, child.ReadWord(14));
        Assert.Equal(second, child.ReadWord(16));
        Assert.Equal((third & 1) != 0 ? -16 : 16, Signed(child, 18));
        Assert.Equal(-37 - (fourth & 31), Signed(child, 20));
        Assert.Equal(0, child.ReadWord(12));
        Assert.Equal(0, child.ReadWord(22));
    }

    [Fact]
    public void Exhausted_optional_birth_does_not_consume_random_or_modify_payloads()
    {
        NativeBattleEffect227Frame frame = Frame(222);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[2];
        while (pool.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (pool, _) = pool.Append(ParticleCallback);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, pool) } };
        NativeBattleEffect227Frame result = BirthParticle(frame);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.Effect.Pools, result.Effect.Pools);
    }

    [Theory]
    [InlineData(0, 1250)]
    [InlineData(7, 4750)]
    public void Endpoint_blend_uses_native256_bias_and512_step_with_unknown_output_padding(int elapsed, int expectedX)
    {
        NativeBattleEffect227Frame frame = Frame(262);
        frame = Write(frame, Main, CopyTask(Read(frame, Main), 0x14, PointView(new(1000, 0, 0)), 8));
        frame = frame with { State = frame.State.WriteView(EndpointAddress, PointView(new(5000, 0, 0)), 8) };
        frame = JsonSerializer.Deserialize<NativeBattleEffect227Frame>(JsonSerializer.Serialize(frame))!;
        (frame, NativeBattleEffectArena point) = BlendEndpoint(frame, Main, elapsed, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(expectedX, Vector(point).X);
        Assert.Equal(new[] { false, false }, point.ReadSnapshot(6, 2).Known);
        Assert.NotNull(frame.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void Caller16_preserves_actual_point_padding_and_signed_depth_width()
    {
        NativeBattleEffectArena point = PointView(new(1, 2, 3)).Write(6, BitConverter.GetBytes((ushort)0xCAFE));
        NativeBattleEffectArena caller = Caller16(point, -128, Arena + MorphOffset);
        Assert.Equal(point.Read(0, 8), caller.Read(0, 8));
        Assert.Equal(-128, BinaryPrimitives.ReadInt32LittleEndian(caller.Read(8, 4)));
        Assert.Equal(Arena + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(caller.Read(12, 4)));
    }

    [Fact]
    public void Terminal_main_draws_then_sets_only_model_flag_byte_and_does_not_increment_age()
    {
        NativeBattleEffect227Frame frame = Frame(284);
        Harness harness = new();
        NativeBattleEffect227Step result = AdvanceMain693A10(frame, Main, null!, harness.Inputs());
        Assert.Equal(2, result.Result);
        Assert.Equal(284, Read(result.Frame, Main).ReadWord(12));
        Assert.Equal(new NativeBattleEffect227ModelFlags(ModelAddress(3), 1, 0xFF, 4), Assert.Single(harness.Operations));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    private static NativeBattleEffect227Resources Resources() => new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(AnchorPointAddress, new byte[8].ToImmutableArray()));

    private static NativeBattleEffect227Frame Frame(int age, bool children = true, bool main = true)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
        (root, _) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteBytes(12, new byte[4]).WriteWord(12, (ushort)age).WriteByte(15, children ? (byte)1 : (byte)0)) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root];
        if (children)
        {
            foreach (NativeBattleCureFamilyPool shape in CommonPools)
            {
                NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, Arena + shape.Bank, shape.Stride, shape.Capacity);
                if (shape.Descriptor == MainDescriptor && main)
                {
                    (pool, _) = pool.Append(MainCallback);
                    pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteBytes(12, new byte[shape.Stride - 12]).WriteWord(12, (ushort)age)) };
                }
                pools = pools.Add(pool);
            }
        }
        NativeBattleEffect227State state = NativeBattleEffect227State.FromColdProcess().DetachBank(0).WriteDword(ArenaGlobal, Arena).WriteDword(CasterGlobal, ModelAddress(3));
        NativeBattleOriginalEffectState effect = new(227, 226, new(227, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, pools);
        return new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect227Operation> Operations { get; } = [];
        public int BoneQueries { get; private set; }
        public NativeBattleEffect227Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, (frame, _, _, retained) =>
        {
            BoneQueries++;
            return new(frame with { State = frame.State.WriteDword(InitializerGlobal, 999) }, PutPoint(retained, 0, new(67, -89, 123)));
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
