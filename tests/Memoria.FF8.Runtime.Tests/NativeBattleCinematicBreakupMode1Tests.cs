using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupMode1Tests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Selection_uses_the_strict_signed_vertical_threshold(short y, bool selected)
    {
        byte[] controls = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(controls.AsSpan(8), 1);
        NativeBattleControl18Face face = new(0x123456, false, [new(3, y, 5), new(3, y, 5), new(3, y, 5)]);
        NativeCrtRandomState initial = new(123);
        NativeBattleControl18SpawnStep result = NativeBattleControl18Spawn.Advance(new byte[68], new byte[1], 0, controls, [face], 0, initial);
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
    [InlineData(-48, true)]
    [InlineData(-64, false)]
    [InlineData(-80, false)]
    public void Auxiliary_motion_stops_only_vertical_velocity_after_crossing_zero(int initialY, bool stopped)
    {
        const uint basis = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x84, basis + 0x5000).WriteTemporaryDword(0x8C, basis + 0x10000).WriteTemporaryDword(0x90, basis + 0x4000).WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797768, basis + 0x50000);
        byte[] payload = new byte[0x4C];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0x2E), 1);
        byte[] row = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(row, 100);
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(4), initialY);
        BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(8), 200);
        short[] channels = [1, 2, 3, 1, 5, 1];
        for (int index = 0; index < channels.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(12 + index * 2), channels[index]);
        }
        row[24] = 1;
        row[25] = 2;
        frame = frame with { Arena = frame.Arena.Write(0x4000, payload).Write(0x500C, BitConverter.GetBytes(basis + 0x7000)).Write(0x7004, BitConverter.GetBytes(0x00030201u)).Write(0x10000, row) };
        NativeBattleCinematicBreakupAuxiliaryServices services = new((current, _) => new(current, 0, 100, 0), (current, _) => current, (current, _, _, _) => current);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupAuxiliary.AdvanceDraw(frame, services);
        byte[] actual = result.Arena.Read(0x10000, 28);
        Assert.Equal(148, BinaryPrimitives.ReadInt32LittleEndian(actual));
        Assert.Equal(initialY + 64, BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(4)));
        Assert.Equal(296, BinaryPrimitives.ReadInt32LittleEndian(actual.AsSpan(8)));
        Assert.Equal((short)3, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(12)));
        Assert.Equal((short)2, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(14)));
        Assert.Equal(stopped ? (short)0 : (short)4, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(16)));
        Assert.Equal(stopped ? (short)0 : (short)1, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(18)));
        Assert.Equal((short)6, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(20)));
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(actual.AsSpan(22)));
        Assert.Equal((byte)1, actual[24]);
        Assert.Equal((byte)1, actual[25]);
    }
}
