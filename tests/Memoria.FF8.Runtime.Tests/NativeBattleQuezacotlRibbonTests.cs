using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlRibbonTests
{
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 216, 160, 108);

    [Theory]
    [InlineData(1, 1234)]
    [InlineData(4, 2468)]
    [InlineData(64, 9872)]
    public void Square_root_uses_original_normalized_table_index_and_native_shift(int squared, int expected)
    {
        ushort[] table = new ushort[256];
        table[64] = 1234;
        Assert.Equal(expected, NativeBattleQuezacotlRibbonMath.SqrtQ12(squared, table.ToImmutableArray()));
    }

    [Fact]
    public void Triangle_uses_three_signed_bone_selectors_native_centroid_rounding_and_cross_normal()
    {
        List<int> selectors = [];
        NativeBattleQuezacotlRibbonSample sample = NativeBattleQuezacotlRibbonMath.Triangle6C9100(Triangles(), 0, selector =>
        {
            selectors.Add(selector);
            return Identity;
        }, SqrtFixture());
        Assert.Equal(new[] { -2, -2, -2 }, selectors);
        Assert.Equal(new NativeBattleVector(5, 5, 0), sample.Position);
        Assert.Equal(new NativeBattleVector(0, 0, -4096), sample.Normal);
    }

    [Theory]
    [InlineData(0, 5)]
    // 6C8FF7/6C900C round each product separately: 5/2 -> 2 and 261/2 -> 130 under027F, then add.
    [InlineData(2048, 132)]
    [InlineData(4096, 261)]
    public void Endpoint_frame_interpolation_uses_Q12_phase_and_does_not_read_beyond_an_exact_last_frame(short progress, short expectedX)
    {
        NativeBattleQuezacotlRibbonSample sample = NativeBattleQuezacotlRibbonMath.Sample6C8F70(Triangles(), 0, 2, progress, _ => Identity, SqrtFixture());
        Assert.Equal(expectedX, sample.Position.X);
        Assert.Equal(new NativeBattleVector(0, 0, -4096), sample.Normal);
    }

    [Fact]
    public void Missing_lookup_and_zero_normal_are_explicit_boundaries()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlRibbonMath.SqrtQ12(1, []));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlRibbonMath.Normalize(0, 0, 0, SqrtFixture(), false));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlRibbonMath.Triangle6C9100(Triangles(), -1, _ => Identity, SqrtFixture()));
    }

    [Fact]
    public void Fifteen_GT4_packets_preserve_fixed_material_padding_and_last_four_projection_depth_payloads()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlRibbonPackets.Emit6C8C50(Points(), Identity, Projection, DistanceFixture());
        Assert.Equal(780, batch.AllocationBytes);
        Assert.Equal(15, batch.Attempts.Length);
        Assert.Equal(Enumerable.Range(0, 15).Select(index => index * 52), batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGeometryPrimitive first = batch.Attempts[0].Primitive!;
        Assert.Equal(0x0C000000u, first.Words[0]);
        Assert.Equal(0x3E010101u, first.Words[1]);
        Assert.Equal(0x3E94C008u, first.Words[3]);
        Assert.Equal(0x00B6C038u, first.Words[6]);
        Assert.Equal(0xC008u, first.Words[9]);
        Assert.Equal(0xC038u, first.Words[12]);
        Assert.Equal(32, first.Bucket);
        Assert.Equal(36, batch.Attempts[14].Primitive!.Bucket);
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.Equal(new[] { 1192, 1208, 1224, 1240 }, attempt.Primitive!.VertexPayload4);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[50]);
        });
    }

    [Fact]
    public void Ribbon_pushes_all_sixteen_actual_full_depths_even_when_every_segment_is_behind_the_camera()
    {
        NativeBattleActionMatrix behind = Identity with { Z = -10000 };
        List<ushort> depths = [];
        NativeBattleGteDepthTrace trace = new([111, 222, 333, 444]);
        NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlRibbonPackets.Emit6C8C50(Points(), behind, Projection, DistanceFixture(), depth =>
        {
            depths.Add(depth);
            trace.Push(depth);
        });
        Assert.Equal(16, depths.Count);
        Assert.All(depths, depth => Assert.Equal((ushort)0, depth));
        Assert.Equal(new int?[] { 0, 0, 0, 0 }, trace.DepthFifo4);
        Assert.Empty(batch.Attempts);
    }

    [Fact]
    public void Degenerate_first_direction_keeps_uninitialized_scratch_strict_and_missing_distance_table_is_rejected()
    {
        ImmutableArray<NativeBattleQuezacotlRibbonPoint> points = Enumerable.Repeat(new NativeBattleQuezacotlRibbonPoint(new(0, 0, 1000), 64, 128), 16).ToImmutableArray();
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlRibbonPackets.Emit6C8C50(points, Identity, Projection, DistanceFixture()));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlRibbonPackets.Emit6C8C50(Points(), Identity, Projection, []));
    }

    [Theory]
    [InlineData(0x001A5020u, 0x03650517u, 0x035B04E9u)]
    [InlineData(0xFFFF0001u, 0x035F0500u, 0x03610500u)]
    public void Degenerate_first_segment_reads_both_signed_halves_of_the_explicit_direction_before_replacing_it(uint direction, uint expectedLeft, uint expectedRight)
    {
        ImmutableArray<NativeBattleQuezacotlRibbonPoint> points = Enumerable.Range(0, 16).Select(index => new NativeBattleQuezacotlRibbonPoint(new((short)(Math.Max(index - 1, 0) * 20), 0, 1000), 64, 128)).ToImmutableArray();
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlRibbonPackets.Emit6C8C50(points, Identity, Projection, DistanceFixture()));
        NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlRibbonPackets.Emit6C8C50(points, Identity, Projection, DistanceFixture(), incomingDirectionDword: direction);
        Assert.Equal(14, batch.Attempts.Length);
        Assert.Equal(14 * 52, batch.AllocationBytes);
        NativeBattleGeometryPrimitive first = batch.Attempts[0].Primitive!;
        Assert.Equal(expectedLeft, first.Words[2]);
        Assert.Equal(expectedRight, first.Words[5]);
        //After the prior read, the leaf writes the actual next-segment direction; point2 no longer uses the incoming DWORD.
        Assert.Equal(0x035E0523u, first.Words[8]);
        Assert.Equal(0x03620523u, first.Words[11]);
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.Equal(new[] { 1000, 1000, 1000, 1000 }, attempt.Primitive!.VertexPayload4);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[50]);
        });
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(batch))));
    }

    [Fact]
    public void Callback_forwards_the_explicit_stack_bytes_without_extra_random_draws_or_task_history_replay()
    {
        byte[] pairs = new byte[80];
        BinaryPrimitives.WriteInt16LittleEndian(pairs.AsSpan(2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(pairs.AsSpan(6), 1);
        NativeBattleQuezacotlRibbonResources resources = new(pairs.ToImmutableArray(), Triangles(), SqrtFixture(), DistanceFixture());
        ImmutableArray<short> envelope = Enumerable.Repeat((short)0, 16).ToImmutableArray();
        ImmutableArray<byte> task = Task(0, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlRibbon.Advance6C8850(task, resources, 0, _ => Identity with { Z = 1000 }, envelope, 7, false, Identity, Projection));
        NativeBattleQuezacotlRibbonStep actual = NativeBattleQuezacotlRibbon.Advance6C8850(task, resources, 0, _ => Identity with { Z = 1000 }, envelope, 7, false, Identity, Projection, incomingDirectionDword: 0x1A5020);
        ImmutableArray<byte> restoredTask = JsonSerializer.Deserialize<ImmutableArray<byte>>(JsonSerializer.Serialize(task));
        NativeBattleQuezacotlRibbonStep independent = NativeBattleQuezacotlRibbon.Advance6C8850(restoredTask, resources, resources.PrimaryBank, 0, _ => Identity with { Z = 1000 }, envelope, 7, false, Identity, Projection, incomingDirectionDword: 0x1A5020);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(independent));
        ushort random = 7;
        for (int index = 0; index < 42; index++)
        {
            random = (ushort)((125 * random + 14) & 0x7FFF);
        }
        Assert.Equal(random, actual.Random17A0);
        Assert.Equal(14, actual.CommonPackets.Attempts.Length);
        Assert.Equal(256, ReadWord(actual.Task128, 0x0E));
        Assert.Equal(0, ReadWord(task, 0x0E));
        Assert.Equal(0, actual.Returned);
    }

    [Fact]
    public void Pause_stops_task_progress_but_retains_drawing_and_exactly_forty_two_private_random_draws()
    {
        ImmutableArray<byte> task = Task(4096, 9);
        NativeBattleQuezacotlRibbonStep paused = Step(task, true);
        NativeBattleQuezacotlRibbonStep running = Step(task, false);
        Assert.Equal(task, paused.Task128);
        Assert.All(paused.TaskWrites.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        ushort expected = 7;
        for (int index = 0; index < 42; index++)
        {
            expected = (ushort)((125 * expected + 14) & 0x7FFF);
        }
        Assert.Equal(expected, paused.Random17A0);
        Assert.Equal(expected, running.Random17A0);
        Assert.Equal(780, paused.CommonPackets.AllocationBytes);
        Assert.Equal(JsonSerializer.Serialize(paused.CommonPackets), JsonSerializer.Serialize(running.CommonPackets));
        Assert.Equal(0, paused.Returned);
        Assert.Equal(2, running.Returned);
    }

    [Fact]
    public void Equality_at_4096_only_writes_progress_and_fade_starts_after_it_is_exceeded()
    {
        NativeBattleQuezacotlRibbonStep result = Step(Task(3840, 9), false);
        Assert.Equal(4096, ReadWord(result.Task128, 0x0E));
        Assert.Equal(9, ReadWord(result.Task128, 0x0C));
        Assert.Equal(2, result.TaskWrites.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Equal(0, result.Returned);
        NativeBattleQuezacotlRibbonStep faded = Step(result.Task128, false);
        Assert.Equal(4096, ReadWord(faded.Task128, 0x0E));
        Assert.Equal(10, ReadWord(faded.Task128, 0x0C));
        Assert.Equal(2, faded.Returned);
        for (int index = 0; index < 16; index++)
        {
            Assert.Equal(64, ReadWord(faded.Task128, 0x20 + index * 6));
            Assert.Equal(0, ReadWord(faded.Task128, 0x22 + index * 6));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, faded.TaskWrites.WriteMask[0x24 + index * 6]);
        }
        Assert.Equal(36, faded.TaskWrites.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
    }

    [Fact]
    public void Progress_addition_has_original_signed_WORD_wrap_without_a_synthetic_completion()
    {
        byte[] task = Task(32760, 9).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(0x10), 16);
        NativeBattleQuezacotlRibbonStep result = Step(task.ToImmutableArray(), false);
        Assert.Equal(-32760, ReadWord(result.Task128, 0x0E));
        Assert.Equal(9, ReadWord(result.Task128, 0x0C));
        Assert.Equal(0, result.Returned);
    }

    [Fact]
    public void Task_and_packet_receipts_survive_JSON_for_independent_next_callback()
    {
        NativeBattleQuezacotlRibbonStep first = Step(Task(0, 0), false);
        NativeBattleQuezacotlRibbonStep restored = JsonSerializer.Deserialize<NativeBattleQuezacotlRibbonStep>(JsonSerializer.Serialize(first))!;
        NativeBattleQuezacotlRibbonStep a = NativeBattleQuezacotlRibbon.Advance6C8850(first.Task128, Resources(), 0, _ => Identity with { Z = 1000 }, Envelope(), first.Random17A0, false, Identity, Projection);
        NativeBattleQuezacotlRibbonStep b = NativeBattleQuezacotlRibbon.Advance6C8850(restored.Task128, Resources(), 0, _ => Identity with { Z = 1000 }, Envelope(), restored.Random17A0, false, Identity, Projection);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    private static NativeBattleQuezacotlRibbonStep Step(ImmutableArray<byte> task, bool paused)
    {
        return NativeBattleQuezacotlRibbon.Advance6C8850(task, Resources(), 0, _ => Identity with { Z = 1000 }, Envelope(), 7, paused, Identity, Projection);
    }

    private static NativeBattleQuezacotlRibbonResources Resources()
    {
        byte[] pairs = new byte[80];
        BinaryPrimitives.WriteInt16LittleEndian(pairs.AsSpan(2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(pairs.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(pairs.AsSpan(6), 1);
        return new(pairs.ToImmutableArray(), Triangles(), SqrtFixture(), DistanceFixture());
    }

    private static ImmutableArray<byte> Task(short progress, short lifetime)
    {
        byte[] task = Enumerable.Repeat((byte)0xAA, 128).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(0x0C), lifetime);
        BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(0x0E), progress);
        BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(0x10), 256);
        task[0x12] = 12;
        task[0x13] = 7;
        for (int index = 0; index < 16; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(0x20 + index * 6), 64);
            BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(0x22 + index * 6), 1);
        }
        return task.ToImmutableArray();
    }

    private static ImmutableArray<short> Envelope()
    {
        return Enumerable.Repeat((short)256, 16).ToImmutableArray();
    }

    private static ImmutableArray<byte> Triangles()
    {
        byte[] source = new byte[48];
        for (int frame = 0; frame < 2; frame++)
        {
            short[] points = [(short)(frame * 256), 0, 0, -2, (short)(frame * 256 + 16), 0, 0, -2, (short)(frame * 256), 16, 0, -2];
            for (int index = 0; index < points.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(frame * 24 + index * 2), points[index]);
            }
        }
        return source.ToImmutableArray();
    }

    private static ImmutableArray<NativeBattleQuezacotlRibbonPoint> Points()
    {
        return Enumerable.Range(0, 16).Select(index => new NativeBattleQuezacotlRibbonPoint(new((short)(index * 20), 0, (short)(1000 + index * 16)), 64, (short)(index + 1))).ToImmutableArray();
    }

    private static ImmutableArray<ushort> SqrtFixture()
    {
        // Controlled test table; installed callbacks bind the authenticated original resource instead.
        return Enumerable.Range(0, 256).Select(value => (ushort)Math.Floor(Math.Sqrt(value) * 512)).ToImmutableArray();
    }

    private static ImmutableArray<byte> DistanceFixture()
    {
        // Controlled test table for the original indexing contract, never a production fallback.
        return Enumerable.Range(0, 16384).Select(value => (byte)Math.Floor(Math.Sqrt((value & 127) * (value & 127) + (value >> 7) * (value >> 7)))).ToImmutableArray();
    }

    private static short ReadWord(ImmutableArray<byte> task, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan().Slice(offset));
    }
}
