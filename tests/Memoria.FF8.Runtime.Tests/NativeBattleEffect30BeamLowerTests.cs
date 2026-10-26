using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect30Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect30BeamLowerTests
{
    [Fact]
    public void Initial144_byte_beam_keeps_all_three_scales4096_and_packed_unknown_W()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).MarkPayloadUnknown(0x7A, 2));
        List<string> calls = [];
        NativeBattleEffect30TaskStep result = AdvanceBeam(frame, fixture.Beam, BeamInputs(calls));
        NativeBattleModulePoolTask beam = Read(result.Frame, fixture.Beam);
        Assert.Equal(new uint[] { 4096, 4096, 4096 }, new[] { beam.ReadDword(0x30), beam.ReadDword(0x34), beam.ReadDword(0x38) });
        Assert.Equal(0x160D538u, beam.ReadDword(0x4C));
        Assert.Equal((byte)1, beam.ReadByte(0x29));
        Assert.Throws<NotSupportedException>(() => beam.ReadWord(0x22));
        Assert.Equal(new[] { "prepare", "draw", "scatter" }, calls);
    }

    [Fact]
    public void Shrinking_beam_draws_and_scatters_before_completing_its_actual_parent_link()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteByte(0x29, 4).WriteWord(0x8C, 1024));
        List<string> calls = [];
        NativeBattleEffect30TaskStep result = AdvanceBeam(frame, fixture.Beam, BeamInputs(calls));
        Assert.Equal(2, result.Result);
        Assert.Equal(new[] { "prepare", "draw", "scatter" }, calls);
        Assert.Equal((byte)0, result.Frame.Effect.Pools[0].Slots[0].ReadByte(0x28));
    }

    [Fact]
    public void Scatter_radius128_can_place_births_more_than64_units_from_the_weighted_beam_point()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame with { Random = new(8) }, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteWord(0x24, 5));
        NativeBattleEffect30Frame result = NativeBattleEffect30BeamLower.Emit(frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask child = Assert.Single(result.Effect.Pools[4].Slots.Where(slot => slot.Callback == ParticleCallback));
        NativeBattleEffectArena shared = NativeBattleEffectNodeContextMemory.Capture(result.Memory);
        int factor = BinaryPrimitives.ReadInt32LittleEndian(shared.Read(0xB0, 4));
        int squared = 0;
        for (int axis = 0; axis < 3; axis++)
        {
            int baseline = 100 + axis * 100 + 400 * factor / 4096;
            int delta = unchecked((short)child.ReadWord(0x1C + axis * 2)) - baseline;
            squared += delta * delta;
        }
        Assert.True(squared > 64 * 64);
    }

    [Fact]
    public void Prepare_samples_target_once_and_caster_each_time_preserving_matrix_tail_and_anchor_W()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = fixture.Frame;
        NativeBattleModulePoolTask task = Read(frame, fixture.Beam).MarkPayloadUnknown(0x66, 14).MarkPayloadUnknown(0x7A, 2).MarkPayloadUnknown(0x82, 2);
        frame = Write(frame, fixture.Beam, task);
        frame = NativeBattleEffect30BeamLower.Prepare(frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        Assert.Equal(new byte[] { 0xF1, 0xF1 }, fixture.Selectors);
        Assert.Equal(new NativeBattleVector(400, 272, 400), Assert.Single(fixture.Directions));
        Assert.Equal((ushort)100, Read(frame, fixture.Beam).ReadWord(0x74));
        Assert.Equal((ushort)500, Read(frame, fixture.Beam).ReadWord(0x7C));
        Assert.Equal((ushort)627, Read(frame, fixture.Beam).ReadWord(0x8E));
        Assert.Throws<NotSupportedException>(() => Read(frame, fixture.Beam).ReadWord(0x66));
        Assert.Throws<NotSupportedException>(() => Read(frame, fixture.Beam).ReadWord(0x7A));
        Assert.Throws<NotSupportedException>(() => Read(frame, fixture.Beam).ReadWord(0x82));
        frame = Write(frame, fixture.Beam, Read(frame, fixture.Beam).WriteWord(0x24, 1));
        frame = NativeBattleEffect30BeamLower.Prepare(frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        Assert.Equal(new byte[] { 0xF1, 0xF1, 0xF1 }, fixture.Selectors);
        Assert.Equal(2, fixture.Directions.Count);
    }

    [Fact]
    public void Emit_uses_shared_factor_owner_four_random_draws_and_single_axis_jitter_after_JSON()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteWord(0x24, 5));
        frame = JsonSerializer.Deserialize<NativeBattleEffect30Frame>(JsonSerializer.Serialize(frame))!;
        uint cursor = TemporaryCursor(frame);
        NativeBattleEffect30Frame result = NativeBattleEffect30BeamLower.Emit(frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask child = Assert.Single(result.Effect.Pools[4].Slots.Where(slot => slot.Callback == ParticleCallback));
        Assert.Equal(new ushort[] { 104, 205, 308 }, new[] { child.ReadWord(0x1C), child.ReadWord(0x1E), child.ReadWord(0x20) });
        NativeCrtRandomState random = new(1);
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, result.Random);
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Capture(result.Memory);
        Assert.Equal(4055, BinaryPrimitives.ReadInt32LittleEndian(context.Read(0xAC, 4)));
        Assert.Equal(41, BinaryPrimitives.ReadInt32LittleEndian(context.Read(0xB0, 4)));
        Assert.Equal(0x2793E58u, result.State.ReadDword(0x26D6A28));
        Assert.Equal(cursor, TemporaryCursor(result));
        Assert.NotNull(result.Memory.GeometryRegisters);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal((byte)1, Read(result, fixture.Beam).ReadByte(0x28));
    }

    [Fact]
    public void Zero_birth_row_preserves_random_and_factors_but_requires_actual_scratch_pointer()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame result = NativeBattleEffect30BeamLower.Emit(fixture.Frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.Empty(result.Effect.Pools[4].Slots.Where(slot => slot.Callback != 0));
        Assert.Equal(0x2793E58u, result.State.ReadDword(0x26D6A28));
        NativeBattleEffect30Frame missing = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x26D6A28, 0) };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect30BeamLower.Emit(missing, fixture.Beam, fixture.Resources, fixture.Inputs));
    }

    [Fact]
    public void Completed_emission_does_not_read_retained_scratch_or_random()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteWord(0x24, 19));
        frame = frame with { State = frame.State.WriteDword(0x26D6A28, 0) };
        Assert.Same(frame, NativeBattleEffect30BeamLower.Emit(frame, fixture.Beam, fixture.Resources, fixture.Inputs));
    }

    [Theory]
    [InlineData(2048, 3)]
    [InlineData(4096, 2)]
    public void Draw_publishes_each_physical_context_before_delivery_and_restores_scale_tail(int ratio, int draws)
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteWord(0x8C, (ushort)ratio).WriteWord(0x8E, 600).MarkPayloadUnknown(0x3C, 4).MarkPayloadUnknown(0x43, 1));
        uint cursor = TemporaryCursor(frame);
        NativeBattleEffect30Frame result = NativeBattleEffect30BeamLower.Draw(frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        Assert.Equal(draws, fixture.Deliveries);
        Assert.Equal(cursor, TemporaryCursor(result));
        Assert.Equal(4096u, Read(result, fixture.Beam).ReadDword(0x30));
        Assert.Equal(4096u, Read(result, fixture.Beam).ReadDword(0x34));
        Assert.Equal(unchecked((uint)(unchecked((int)Read(result, fixture.Beam).ReadWord(0x8E) * ratio) / 4864)), Read(result, fixture.Beam).ReadDword(0x38));
        Assert.Throws<NotSupportedException>(() => Read(result, fixture.Beam).ReadDword(0x3C));
        Assert.NotNull(result.Memory.GeometryRegisters);
        Assert.Single(fixture.Directions);
    }

    [Fact]
    public void Hidden_draw_still_executes_rotation_and_horizontal_look_without_packet_delivery()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteByte(0x26, 4).WriteWord(0x8C, 4096).WriteWord(0x46, 0));
        NativeBattleEffect30Frame result = NativeBattleEffect30BeamLower.Draw(frame, fixture.Beam, fixture.Resources, fixture.Inputs);
        Assert.Equal(0, fixture.Deliveries);
        Assert.Equal(new NativeBattleVector(400, 0, 400), Assert.Single(fixture.Directions));
        Assert.Equal((ushort)0, Read(result, fixture.Beam).ReadWord(0x1E));
        Assert.NotNull(result.Memory.GeometryRegisters);
    }

    private static uint TemporaryCursor(NativeBattleEffect30Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));

    private static NativeBattleEffect30Inputs BeamInputs(List<string> calls)
    {
        NativeBattleEffect30Frame Mark(NativeBattleEffect30Frame frame, string name)
        {
            calls.Add(name);
            return frame;
        }
        return new(null!, null!, null!, (frame, _) => frame)
        {
            RefreshRoot = null!,
            PrepareTarget = null!,
            AdvanceNodes = null!,
            PrepareBeam144 = (frame, _) => Mark(frame, "prepare"),
            DrawBeam144 = (frame, _) => Mark(frame, "draw"),
            EmitBeamParticles = (frame, _) => Mark(frame, "scatter"),
            AdvanceParticles96 = null!
        };
    }


    private sealed class Fixture
    {
        public NativeBattleEffect30Frame Frame { get; }
        public uint Beam { get; }
        public NativeBattleEffect30Resources Resources { get; }
        public NativeBattleEffect30LowerInputs Inputs { get; }
        public List<byte> Selectors { get; } = [];
        public List<NativeBattleVector> Directions { get; } = [];
        public int Deliveries { get; private set; }
        private static readonly NativeBattlePackedMatrix32 Identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);

        public Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
            NativeBattleEffectHierarchyAppend beam = root.Hierarchy.Append8DC540(3, BeamCallback, 144, root.Address, (_, _, _) => new(0, 3));
            Beam = beam.Address;
            NativeBattleOriginalEffectState effect = new(30, 30, new(30, Entry, Initializer), new(0, 0, 0, 0, 0, 0), Pools[0].Descriptor) { Pools = beam.Hierarchy.Pools };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, effect, new(1), NativeBattleEffect30State.FromColdProcess().WriteDword(0x26D6A28, 0x2793E58)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleModulePoolTask task = Read(Frame, Beam).WriteBytes(0x54, Identity.Bytes.AsSpan()).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x40, 0x00112233).WriteWord(0x74, 100).WriteWord(0x76, 200).WriteWord(0x78, 300).WriteWord(0x7C, 500).WriteWord(0x7E, 600).WriteWord(0x80, 700);
            Frame = Write(Frame, Beam, task);
            ImmutableArray<NativeBattleEffect30Scatter> rows = Enumerable.Repeat(new NativeBattleEffect30Scatter(0, 0, 0), 19).ToImmutableArray().SetItem(5, new(0, 1024, 1));
            ImmutableDictionary<uint, ImmutableArray<byte>> meshes = new uint[] { 0x160D538, 0x160DC50, 0x160DCF8 }.ToImmutableDictionary(address => address, address =>
            {
                byte[] bytes = new byte[40];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, 8);
                return bytes.ToImmutableArray();
            });
            Resources = new(new("fixture", []), new([]), meshes, new(0, 0, 0, 0, []), rows, 0);
            Inputs = new(ReadBone, (_, _) => Identity, Look, _ => Identity, (_, _, _) => new(0, 3), Deliver, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        }

        private NativeBattleEffect30PointStep ReadBone(NativeBattleEffect30Frame frame, byte actor, byte selector, short distance)
        {
            Selectors.Add(selector);
            return new(frame, actor == 3 ? new(500, 600, 700) : new(100, 200, 300));
        }

        private NativeBattleEffect30MatrixStep Look(NativeBattleEffect30Frame frame, NativeBattlePackedMatrix32 destination, NativeBattleVector direction, NativeBattleVector up)
        {
            Assert.Equal(new NativeBattleVector(0, 4096, 0), up);
            Directions.Add(direction);
            return new(frame, Identity);
        }

        private NativeBattleEffect30Frame Deliver(NativeBattleEffect30Frame frame, NativeBattleGfGeometryBatch batch)
        {
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleTemporaryMemory.ReadView(frame.Staging!, TemporaryCursor(frame) - 88, 88).Read(0x18, 4)));
            Deliveries++;
            return frame;
        }
    }
}
