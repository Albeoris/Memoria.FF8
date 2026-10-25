using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupMode7Tests
{
    [Theory]
    [InlineData(1, 8, 7)]
    [InlineData(-1, 16, 7)]
    [InlineData(-1, 8, 2)]
    [InlineData(1, 16, 2)]
    public void Signed_threshold_modes_and_periodic_fallback_emit_after_seven_fragment_draws(short y, int emissions, ushort mode)
    {
        NativeCrtRandomState initial = new(123);
        NativeCrtRandomState afterFragment = Advance(initial, 7);
        int calls = 0;
        NativeBattleControl18SpawnServices services = new(1, (random, x, fixedY, z, count) =>
        {
            Assert.Equal(Advance(afterFragment, calls * 48), random);
            calls++;
            Assert.Equal(3 << 8, x);
            Assert.Equal(y << 8, fixedY);
            Assert.Equal(5 << 8, z);
            Assert.Equal(8, count);
            return Advance(random, count * 6);
        });
        NativeBattleControl18SpawnStep result = NativeBattleControl18Spawn.Advance(new byte[68], new byte[1], 0, Controls(mode), [new(0x123456, false, [new(3, y, 5), new(3, y, 5), new(3, y, 5)])], 0, initial, services);
        Assert.Equal(emissions / 8, calls);
        Assert.Equal((ushort)1, result.LiveCount);
        Assert.Equal((byte)1, result.PolygonStates[0]);
        Assert.Equal(1, result.FallbackCountdown);
        Assert.Equal(Advance(initial, 7 + emissions * 6), result.Random);
    }

    [Fact]
    public void Equal_threshold_decrements_fallback_only_for_unflagged_faces_and_resets_after_emission()
    {
        NativeBattleControl18Face face = new(0x123456, false, [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)]);
        int calls = 0;
        NativeBattleControl18SpawnStep result = NativeBattleControl18Spawn.Advance(new byte[136], new byte[] { 0, 255, 0, 0 }, 0, Controls(), [face, face, face, face], 0, new(123), new(2, (random, _, _, _, count) =>
        {
            calls++;
            Assert.Equal(8, count);
            return Advance(random, 6 * count);
        }));
        Assert.Equal(2, calls);
        Assert.Equal(new byte[] { 0, 255, 1, 0 }, result.PolygonStates.ToArray());
        Assert.Equal(1, result.FallbackCountdown);
    }

    [Fact]
    public void Mode_seven_physics_changes_only_vertical_motion_angle_and_lifetime()
    {
        byte[] record = new byte[68];
        BinaryPrimitives.WriteUInt32LittleEndian(record, 0x123456);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(4), 100);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8), -100);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(12), 300);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x16), short.MaxValue);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x1E), 1);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x32), 32760);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(0x34), 10);
        record[0x2E] = 1;
        NativeBattleControl18PhysicsStep result = NativeBattleControl18Physics.Advance(record, new byte[] { 1 }, 1, 7);
        byte[] expected = (byte[])record.Clone();
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(0x16), short.MinValue);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), unchecked(-100 + (short.MinValue << 4)));
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(0x32), unchecked((short)32770));
        expected[0x2E] = 0;
        Assert.Equal(expected, result.Records.ToArray());
        Assert.Equal((ushort)1, result.LiveCount);
        Assert.Equal(new byte[] { 1 }, result.PolygonStates.ToArray());
    }

    private static byte[] Controls(ushort mode = 7)
    {
        byte[] result = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), mode);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x2E), 512);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x30), 8);
        result[0xC] = 15;
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
