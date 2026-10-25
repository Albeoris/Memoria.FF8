using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupMode5Tests
{
    [Fact]
    public void Initial_polar_records_use_the_anchor_and_centroids_divided_toward_zero()
    {
        NativeBattleControl18Face triangle = Face(false, new(8, 300, 0));
        NativeBattleControl18Face quad = new(0x2000, true, [new(-1, 100, 0), new(0, 200, 0), new(0, 300, 0), new(0, 400, 0)]);
        NativeBattleCinematicBreakupMode5Step result = NativeBattleCinematicBreakupMode5.Initialize(WaterSchedulerFixture.Create(), new(0, 123, 0), [triangle, quad]);
        Assert.Equal(new ushort[] { 1024, 8, 0, 0 }, Words(result.Anchors.AsSpan()));
        Assert.Equal((ushort)0, result.Frame.ReadTemporaryWord(0xD0));
        Assert.Equal((ushort)123, result.Frame.ReadTemporaryWord(0xD2));
        Assert.Equal((ushort)0, result.Frame.ReadTemporaryWord(0xD4));
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void Initial_radius_is_retained_as_a_native_word_even_when_it_exceeds_signed_range()
    {
        NativeBattleCinematicBreakupMode5Step result = NativeBattleCinematicBreakupMode5.Initialize(WaterSchedulerFixture.Create(), new(-20000, -7, 0), [Face(false, new(20000, 1000, 0))]);
        Assert.Equal(new ushort[] { 1024, 40000 }, Words(result.Anchors.AsSpan()));
        Assert.Equal(40000u, result.Frame.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void Selection_uses_the_retained_signed_radius_and_strict_threshold_instead_of_current_vertices()
    {
        NativeBattleControl18Face face = Face(false, new(3, -30000, 5));
        NativeCrtRandomState initial = new(123);
        NativeBattleControl18SpawnStep result = NativeBattleCinematicBreakupMode5.Advance(new byte[204], new byte[3], 0, Controls(0, 0), [face, face, face], 10, Anchors(11, 10, -1), initial, new(1, (_, _, _, _, _) => throw new InvalidOperationException()));
        Assert.Equal(new byte[] { 1, 0, 0 }, result.PolygonStates.ToArray());
        Assert.Equal((ushort)1, result.LiveCount);
        Assert.Equal(Advance(initial, 7), result.Random);
        Assert.Equal(3 << 8, BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(4)));
        Assert.Equal(-30000 << 8, BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(8)));
    }

    [Fact]
    public void Immediate_and_periodic_fallback_spawns_keep_native_emission_and_random_order()
    {
        NativeBattleControl18Face face = Face(false, new(3, 4, 5));
        NativeCrtRandomState initial = new(123);
        List<int> emissions = [];
        NativeBattleControl18SpawnServices services = new(2, (random, x, y, z, count) =>
        {
            Assert.Equal(Advance(initial, emissions.Count switch { 0 => 7, 1 => 62, _ => 110 }), random);
            Assert.Equal(3 << 8, x);
            Assert.Equal(4 << 8, y);
            Assert.Equal(5 << 8, z);
            emissions.Add(count);
            return Advance(random, count * 6);
        });
        NativeBattleControl18SpawnStep result = NativeBattleCinematicBreakupMode5.Advance(new byte[204], new byte[3], 0, Controls(), [face, face, face], 10, Anchors(11, 10, short.MinValue), initial, services);
        Assert.Equal(new[] { 8, 8, 8 }, emissions);
        Assert.Equal(new byte[] { 1, 0, 1 }, result.PolygonStates.ToArray());
        Assert.Equal((ushort)2, result.LiveCount);
        Assert.Equal(2, result.FallbackCountdown);
        Assert.Equal(Advance(initial, 158), result.Random);
    }

    [Fact]
    public void Flagged_faces_keep_their_polar_index_and_capacity_stops_before_more_spawns()
    {
        NativeBattleControl18Face face = Face(true, new(2, 3, 4));
        NativeCrtRandomState initial = new(123);
        NativeBattleControl18SpawnStep result = NativeBattleCinematicBreakupMode5.Advance(new byte[68], new byte[] { 255, 0, 0 }, 0, Controls(0, 0), [face, face, face], 10, Anchors(0, 11, 12), initial, new(1, (_, _, _, _, _) => throw new InvalidOperationException()));
        Assert.Equal(new byte[] { 255, 1, 0 }, result.PolygonStates.ToArray());
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(result.Records.AsSpan().Slice(0x30)));
        Assert.Equal(Advance(initial, 7), result.Random);
    }

    [Fact]
    public void Missing_polar_owner_and_wrong_control_mode_fail_before_spawning()
    {
        NativeBattleControl18Face face = Face(false, new(0, 0, 0));
        NativeBattleControl18SpawnServices services = new(1, (random, _, _, _, _) => random);
        Assert.Throws<NotSupportedException>(() => NativeBattleControl18Spawn.Advance(new byte[68], new byte[1], 0, Controls(), [face], 0, new(123), services));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBreakupMode5.Advance(new byte[68], new byte[1], 0, Controls(), [face], 0, [], new(123), services));
        byte[] controls = Controls();
        BinaryPrimitives.WriteUInt16LittleEndian(controls.AsSpan(8), 2);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicBreakupMode5.Advance(new byte[68], new byte[1], 0, controls, [face], 0, Anchors(1), new(123), services));
    }

    [Fact]
    public void Mode_five_physics_updates_vertical_velocity_angle_and_signed_lifetime_only()
    {
        byte[] record = new byte[68];
        BinaryPrimitives.WriteUInt32LittleEndian(record, 0x123456);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(4), 123);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8), -100);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(12), 456);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x16), short.MaxValue);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x1E), 1);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x32), short.MaxValue);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x34), 2);
        NativeBattleControl18PhysicsStep result = NativeBattleControl18Physics.Advance(record, new byte[] { 1 }, 1, 5);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Records.AsSpan()));
        Assert.Equal(123, BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(4)));
        Assert.Equal(unchecked(-100 + (short.MinValue << 4)), BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(8)));
        Assert.Equal(456, BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(12)));
        Assert.Equal(unchecked((short)32769), BinaryPrimitives.ReadInt16LittleEndian(result.Records.AsSpan().Slice(0x32)));
        Assert.Equal((byte)255, result.Records[0x2E]);
        Assert.Equal((ushort)0, result.LiveCount);
        Assert.Equal(new byte[] { 255 }, result.PolygonStates.ToArray());
    }

    [Fact]
    public void Mode_five_auxiliary_initialization_preserves_unknown_record_fields()
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, basis + 0x10000);
        frame = frame with { Arena = frame.Arena.Write(0x4008, BitConverter.GetBytes((ushort)5)).Write(0x402E, BitConverter.GetBytes((ushort)2)).MarkUnknown(0x10000, 56) };
        frame = NativeBattleCinematicBreakupAuxiliary.Initialize(frame, basis + 0x5000, basis + 0x4000);
        var snapshot = frame.Arena.ReadSnapshot(0x10000, 56);
        Assert.Equal(new[] { 24, 52 }, Enumerable.Range(0, 56).Where(index => snapshot.Known[index]));
        Assert.Equal(basis + 0x10000, WaterSchedulerFixture.ReadSlotDword(frame, 0, 0xA0));
        Assert.Equal(basis + 0x10038, WaterSchedulerFixture.ReadCoreDword(frame, 0x2797704));
    }

    private static NativeBattleControl18Face Face(bool quad, NativeBattleVector point)
    {
        return new(0x123456, quad, Enumerable.Repeat(point, quad ? 4 : 3).ToImmutableArray());
    }

    private static byte[] Controls(ushort capacity = 1024, ushort emissions = 8)
    {
        byte[] result = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 5);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x2E), capacity);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x30), emissions);
        result[0xC] = 15;
        return result;
    }

    private static byte[] Anchors(params short[] distances)
    {
        byte[] result = new byte[distances.Length * 4];
        for (int index = 0; index < distances.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 4 + 2), distances[index]);
        }
        return result;
    }

    private static ushort[] Words(ReadOnlySpan<byte> bytes)
    {
        ushort[] result = new ushort[bytes.Length / 2];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(index * 2));
        }
        return result;
    }

    private static NativeCrtRandomState Advance(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
