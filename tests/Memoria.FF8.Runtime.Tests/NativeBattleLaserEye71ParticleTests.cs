using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Controller;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71ParticleTests
{
    [Fact]
    public void Initialization_draws_three_CRT_values_without_advancing_animation_or_position()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71TaskStep result = fixture.Advance(fixture.Frame);
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
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1).WriteWord(0x52, 6).WriteWord(0x58, unchecked((ushort)-17)).WriteWord(0x5A, 17).WriteWord(0x5C, unchecked((ushort)-15)).MarkPayloadUnknown(0x22, 2));
        frame = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleLaserEye71Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        NativeBattleLaserEye71TaskStep result = fixture.Advance(frame);
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
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1).WriteByte(0x26, 0).WriteWord(0x50, 6).WriteWord(0x52, 6));
        NativeBattleLaserEye71TaskStep result = fixture.Advance(frame);
        Assert.Equal(2, result.Result);
        Assert.Equal(0, fixture.Deliveries);
        Assert.Equal((byte)0, Read(result.Frame, fixture.Root).ReadByte(0x28));
    }

    [Fact]
    public void Animation_comparison_uses_wrapped_signed_WORD_not_unsigned_age()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x29, 1).WriteWord(0x50, 32767).WriteWord(0x52, 6));
        NativeBattleLaserEye71TaskStep result = fixture.Advance(frame);
        Assert.Equal(0, result.Result);
        Assert.Equal((ushort)32768, Read(result.Frame, fixture.Address).ReadWord(0x50));
        Assert.Equal((byte)1, Read(result.Frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Actual180_draw_publishes_phase_flags_GTE_and_released_temp_without_requiring_unknown_source_W()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteByte(0x26, 0).MarkPayloadUnknown(0x22, 2));
        uint cursor = fixture.Cursor(frame);
        NativeBattleLaserEye71TaskStep result = fixture.Advance(frame);
        Assert.Equal(1, fixture.Deliveries);
        Assert.Equal(cursor, fixture.Cursor(result.Frame));
        Assert.NotNull(result.Frame.Memory.GeometryRegisters);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Staging!.Read(cursor + 4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Staging.Read(cursor + 36, 2)));
        Assert.Equal(0x158FF60u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging.Read(cursor, 4)));
        Assert.Equal(1u, result.Frame.State.ReadDword(0x25FCDCC));
        Assert.Throws<NotSupportedException>(() => Read(result.Frame, fixture.Address).ReadWord(0x22));
    }

    private sealed class Fixture
    {
        public NativeBattleLaserEye71Frame Frame { get; }
        public NativeBattleLaserEye71Resources Resources { get; }
        public NativeBattleLaserEye71ParticleInputs Inputs { get; }
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
            NativeBattleOriginalEffectState effect = new(71, 71, new(71, Entry, Initializer), new(0, 0, 0, 0, 0, 0), Pools[0].Descriptor) { Pools = child.Hierarchy.Pools };
            NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = identity, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, effect, new(1), NativeBattleLaserEye71State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Frame = Write(Frame, Address, Read(Frame, Address).WriteByte(0x26, 4).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300));
            byte[] sprite = new byte[30];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8, 2), 7);
            for (int index = 0; index < 8; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2, 2), 26);
            }
            Resources = new(new("fixture", []), new([]), new("fixture", []), [], new(0, 0x158FF60, 0, 0, sprite.ToImmutableArray()), [], 0);
            Inputs = new(identity, new(null, 100, 160, 108), 0, Deliver);
        }
        public NativeBattleLaserEye71TaskStep Advance(NativeBattleLaserEye71Frame frame) => NativeBattleLaserEye71Particles.Advance(frame, Address, Resources, Inputs);
        public uint Cursor(NativeBattleLaserEye71Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        private NativeBattleLaserEye71Frame Deliver(NativeBattleLaserEye71Frame frame, NativeBattleGfGeometryBatch batch)
        {
            Assert.Empty(batch.Attempts);
            uint temporary = Cursor(frame) - 180;
            Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging!.Read(temporary + 38, 2)));
            Deliveries++;
            return frame with { State = frame.State.WriteDword(0x25FCDCC, frame.State.ReadDword(0x25FCDCC) + 1) };
        }
    }
}
