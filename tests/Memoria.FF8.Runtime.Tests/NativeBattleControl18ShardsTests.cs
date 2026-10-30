using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl18ShardsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Effect77_shards_keep_signed_Q8_corners_native_flag_payload_and_record_birth(bool quad)
    {
        byte[] records = [.. new byte[68], .. Record(quad)];
        BinaryPrimitives.WriteInt32LittleEndian(records.AsSpan(68 + 4), 32767 << 8);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(68 + 16), 1);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeBattlePilotVisualLifetimes.Allocated(arena, 0x2000, 1);
        NativeBattlePilotVisualIdentity Identity(int slot) => NativeBattlePilotVisualLifetimes.Identity(77, arena, 0x2000, slot, 3) with { ResourceGeneration = 17 };
        NativeBattleModuleProjectedVertex Project(NativeBattleVector point) => new(0, 4096, 0x80004000);
        System.Numerics.Vector3 World(NativeBattleVector point) => new(point.X, point.Y, point.Z);
        NativeBattleGfGeometryBatch plain = NativeBattleControl18Shards.Emit(records, Polygon, new uint[] { 0, 0 }, Project, 17);
        NativeBattleGfGeometryBatch captured = NativeBattleControl18Shards.Emit(records, Polygon, new uint[] { 0, 0 }, Project, 17, World, Identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry first = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.NotNull(first);
        Assert.Equal(new System.Numerics.Vector3(-32768, 0, 0), first.A);
        Assert.Equal(1, first.Identity.Slot);
        NativeBattleGfGeometryBatch depths = NativeBattleCinematicActorPresentation.NativeDepths(captured);
        Assert.Equal(new System.Numerics.Vector4(0x10000, 0x10000, 0x10000, quad ? 0x10000 : 0), Assert.Single(depths.Attempts).Primitive!.PilotGeometry!.SourceDepths);
        NativeBattlePilotVisualLifetimes.Allocated(arena, 0x2000, 1);
        NativeBattleGfGeometryBatch reused = NativeBattleControl18Shards.Emit(records, Polygon, new uint[] { 0, 0 }, Project, 17, World, Identity);
        Assert.NotEqual(first.Identity.Generation, Assert.Single(reused.Attempts).Primitive!.PilotGeometry!.Identity.Generation);
        Assert.Equal(NativeBattleTestSnapshots.Json(captured), NativeBattleTestSnapshots.Json(reused));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_UV_mapping_TPAGE_bit_and_partial_packet_stores_are_distinct_for_triangles_and_quads(bool quad)
    {
        byte[] record = Record(quad);
        int corner = 0;
        NativeBattleModuleProjectedVertex Project(NativeBattleVector point)
        {
            ushort[] depths = [4000, 4017, 4095, 4200];
            uint[] flags = [0x80004000, 0x80002000, 0x80000000, 0x1000];
            int index = corner++;
            return new((uint)(index + 1), depths[index], flags[index]);
        }
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Shards.Emit(record, Polygon, new uint[] { 0x112233, 0x445566 }, Project, 17);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(quad ? 40 : 32, batch.AllocationBytes);
        Assert.Equal(quad ? (byte)9 : (byte)7, attempt.Data[3]);
        Assert.Equal(quad ? 0x2E445566u : 0x26445566u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(4, 4)));
        Assert.Equal((ushort)0x0908, Word(attempt, 12));
        Assert.Equal((ushort)0x0B0A, Word(attempt, 14));
        Assert.Equal((ushort)0x0D0C, Word(attempt, 20));
        Assert.Equal((ushort)0x0F2E, Word(attempt, 22));
        Assert.Equal(quad ? (ushort)0x1110 : (ushort)0x0706, Word(attempt, 28));
        Assert.All(attempt.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        if (quad)
        {
            Assert.Equal((ushort)0x1312, Word(attempt, 36));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        }
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(17 + (quad ? 254 : 252), primitive.Bucket);
        Assert.Equal(new[] { 0x10000, 0x8000, 0, quad ? 0x4000 : 0 }, primitive.VertexPayload4);
    }

    [Fact]
    public void Center_is_arithmetic_Q8_before_signed_WORD_addition_and_inactive_records_do_not_project_or_read_polygons()
    {
        byte[] records = new byte[136];
        byte[] live = Record(false);
        BinaryPrimitives.WriteInt32LittleEndian(live.AsSpan(4), 32767 << 8);
        BinaryPrimitives.WriteInt32LittleEndian(live.AsSpan(8), -257);
        BinaryPrimitives.WriteInt32LittleEndian(live.AsSpan(12), 511);
        BinaryPrimitives.WriteInt16LittleEndian(live.AsSpan(16), 1);
        BinaryPrimitives.WriteInt16LittleEndian(live.AsSpan(18), 1);
        BinaryPrimitives.WriteInt16LittleEndian(live.AsSpan(20), -2);
        live.CopyTo(records, 68);
        List<NativeBattleVector> points = [];
        NativeBattleControl18Shards.Emit(records, Polygon, new uint[] { 0, 0 }, point =>
        {
            points.Add(point);
            return new(0, 4096, 0);
        }, 17);
        Assert.Equal(3, points.Count);
        Assert.Equal(new NativeBattleVector(-32768, -1, -1), points[0]);
    }

    [Fact]
    public void Rejected_depth_still_writes_and_allocates_both_packet_sizes_without_FLAG_or_area_culling()
    {
        byte[] records = [.. Record(false), .. Record(true)];
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Shards.Emit(records, Polygon, new uint[] { 0, 0 }, _ => new(0, 65535, uint.MaxValue), 17);
        Assert.Equal(72, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 32 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        NativeBattleGfGeometryBatch clipped = NativeBattleControl18Shards.Emit(Record(false), Polygon, new uint[] { 0, 0 }, _ => new(0, 4096, uint.MaxValue), 17);
        Assert.NotNull(Assert.Single(clipped.Attempts).Primitive);
    }

    private static byte[] Record(bool quad)
    {
        byte[] record = new byte[68];
        BinaryPrimitives.WriteUInt32LittleEndian(record, 0x1234);
        record[0x26] = quad ? (byte)1 : (byte)0;
        record[0x2E] = 1;
        return record;
    }
    private static ImmutableArray<byte> Polygon(uint pointer, int length)
    {
        Assert.Equal(0x1234u, pointer);
        return Enumerable.Range(0, length).Select(index => (byte)index).ToImmutableArray();
    }
    private static ushort Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan(offset, 2));
}
