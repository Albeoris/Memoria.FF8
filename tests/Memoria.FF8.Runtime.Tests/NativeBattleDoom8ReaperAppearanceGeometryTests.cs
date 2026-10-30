using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ReaperAppearanceGeometryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Presentation_capture_preserves_detached_native_state_and_rejected_face_writes(bool quad, bool reverse)
    {
        Fixture nativeFixture = new(quad: quad, reverse: reverse);
        NativeBattleDoom8AppearanceStep native = nativeFixture.Draw();
        string frozen = SnapshotJson(native);
        Fixture visualFixture = new(quad: quad, reverse: reverse);
        NativeBattlePilotVisualIdentity identity = new(8, 0x275DF50, 0, 1, 0) { ResourceGeneration = visualFixture.Model.Address };
        NativeBattleCameraProjection projection = visualFixture.Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
        NativeBattleDoom8AppearanceStep visual = NativeBattleDoom8ReaperAppearance.Draw8CC7B0(visualFixture.Frame, visualFixture.Staging, visualFixture.Model, projection, pilotIdentity: identity);
        Assert.Equal(frozen, SnapshotJson(visual));
        foreach (NativeBattleGfGeometryAttempt attempt in visual.Batch.Attempts.Where(attempt => attempt.Primitive is not null))
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
            Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
            Assert.Equal(new System.Numerics.Vector3(-100, -100, 2000), geometry.A);
            Assert.NotNull(geometry.SourceDepths);
        }
        if (reverse)
        {
            Assert.Empty(visual.Batch.Attempts);
        }
        else
        {
            Assert.Contains(visual.Batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null);
        }
    }

    [Fact]
    public void Accepted_quad_uses_ordered_screen_prefix_sparse_tail_and_actual_depth_sidecars()
    {
        Fixture fixture = new(quad: true);
        NativeBattleDoom8Frame continuation = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Frame));
        NativeBattleFileStaging continuationStaging = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Staging));
        NativeBattleDoom8AppearanceStep result = fixture.Draw();
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.Null(result.Batch.Attempts[0].Primitive);
        NativeBattleGfGeometryAttempt packet = result.Batch.Attempts[1];
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
        Assert.Equal(142, primitive.Bucket);
        Assert.Equal(new int[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.Equal(0x09000000u, primitive.Words[0]);
        Assert.Equal(0x2C808080u, primitive.Words[1]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, primitive.PhysicalWriteMask!.Value[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[39]);
        Assert.Equal(0x2000028u, result.Frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.CursorAddress));
        Assert.Equal((uint?)500, result.Frame.Memory.GeometryRegisters!.Otz);
        Assert.Equal((uint?)0, result.Frame.Memory.ScriptedColorRegisters!.Flags);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(SnapshotJson(result), SnapshotJson(NativeBattleDoom8ReaperAppearance.Draw8CC7B0(continuation, continuationStaging, fixture.Model, fixture.Projection)));
    }

    [Fact]
    public void Unknown_or_partially_known_vertex_W_is_copied_without_zero_padding()
    {
        Fixture fixture = new(quad: false, knownW: false);
        fixture.Staging = fixture.Staging.Write(NativeBattleTemporaryMemory.Address + 0x2E, [0xAB]);
        NativeBattleDoom8AppearanceStep result = fixture.Draw();
        for (int vertex = 0; vertex < 3; vertex++)
        {
            int offset = checked((int)(NativeBattleDoom8ReaperAppearance.VertexAddress - NativeBattleDoom8State.BaseAddress)) + vertex * 8;
            (byte[] bytes, bool[] known) = result.Frame.State.Globals.ReadSnapshot(offset, 8);
            Assert.All(known.Take(7), value => Assert.True(value));
            Assert.Equal((byte)0xAB, bytes[6]);
            Assert.False(known[7]);
        }
        Assert.True(result.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal((byte)0xAB, result.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.Equal(32, result.Batch.AllocationBytes);
    }

    [Fact]
    public void Rejected_face_still_updates_age_mean_input_FIFO_NCLIP_and_flags()
    {
        Fixture fixture = new(quad: false, reverse: true);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.Write(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, [18]).WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x54, unchecked((ushort)-32768)) };
        NativeBattleDoom8AppearanceStep result = fixture.Draw();
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(new byte[] { 19 }, result.Frame.State.Read(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, 1));
        Assert.True(unchecked((int)result.Frame.Memory.GeometryRegisters!.Mac0!.Value) < 0);
        Assert.Equal((uint?)0, result.Frame.Memory.ScriptedColorRegisters!.Flags);
        Assert.Equal(0x2000000u, result.Frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.CursorAddress));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging, result.TemporaryAddress, 0x100);
        Assert.Equal((ushort)1, U16(temporary.Read(0xFE, 2)));
        Assert.Equal(result.Frame.Memory.GeometryRegisters.Mac0.Value, U32(temporary.Read(0xEC, 4)));
    }

    [Fact]
    public void First_face_negates_X_Z_displacement_and_does_not_advance_retained_angle()
    {
        Fixture fixture = new(quad: false);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.Write(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, [0]).WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x54, unchecked((ushort)-32768)) };
        NativeBattleDoom8AppearanceStep result = fixture.Draw();
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging, result.TemporaryAddress, 0x100);
        short dx = unchecked((short)(NativeBattleCameraMath.Sine(256) * (14 * 48) / 4096));
        short dz = unchecked((short)(NativeBattleCameraMath.Cosine(256) * (14 * 48) / 4096));
        Assert.Equal(unchecked((ushort)-dx), U16(temporary.Read(0xF8, 2)));
        Assert.Equal(unchecked((ushort)-dz), U16(temporary.Read(0xFC, 2)));
        Assert.Equal(unchecked((ushort)(-100 - dx)), U16(temporary.Read(0x54, 2)));
        Assert.Equal(unchecked((ushort)-dz), U16(temporary.Read(0x58, 2)));
        Assert.Equal((ushort)0, result.Frame.State.ReadWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x58));
        Assert.Equal(SnapshotJson(fixture.Frame.Random), SnapshotJson(result.Frame.Random));
    }

    [Fact]
    public void Signed_mean_truncates_toward_zero_before_the_threshold_comparison()
    {
        Fixture fixture = new(quad: false, y: [-2, 0, 0]);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.Write(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, [0]) };
        NativeBattleDoom8AppearanceStep result = fixture.Draw();
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging, result.TemporaryAddress, 0x100);
        Assert.Equal((ushort)0, U16(temporary.Read(0xF6, 2)));
        Assert.Equal(new byte[] { 1 }, result.Frame.State.Read(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, 1));
    }

    [Fact]
    public void Masked_object_does_not_consume_face_age_or_vertex_scratch_and_keeps_neighbor_globals()
    {
        Fixture fixture = new(quad: false);
        fixture.Model = fixture.Model with { Bytes = WriteDword(fixture.Model.Bytes, 0x7C, 0) };
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.Write(NativeBattleDoom8ReaperAppearance.VertexAddress, [0x91]).Write(0x275F980, [0x92]).Write(0x275F9D4, [0x93]) };
        NativeBattleDoom8AppearanceStep result = fixture.Draw();
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(new byte[] { 0x91 }, result.Frame.State.Read(NativeBattleDoom8ReaperAppearance.VertexAddress, 1));
        Assert.Equal(new byte[] { 0x92 }, result.Frame.State.Read(0x275F980, 1));
        Assert.Equal(new byte[] { 0x93 }, result.Frame.State.Read(0x275F9D4, 1));
        Assert.Equal(new byte[] { 19 }, result.Frame.State.Read(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, 1));
        Assert.Throws<NotSupportedException>(() => result.Frame.State.Read(NativeBattleDoom8ReaperAppearance.VertexAddress + 1, 1));
    }

    [Fact]
    public void Nonzero_linked_model_and_missing_bone_owner_fail_before_a_fallback()
    {
        Fixture fixture = new(quad: false);
        NativeBattleDoom8AppearanceModel independent = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(fixture.Model));
        NativeBattleDoom8AppearanceModel linked = independent with { Bytes = WriteDword(independent.Bytes, 0x78, 0x1234) };
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8ReaperAppearance.Draw8CC7B0(fixture.Frame, fixture.Staging, linked, fixture.Projection));
        fixture.Model = fixture.Model with { Bones = [] };
        Assert.Throws<InvalidDataException>(() => fixture.Draw());
    }

    private sealed class Fixture
    {
        public NativeBattleDoom8Frame Frame { get; set; }
        public NativeBattleFileStaging Staging { get; set; }
        public NativeBattleDoom8AppearanceModel Model { get; set; }
        public NativeBattleCameraProjection Projection { get; } = new(null, 100, 160, 108);

        public Fixture(bool quad, bool reverse = false, bool knownW = true, short[]? y = null)
        {
            const uint address = 0x275F2C8;
            Frame = NativeBattleDoom8ReaperAppearance.Initialize8CD020(NativeBattleDoom8ReaperAppearanceTests.Frame(), address);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            Frame = NativeBattleDoom8ReaperAppearance.PrepareCamera8CC6B0(Frame, NativeBattlePackedMatrix32.FromMatrix(identity, null), NativeBattlePackedMatrix32.FromMatrix(identity with { Z = 2000 }, null));
            Frame = Frame with { State = Frame.State.WriteDword(NativeBattleDoom8ReaperAppearance.CursorAddress, 0x2000000).WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5A, 256).Write(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, [19]) };
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
            if (knownW)
            {
                Staging = Staging.Write(NativeBattleTemporaryMemory.Address + 0x2E, [0xCD, 0xAB]);
            }
            NativeBattleEffectArena model = NativeBattleEffectArena.Clear().MarkUnknown(0, 128);
            model = WriteDword(model, 0x28, 0xFF332211);
            model = WriteDword(model, 0x78, 0);
            model = WriteDword(model, 0x7C, 1);
            short[] vertices = quad ? [-100, -100, 0, 100, -100, 0, -100, 100, 0, 100, 100, 0] : [-100, -100, 0, 100, -100, 0, -100, 100, 0];
            if (y is not null)
            {
                for (int index = 0; index < y.Length; index++)
                {
                    vertices[index * 3 + 1] = y[index];
                }
            }
            byte[] packed = new byte[vertices.Length * 2 + 6];
            for (int index = 0; index < vertices.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 2), vertices[index]);
            }
            byte[] polygon = Enumerable.Range(0, quad ? 20 : 16).Select(index => (byte)(index + 0x30)).ToArray();
            ushort[] indices = quad ? [0, 1, 2, 3] : reverse ? [0, 2, 1] : [0, 1, 2];
            for (int index = 0; index < indices.Length; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(index * 2), indices[index]);
            }
            ImmutableArray<NativeBattleRawVertex> typed = Enumerable.Range(0, vertices.Length / 3).Select(index => new NativeBattleRawVertex(new(vertices[index * 3], vertices[index * 3 + 1], vertices[index * 3 + 2]), 0)).ToImmutableArray();
            NativeBattleRawMesh mesh = new(typed, [new(quad, polygon.ToImmutableArray())]) { PackedVertexGroups = [new(0, (short)typed.Length, packed.ToImmutableArray())] };
            Model = new(address, model, [mesh], [NativeBattlePackedMatrix32.FromMatrix(identity, null)]);
        }

        public NativeBattleDoom8AppearanceStep Draw(NativeBattleDoom8Frame? frame = null)
        {
            return NativeBattleDoom8ReaperAppearance.Draw8CC7B0(frame ?? Frame, Staging, Model, Projection);
        }
    }

    private static NativeBattleEffectArena WriteDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static ushort U16(byte[] bytes) => BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
