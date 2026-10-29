using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Controller;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71Smooth60Tests
{
    [Theory]
    [InlineData(2048, 3)]
    [InlineData(4096, 2)]
    public void All_authored_beam_passes_capture_distinct_task_owned_sources_without_advancing_random(int ratio, int expected)
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, fixture.Beam, Read(fixture.Frame, fixture.Beam).WriteWord(0x8C, (ushort)ratio).WriteWord(0x8E, 600));
        List<NativeBattleGfGeometryBatch> batches = [];
        NativeBattleLaserEye71LowerInputs inputs = new((current, _, _, _) => throw new InvalidOperationException(), (_, _) => fixture.Identity, (current, _, _, _) => new(current, fixture.Identity), _ => fixture.Identity, (_, _, _) => new(0, 3), (current, batch) =>
        {
            batches.Add(batch);
            return current;
        }, fixture.Projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        uint cursor = fixture.Cursor(frame);
        NativeBattleLaserEye71Frame result = NativeBattleLaserEye71BeamLower.Draw(frame, fixture.Beam, fixture.Resources, inputs);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(cursor, fixture.Cursor(result));
        Assert.Equal(expected, batches.Count);
        List<NativeBattlePilotVisualIdentity> identities = [];
        foreach (NativeBattleGfGeometryBatch batch in batches)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(batch.Attempts).Primitive);
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal((ushort)71, geometry.Identity.Effect);
            Assert.Equal((byte)3, geometry.CornerCount);
            Assert.False(geometry.ScreenSpace);
            Assert.True(geometry.A.Z > 0);
            identities.Add(geometry.Identity);
        }
        Assert.Equal(expected, identities.Distinct().Count());
        Assert.Single(identities.Select(identity => (identity.Bank, identity.Slot, identity.Generation)).Distinct());
    }

    [Fact]
    public void Particle_capture_retains_native_animation_and_moves_with_actual_task_position()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = Write(fixture.Frame, fixture.Particle, Read(fixture.Frame, fixture.Particle).WriteByte(0x29, 1).WriteDword(0x4C, fixture.Resources.ParticleSprite.Address).WriteWord(0x52, 6).WriteWord(0x58, 256));
        List<NativeBattlePilotVisualGeometry> captured = [];
        NativeBattleLaserEye71ParticleInputs inputs = new(fixture.Identity, fixture.Projection, 0, (current, batch) =>
        {
            Assert.Equal(2, batch.Attempts.Length);
            Assert.Equal(40, batch.AllocationBytes);
            NativeBattleGfGeometryAttempt physical = batch.Attempts[0];
            Assert.Null(physical.Primitive);
            Assert.Equal(0, physical.ByteOffsetRelative);
            Assert.Equal(40, physical.Data.Length);
            for (int index = 0; index < 40; index++)
            {
                Assert.Equal(index is 30 or 31 or 38 or 39 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, physical.WriteMask[index]);
            }
            NativeBattleGfGeometryAttempt emitted = Assert.Single(batch.Attempts.Where(attempt => attempt.Primitive is not null));
            Assert.Equal(physical.ByteOffsetRelative, emitted.ByteOffsetRelative);
            Assert.Equal(physical.Data, emitted.Data);
            Assert.All(emitted.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(emitted.Primitive);
            captured.Add(Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry));
            return current;
        });
        uint cursor = fixture.Cursor(frame);
        NativeBattleLaserEye71TaskStep first = NativeBattleLaserEye71Particles.Advance(frame, fixture.Particle, fixture.Resources, inputs);
        NativeBattleLaserEye71TaskStep second = NativeBattleLaserEye71Particles.Advance(first.Frame, fixture.Particle, fixture.Resources, inputs);
        Assert.Equal(frame.Random, second.Frame.Random);
        Assert.Equal(cursor, fixture.Cursor(second.Frame));
        Assert.Equal((ushort)2, Read(second.Frame, fixture.Particle).ReadWord(0x50));
        Assert.Equal(2, captured.Count);
        Assert.Equal(captured[0].Identity, captured[1].Identity);
        Assert.True(captured[1].A.X > captured[0].A.X);
        Assert.NotNull(captured[0].Sprite);
    }

    private sealed class Fixture
    {
        internal NativeBattleLaserEye71Frame Frame { get; }
        internal uint Beam { get; }
        internal uint Particle { get; }
        internal NativeBattleLaserEye71Resources Resources { get; }
        internal NativeBattlePackedMatrix32 Identity { get; } = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        internal NativeBattleCameraProjection Projection { get; } = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);

        internal Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
            NativeBattleEffectHierarchyAppend beam = root.Hierarchy.Append8DC540(3, BeamCallback, 148, root.Address, (_, _, _) => new(0, 3));
            Beam = beam.Address;
            NativeBattleEffectHierarchyAppend particle = beam.Hierarchy.Append8DC540(4, ParticleCallback, 96, root.Address, (_, _, _) => new(0, 3));
            Particle = particle.Address;
            NativeBattleOriginalEffectState effect = new(71, 71, new(71, Entry, Initializer), new(0, 0, 0, 0, 0, 0), Pools[0].Descriptor) { Pools = particle.Hierarchy.Pools };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ModuleSpriteBillboard21DFED0 = Identity, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, effect, new(1), NativeBattleLaserEye71State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Frame = Write(Frame, Beam, Read(Frame, Beam).WriteBytes(0x54, Identity.Bytes.AsSpan()).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x20, 10000).WriteWord(0x7C, 400).WriteWord(0x80, 400));
            Frame = Write(Frame, Particle, Read(Frame, Particle).WriteWord(0x20, 10000));
            Resources = new(new("fixture", []), new([]), new("fixture", []), new uint[] { 0x158F688, 0x158FDA0, 0x158FE48 }.Select(Mesh).ToImmutableArray(), Sprite(), [], 0);
        }

        internal uint Cursor(NativeBattleLaserEye71Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));

        private static NativeBattleStaticMeshResource Mesh(uint address)
        {
            byte[] bytes = new byte[76];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
            short[] vertices = [-400, -400, 0, 0, 400, -400, 0, 0, -400, 400, 0, 0];
            for (int index = 0; index < vertices.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), vertices[index]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 0x20000000);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 4);
            return new(0, address, bytes.ToImmutableArray());
        }

        private static NativeBattleModuleResource Sprite()
        {
            byte[] bytes = new byte[50];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 7);
            for (int index = 0; index < 7; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), 26);
            }
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), -1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(26), 1);
            bytes[30] = 2;
            bytes[31] = 128;
            bytes[32] = 2;
            bytes[33] = 0x2C;
            return new(0, 0x158FF60, 0, 0, bytes.ToImmutableArray());
        }
    }
}
