using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect30Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect30ParticleTests
{
    [Fact]
    public void Actual_particle_capture_uses_physical_birth_and_resource_without_changing_CRT_or_temporary_bytes()
    {
        Fixture nativeFixture = new();
        List<NativeBattleGfGeometryBatch> nativePackets = [];
        NativeBattleEffect30TaskStep native = Draw(nativeFixture, false, nativePackets);
        string frozen = NativeBattleTestSnapshots.Json(native);
        string frozenPackets = NativeBattleTestSnapshots.Json(nativePackets);
        Fixture capturedFixture = new();
        List<NativeBattleGfGeometryBatch> capturedPackets = [];
        NativeBattleEffect30TaskStep captured = Draw(capturedFixture, true, capturedPackets);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozenPackets, NativeBattleTestSnapshots.Json(capturedPackets));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(Assert.Single(capturedPackets).Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal((ushort)30, geometry.Identity.Effect);
        Assert.Equal(capturedFixture.Resources.ParticleSprite.Address, geometry.Identity.ResourceGeneration);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.NotNull(geometry.Sprite);

        NativeBattleEffect30TaskStep Draw(Fixture fixture, bool capture, List<NativeBattleGfGeometryBatch> packets)
        {
            byte[] sprite = new byte[50];
            fixture.Resources.ParticleSprite.Bytes.CopyTo(sprite);
            BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(26), 1);
            sprite[30] = 8;
            sprite[32] = 8;
            NativeBattleEffect30Resources resources = fixture.Resources with { ParticleSprite = fixture.Resources.ParticleSprite with { Bytes = sprite.ToImmutableArray() } };
            NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x26, 0).MarkPayloadUnknown(0x22, 2));
            NativeBattleEffect30ParticleInputs inputs = fixture.Inputs with { CapturePresentation = capture, Projection = fixture.Inputs.Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() }, DeliverPrivate = (current, batch) =>
            {
                packets.Add(batch);
                return current;
            } };
            return NativeBattleEffect30Particles.Advance(frame, fixture.Address, resources, inputs);
        }
    }

    [Fact]
    public void Initialization_draws_three_CRT_values_without_advancing_animation_or_position()
    {
        Fixture fixture = new();
        NativeBattleEffect30TaskStep result = fixture.Advance(fixture.Frame);
        NativeBattleModulePoolTask task = Read(result.Frame, fixture.Address);
        Assert.Equal(new ushort[] { unchecked((ushort)-471), unchecked((ushort)-477), unchecked((ushort)-322) }, new[] { task.ReadWord(0x58), task.ReadWord(0x5A), task.ReadWord(0x5C) });
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)0, task.ReadWord(0x50));
        Assert.Equal((ushort)6, task.ReadWord(0x52));
        Assert.Equal(fixture.Resources.ParticleSprite.Address, task.ReadDword(0x4C));
        Assert.Equal((ushort)100, task.ReadWord(0x1C));
        Assert.Equal(0, fixture.Deliveries);
    }

    [Fact]
    public void Damping_and_integration_truncate_negative_values_toward_zero_and_preserve_packed_W()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1).WriteWord(0x52, 6).WriteWord(0x58, unchecked((ushort)-17)).WriteWord(0x5A, 17).WriteWord(0x5C, unchecked((ushort)-15)).MarkPayloadUnknown(0x22, 2));
        frame = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect30Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        NativeBattleEffect30TaskStep result = fixture.Advance(frame);
        NativeBattleModulePoolTask task = Read(result.Frame, fixture.Address);
        Assert.Equal(new ushort[] { unchecked((ushort)-16), 16, unchecked((ushort)-15) }, new[] { task.ReadWord(0x58), task.ReadWord(0x5A), task.ReadWord(0x5C) });
        Assert.Equal(new ushort[] { 99, 201, 300 }, new[] { task.ReadWord(0x1C), task.ReadWord(0x1E), task.ReadWord(0x20) });
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x22));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Seventh_animation_step_hides_before_draw_clamps_and_releases_actual_hierarchical_child()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1).WriteByte(0x26, 0).WriteWord(0x50, 6).WriteWord(0x52, 6));
        NativeBattleEffect30TaskStep result = fixture.Advance(frame);
        Assert.Equal(2, result.Result);
        Assert.Equal(0, fixture.Deliveries);
        Assert.Equal((byte)0, Read(result.Frame, fixture.Root).ReadByte(0x28));
    }

    [Fact]
    public void Animation_comparison_uses_wrapped_signed_WORD_not_unsigned_age()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1).WriteWord(0x50, 32767).WriteWord(0x52, 6));
        NativeBattleEffect30TaskStep result = fixture.Advance(frame);
        Assert.Equal(0, result.Result);
        Assert.Equal((ushort)32768, Read(result.Frame, fixture.Address).ReadWord(0x50));
        Assert.Equal((byte)1, Read(result.Frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Actual180_draw_publishes_phase_flags_GTE_and_released_temp_without_requiring_unknown_source_W()
    {
        Fixture fixture = new();
        NativeBattleEffect30Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x26, 0).MarkPayloadUnknown(0x22, 2));
        uint cursor = fixture.Cursor(frame);
        NativeBattleEffect30TaskStep result = fixture.Advance(frame);
        Assert.Equal(1, fixture.Deliveries);
        Assert.Equal(cursor, fixture.Cursor(result.Frame));
        Assert.NotNull(result.Frame.Memory.GeometryRegisters);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Staging!.Read(cursor + 4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Staging.Read(cursor + 36, 2)));
        Assert.Equal(0x160DE10u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging.Read(cursor, 4)));
        Assert.Throws<NotSupportedException>(() => Read(result.Frame, fixture.Address).ReadWord(0x22));
    }

    private sealed class Fixture
    {
        public NativeBattleEffect30Frame Frame { get; }
        public NativeBattleEffect30Resources Resources { get; }
        public NativeBattleEffect30ParticleInputs Inputs { get; }
        public uint Address { get; }
        public uint Root { get; }
        public int Deliveries { get; private set; }
        public Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
            Root = root.Address;
            NativeBattleEffectHierarchyAppend child = root.Hierarchy.Append8DC540(4, ParticleCallback, 96, root.Address, (_, _, _) => new(0, 3));
            Address = child.Address;
            NativeBattleOriginalEffectState effect = new(30, 30, new(30, Entry, Initializer), new(0, 0, 0, 0, 0, 0), Pools[0].Descriptor) { Pools = child.Hierarchy.Pools };
            NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = identity, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, effect, new(1), NativeBattleEffect30State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Frame = Write(Frame, Address, Read(Frame, Address).WriteByte(0x26, 4).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300));
            byte[] sprite = new byte[30];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8, 2), 7);
            for (int index = 0; index < 8; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2, 2), 26);
            }
            Resources = new(new("fixture", []), new([]), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, new(0, 0x160DE10, 0, 0, sprite.ToImmutableArray()), [], 0);
            Inputs = new(identity, new(null, 100, 160, 108), 0, Deliver);
        }
        public NativeBattleEffect30TaskStep Advance(NativeBattleEffect30Frame frame) => NativeBattleEffect30Particles.Advance(frame, Address, Resources, Inputs);
        public uint Cursor(NativeBattleEffect30Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        private NativeBattleEffect30Frame Deliver(NativeBattleEffect30Frame frame, NativeBattleGfGeometryBatch batch)
        {
            Assert.Empty(batch.Attempts);
            uint temporary = Cursor(frame) - 180;
            Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging!.Read(temporary + 38, 2)));
            Deliveries++;
            return frame;
        }
    }
}
