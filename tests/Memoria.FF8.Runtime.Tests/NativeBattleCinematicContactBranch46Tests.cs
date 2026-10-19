using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicContactBranch46Tests
{
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 4, Address(0x1000));

    [Theory]
    [InlineData(0, 0, 0, 0u, true)]
    [InlineData(0, 0, 4, 0u, false)]
    [InlineData(1, 0, 4, 0u, true)]
    [InlineData(1, 8, 0, 0u, false)]
    [InlineData(2, 8, 0, 0u, true)]
    [InlineData(2, 0, 4, 0u, false)]
    [InlineData(3, 0, 2, 0u, true)]
    [InlineData(3, 8, 4, 0u, false)]
    [InlineData(4, 0, 0, 0x2000u, true)]
    [InlineData(4, 8, 6, 0x200u, false)]
    [InlineData(127, 0, 0, 0u, true)]
    public void Mode_table_selects_the_native_contact_byte_or_lazy_battle_status(int mode, byte flags2, byte flags3, uint status, bool branch)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(mode, 1, -12);
        frame = frame with { Arena = frame.Arena.Write(0x6000, [3]).Write(0x6002, [flags2, flags3]) };
        if (!branch)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        int statusReads = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicContactBranch46.Execute(frame, Program, UnexpectedActionRead, (_, actor) =>
        {
            Assert.Equal(4, mode);
            Assert.Equal((byte)3, actor);
            statusReads++;
            return status;
        });
        Assert.Equal(Address(branch ? 0xFF4 : 0x1004), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(mode == 4 ? 1 : 0, statusReads);
        Assert.Equal((ushort)0xA5A5, result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void Unsigned_count_scans_past_127_and_stops_at_the_first_matching_record()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, 0x81, 18);
        for (int index = 0; index < 128; index++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x6000 + 24 * index, [(byte)(index == 127 ? 3 : 1)]) };
        }
        frame = frame with { Arena = frame.Arena.Write(0x6000 + 24 * 127 + 3, [4]) };
        NativeBattleWaterExecutionFrame result = Run(frame);
        Assert.Equal(Address(0x1012), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    public void Exhausted_scan_still_reads_the_next_record_flags_instead_of_inventing_no_match(byte count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, count, 12);
        for (int index = 0; index < count; index++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x6000 + 24 * index, [1]) };
        }
        Assert.Throws<NotSupportedException>(() => Run(frame));
        frame = frame with { Arena = frame.Arena.Write(0x6000 + 24 * count + 3, [4]) };
        Assert.Equal(Address(0x100C), WaterSchedulerFixture.ReadCoreDword(Run(frame), 0x2797450));
    }

    [Fact]
    public void Byte_three_is_consumed_before_the_mode_two_byte_two_test()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2, 1, 12);
        frame = frame with { Arena = frame.Arena.Write(0x6000, [3]).Write(0x6002, [8]) };
        Assert.Throws<NotSupportedException>(() => Run(frame));
    }

    [Fact]
    public void External_packet_reader_observes_only_the_actual_ordered_fields()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2, 1, -4);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, 0x30000000);
        List<(uint Address, int Length)> reads = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicContactBranch46.Execute(frame, Program, (_, address, length) =>
        {
            reads.Add((address, length));
            return address switch
            {
                0x30000008 => BitConverter.GetBytes(0x30000100u),
                0x30000010 => [1],
                0x30000100 => [3],
                0x30000103 => [0],
                0x30000102 => [8],
                _ => throw new InvalidOperationException()
            };
        }, (_, _) => throw new InvalidOperationException());
        Assert.Equal(new[] { (0x30000008u, 4), (0x30000010u, 1), (0x30000100u, 1), (0x30000103u, 1), (0x30000102u, 1) }, reads);
        Assert.Equal(Address(0xFFC), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame) => NativeBattleCinematicContactBranch46.Execute(frame, Program, UnexpectedActionRead, (_, _) => throw new InvalidOperationException());
    private static byte[] UnexpectedActionRead(NativeBattleWaterExecutionFrame frame, uint address, int length) => throw new InvalidOperationException();

    private static NativeBattleWaterExecutionFrame Fixture(int mode, byte count, short displacement)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, Address(0x5000));
        frame = frame.WriteTemporaryWord(0x14A, unchecked((ushort)(0x46 | mode << 9)));
        frame = frame with { Arena = frame.Arena.Write(0x201B, [3]).Write(0x5008, BitConverter.GetBytes(Address(0x6000))).Write(0x5010, [count]).MarkUnknown(0x6000, 0xD00) };
        byte[] program = [0x46, 0, 0, 0];
        BinaryPrimitives.WriteUInt16LittleEndian(program, unchecked((ushort)(0x46 | mode << 9)));
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(2), displacement);
        return frame with { Arena = frame.Arena.Write(0x1000, program) };
    }
}
