using System.Collections.Immutable;
using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupMode6Tests
{
    [Theory]
    [InlineData(false, -1, true)]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, -1, true)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    public void Selection_uses_the_current_signed_vertical_sum_and_strict_threshold(bool quad, short y, bool selected)
    {
        byte[] payload = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), 6);
        NativeBattleControl18Face face = new(0x123456, quad, Enumerable.Repeat(new NativeBattleVector(3, y, 5), quad ? 4 : 3).ToImmutableArray());
        NativeCrtRandomState initial = new(123);
        NativeBattleControl18SpawnStep result = NativeBattleControl18Spawn.Advance(new byte[68], new byte[1], 0, payload, [face], 0, initial);
        Assert.Equal(selected ? (ushort)1 : (ushort)0, result.LiveCount);
        Assert.Equal(selected ? (byte)1 : (byte)0, result.PolygonStates[0]);
        NativeCrtRandomState expected = initial;
        for (int index = 0; index < (selected ? 7 : 0); index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, result.Random);
    }

    [Theory]
    [InlineData(300, 0, 0, 300)]
    [InlineData(300, 1024, 300, 0)]
    [InlineData(-300, -1024, 300, 0)]
    [InlineData(1048577, 1024, 1, 0)]
    public void Auxiliary_integrates_before_polar_projection_with_signed_shifts_and_wrapped_products(int radius, int angle, short expectedX, short expectedZ)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x84, basis + 0x5000).WriteTemporaryDword(0x8C, basis + 0x10000).WriteTemporaryDword(0x90, basis + 0x4000).WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797768, basis + 0x50000);
        byte[] payload = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0x2E), 1);
        byte[] row = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(row, unchecked((radius << 8) - 16));
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(4), 256 - 32);
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(8), unchecked((angle << 4) - 48));
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(12), 1);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(16), 2);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(20), 3);
        row[24] = 1;
        row[25] = 2;
        frame = frame with { Arena = frame.Arena.Write(0x4000, payload).Write(0x500C, BitConverter.GetBytes(basis + 0x7000)).Write(0x7004, BitConverter.GetBytes(0x00030201u)).Write(0x10000, row) };
        NativeBattleVector? projectedPosition = null;
        NativeBattleCinematicBreakupAuxiliaryServices services = new((current, position) =>
        {
            projectedPosition = position;
            return new(current, 0x12345678, 100, 0);
        }, (current, depths) =>
        {
            Assert.Equal(new[] { 400, 400, 400, 400 }, depths);
            return current;
        }, (current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupAuxiliary.AdvanceDraw(frame, services);
        Assert.Equal(new NativeBattleVector(expectedX, 1, expectedZ), projectedPosition);
        Assert.Equal(unchecked((ushort)expectedX), result.ReadTemporaryWord(0xF0));
        Assert.Equal((ushort)1, result.ReadTemporaryWord(0xF2));
        Assert.Equal(unchecked((ushort)expectedZ), result.ReadTemporaryWord(0xF4));
        byte[] actual = result.Arena.Read(0x10000, 28);
        Assert.Equal(unchecked(radius << 8), BinaryPrimitives.ReadInt32LittleEndian(actual));
        Assert.Equal(unchecked(angle << 4), BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(8)));
        Assert.Equal((short)2, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(16)));
        Assert.Equal((byte)1, actual[24]);
        Assert.Equal((byte)1, actual[25]);
    }
}
