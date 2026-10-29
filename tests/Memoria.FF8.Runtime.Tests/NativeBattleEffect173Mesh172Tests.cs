using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect173Mesh172Tests
{
    [Fact]
    public void Active_first_age_extrapolates_DPCT_and_keeps_native_GT3_layout_and_depth_fifo()
    {
        NativeBattleEffect173Mesh172Step result = Draw(moving: true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(0x09000000u, attempt.Primitive!.Words[0]);
        Assert.Equal(0x34FF8040u, attempt.Primitive.Words[1]);
        Assert.Equal(0x12340102u, attempt.Primitive.Words[3]);
        Assert.Equal(0x00450304u, attempt.Primitive.Words[6]);
        Assert.Equal(0x0506u, attempt.Primitive.Words[9]);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Equal(new[] { 777, 2000, 2000, 2000 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(unchecked((uint)-4096), U(result.Context172, 12));
        Assert.Equal((short)1, S(result.Records, 14));
        Assert.DoesNotContain(true, result.Records.ReadSnapshot(22, 2).Known);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_projection_or_zero_area_retains_raw_packet_stores_before_color_math(bool zeroArea)
    {
        NativeBattleEffect173Mesh172Step result = Draw(moving: false, phase: 12, z: zeroArea ? 2000 : 0, degenerate: zeroArea);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0x34804020u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.All(attempt.WriteMask.Skip(36).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.DoesNotContain(true, result.Context172.ReadSnapshot(0x38, 4).Known);
        if (!zeroArea)
        {
            Assert.DoesNotContain(true, result.Context172.ReadSnapshot(0x30, 4).Known);
        }
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, -1, 0)]
    public void Skipped_delay_and_retired_record_do_not_load_geometry_or_emit_packets(bool moving, short recordAge, int phase)
    {
        NativeBattleEffectArena record = Records().Write(14, BitConverter.GetBytes(recordAge)).Write(6, BitConverter.GetBytes((short)0));
        NativeBattleEffect173Mesh172Step result = Draw(moving, phase, records: record);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Memory().GeometryRegisters)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result.Memory.GeometryRegisters)));
        Assert.Equal(record.ReadSnapshot(0, 32).Data, result.Records.ReadSnapshot(0, 32).Data);
    }

    [Fact]
    public void Paused_shard_retains_record_motion_but_projects_old_position_with_current_angle()
    {
        NativeBattleEffectArena records = Records().Write(14, BitConverter.GetBytes((short)2)).Write(16, BitConverter.GetBytes((short)50));
        NativeBattleEffect173Mesh172Step result = Draw(true, records: records, paused: true);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(records.ReadSnapshot(0, 32).Data, result.Records.ReadSnapshot(0, 32).Data);
        Assert.Equal(records.ReadSnapshot(0, 32).Known, result.Records.ReadSnapshot(0, 32).Known);
        Assert.Equal(512u, U(result.Context172, 12));
    }

    [Fact]
    public void Sixteenth_active_age_draws_then_retires_and_round_trip_preserves_partial_records()
    {
        NativeBattleEffectArena records = Records().Write(14, BitConverter.GetBytes((short)15));
        NativeBattleEffect173Mesh172Step result = Draw(true, records: records);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal((short)-1, S(result.Records, 14));
        NativeBattleEffect173Mesh172Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect173Mesh172Step>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(result.Records.ReadSnapshot(0, 32).Known, restored.Records.ReadSnapshot(0, 32).Known);
        Assert.DoesNotContain(true, restored.Context172.ReadSnapshot(0x8C + 18, 2).Known);
        Assert.Empty(Draw(true, records: restored.Records).Batch.Attempts);
    }

    [Fact]
    public void Centroid_initializer_consumes_one_random_per_face_and_preserves_unwritten_record_padding()
    {
        NativeBattleStaticMeshResource mesh = Mesh();
        NativeBattleEffect173Frame frame = new(Memory(), new(173, 172, new(173, 0x5D74B0, 0x5D7490), null, null) { Pools = [] }, new(345), NativeBattleEffect173State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = frame with { State = frame.State.WriteView(0x2398AA8, frame.State.ReadView(0x2398AA8, 32).MarkUnknown(22, 2), 32) };
        (frame, uint temporary) = Allocate(frame, 172);
        NativeBattleEffectArena context = Context(frame, temporary, 172).Write(0, BitConverter.GetBytes(mesh.Address)).Write(0x24, BitConverter.GetBytes(0u));
        frame = PublishContext(frame, temporary, context, 172);
        NativeBattleEffect173Resources resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, null!, [], [mesh.Address], [0x2398AA8], 0, [], [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        NativeCrtRandomState expected = frame.Random.Draw(out ushort random);
        NativeBattleEffect173Frame result = NativeBattleEffect173Lower.InitializeCentroids7980(frame, temporary, resources);
        Assert.Equal(expected, result.Random);
        NativeBattleEffectArena record = result.State.ReadView(0x2398AA8, 32);
        Assert.Equal((short)-33, S(record, 0));
        Assert.Equal((short)-33, S(record, 2));
        Assert.Equal((ushort)((random & 7) << 4), unchecked((ushort)S(record, 26)));
        Assert.Equal((ushort)(((random >> 3) & 7) << 3), unchecked((ushort)S(record, 30)));
        Assert.DoesNotContain(true, record.ReadSnapshot(22, 2).Known);
        Assert.Equal(0x2398AA8u + 32, U(Context(result, temporary, 172), 0x28));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_retains_actual_moving_centroid_corners_native_fifo_and_all_writes(bool paused)
    {
        NativeBattleEffect173Mesh172Step plain = Draw(true, records: MovingRecord(), paused: paused);
        NativeBattleEffect173Mesh172Step captured = Draw(true, records: MovingRecord(), paused: paused, pilotIdentity: new(173, 0x100, 0, 123, 7));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context172), NativeBattleTestSnapshots.Json(captured.Context172));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Records), NativeBattleTestSnapshots.Json(captured.Records));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Batch), NativeBattleTestSnapshots.Json(captured.Batch));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-95, -94, 2007), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(105, -94, 2007), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(-95, 106, 2007), geometry.C);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector4(777, 2007, 2007, 2007), geometry.SourceDepths);

        static NativeBattleEffectArena MovingRecord() => Records().Write(0, BitConverter.GetBytes((short)20)).Write(2, BitConverter.GetBytes((short)30)).Write(4, BitConverter.GetBytes((short)40)).Write(8, BitConverter.GetBytes((short)5)).Write(10, BitConverter.GetBytes((short)6)).Write(12, BitConverter.GetBytes((short)7));
    }

    [Fact]
    public void Presentation_retains_rejected_native_partial_writes()
    {
        NativeBattleEffect173Mesh172Step plain = Draw(false, phase: 12, z: 0);
        NativeBattleEffect173Mesh172Step captured = Draw(false, phase: 12, z: 0, pilotIdentity: new(173, 0x100, 0, 123, 7));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        Assert.Null(Assert.Single(captured.Batch.Attempts).Primitive);
    }

    private static NativeBattleEffect173Mesh172Step Draw(bool moving, int phase = 0, int z = 2000, bool degenerate = false, NativeBattleEffectArena? records = null, bool paused = false, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleStaticMeshResource mesh = Mesh(degenerate);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 172).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0x30u)).Write(0x20, BitConverter.GetBytes(phase));
        context = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(Identity(z), null)), 0, context, 0x6C, 32);
        return NativeBattleEffect173Mesh172.DrawLoaded(Memory(), mesh, context, records ?? Records(), 0x2398AA8, new(Identity(0), 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, moving, paused, pilotIdentity: pilotIdentity);
    }
    private static NativeBattleEffectArena Records() => NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, new byte[22]).Write(24, new byte[8]).Write(6, BitConverter.GetBytes((short)-1));
    internal static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity(2000), true, true, null) with { Sz = [11, 22, 33, 777] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    internal static NativeBattleActionMatrix Identity(int z) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
    private static NativeBattleStaticMeshResource Mesh(bool degenerate = false)
    {
        const int polygon = 32;
        byte[] bytes = new byte[polygon + 32 + 28];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        (short X, short Y)[] vertices = [(-100, -100), (100, -100), (-100, 100)];
        for (int row = 0; row < 3; row++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + row * 8), vertices[row].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + row * 8), vertices[row].Y);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(polygon + 24), 1);
        int face = polygon + 28;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x34804020);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), degenerate ? (ushort)0 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), degenerate ? (ushort)0 : (ushort)4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0x0506);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12340102);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x00450304);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x00804020);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x00804020);
        return new(0, 0x100000, bytes.ToImmutableArray());
    }
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
