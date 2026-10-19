using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl18ControllerTests
{
    [Fact]
    public void Original_script_draw_kind_is_decimal22_and_does_not_admit_the_unrelated_hex22_slot()
    {
        Assert.Equal(0xB4BBE0u, NativeBattleCinematicDispatch.RequireSlot(18, "draw_table", 22, 0xB4BBE0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(18, "draw_table", 0x22, 0xB4BBE0));
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -2)]
    public void Threshold_spawn_preserves_polygon_identity_and_distinct_signed_center_division(bool quad, short expected)
    {
        byte[] controls = Controls();
        ImmutableArray<NativeBattleVector> vertices = quad ? [new(-5, -1, 0), new(0, -1, 0), new(0, -1, 0), new(0, -1, 0)] : [new(-5, -1, 0), new(0, -1, 0), new(0, -1, 0)];
        NativeBattleControl18SpawnStep result = NativeBattleControl18Spawn.Advance(new byte[68], new byte[1], 0, controls, [new(0x1939020, quad, vertices)], 0, new(123));
        Assert.Equal((ushort)1, result.LiveCount);
        Assert.Equal((byte)1, result.PolygonStates[0]);
        Assert.Equal(0x1939020u, BinaryPrimitives.ReadUInt32LittleEndian(result.Records.AsSpan()));
        Assert.Equal(expected << 8, BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(4)));
        Assert.Equal((short)(-5 - expected), BinaryPrimitives.ReadInt16LittleEndian(result.Records.AsSpan().Slice(0x10)));
        NativeCrtRandomState random = new(123);
        for (int draw = 0; draw < 7; draw++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, result.Random);
    }

    [Fact]
    public void Full_record_bank_stops_before_consuming_random_or_touching_the_next_polygon()
    {
        byte[] records = new byte[68];
        BinaryPrimitives.WriteUInt32LittleEndian(records, 0x1234);
        NativeCrtRandomState random = new(12);
        NativeBattleControl18SpawnStep result = NativeBattleControl18Spawn.Advance(records, new byte[1], 1, Controls(), [new(0x1939020, false, [new(0, -1, 0), new(0, -1, 0), new(0, -1, 0)])], 0, random);
        Assert.Equal(random, result.Random);
        Assert.Equal((byte)0, result.PolygonStates[0]);
        Assert.Equal(records, result.Records.ToArray());
    }

    [Fact]
    public void Physics_wraps_word_velocity_and_expires_using_the_old_signed_life_byte()
    {
        byte[] records = new byte[68];
        BinaryPrimitives.WriteUInt32LittleEndian(records, 0x1234);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(0x3C), short.MaxValue);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(0x3E), 1);
        records[0x2E] = 0;
        NativeBattleControl18PhysicsStep result = NativeBattleControl18Physics.Advance(records, new byte[1], 1, 2);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Records.AsSpan()));
        Assert.Equal(short.MinValue << 4, BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan().Slice(4)));
        Assert.Equal((byte)255, result.Records[0x2E]);
        Assert.Equal((byte)255, result.PolygonStates[0]);
        Assert.Equal((ushort)0, result.LiveCount);
    }

    [Fact]
    public void Draw_only_mode_preserves_every_record_byte_and_live_count()
    {
        byte[] records = Enumerable.Range(0, 68).Select(value => (byte)value).ToArray();
        NativeBattleControl18PhysicsStep result = NativeBattleControl18Physics.Advance(records, [1], 2, 4);
        Assert.Equal(records, result.Records.ToArray());
        Assert.Equal((ushort)2, result.LiveCount);
    }

    private static byte[] Controls()
    {
        byte[] result = new byte[0x32];
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 2);
        result[0xC] = 15;
        return result;
    }
}
