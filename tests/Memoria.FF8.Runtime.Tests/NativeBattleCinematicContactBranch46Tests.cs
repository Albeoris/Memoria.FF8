using static Memoria.FF8.Tests.NativeBattleTypedFixture;
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
        NativeBattleActionPacketsState packets = Packets(1, [Half(3, flags2, flags3)]);
        if (!branch)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2) };
        }
        int statusReads = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicContactBranch46.Execute(frame, Program, Services(packets), (_, actor) =>
        {
            Assert.Equal(4, mode);
            Assert.Equal(OriginalOwner(3), actor);
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
        NativeBattleActionPacketsState packets = Packets(129, Enumerable.Range(0, 129).Select(index => Half(index == 127 ? 3 : 1, 0, index == 127 ? (byte)4 : (byte)0)));
        NativeBattleWaterExecutionFrame result = Run(frame, packets);
        Assert.Equal(Address(0x1012), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    public void Exhausted_scan_still_reads_the_next_record_flags_instead_of_inventing_no_match(byte count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1, count, 12);
        NativeBattleContactHalf[] scanned = Enumerable.Repeat(Half(1, 0, 0), count).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(frame, Packets(count, scanned)));
        Assert.Equal(Address(0x100C), WaterSchedulerFixture.ReadCoreDword(Run(frame, Packets(count, [.. scanned, Half(3, 0, 4)])), 0x2797450));
    }

    [Fact]
    public void An_unknown_hit_flag_byte_cannot_be_bound_as_a_live_contact()
    {
        NativeBattleRawContactPacket contact = Assert.IsType<NativeBattleRawContactPacket>(Packets(1, [Half(3, 8, 0)]).ExportRetained().Contacts[0]);
        byte[] unknown = new byte[24];
        unknown[3] = 0xFF;
        var (models, boundary) = OriginalModels([(3, new NativeBattleGraphicsModel(0))]);
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacketCodec.DecodeOriginal(contact with { UnknownBits = [.. unknown] }, boundary, models));
    }

    [Fact]
    public void Live_packet_owners_supply_the_contact_even_with_unmapped_original_action_pointer()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(2, 1, -4);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, 0x30000000);
        NativeBattleWaterExecutionFrame result = Run(frame, Packets(1, [Half(3, 8, 0)]));
        Assert.Equal(Address(0xFFC), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleContactHalf Half(int owner, byte flags2, byte flags) => new(OriginalOwner(owner), 0, flags2, flags, 0, 0, 0);

    private static NativeBattleActionPacketsState Packets(int count, IEnumerable<NativeBattleContactHalf> contacts)
    {
        (NativeBattleActionPacketsState packets, _) = OriginalPackets().Prepare(new(OriginalOwner(0), 0, 0, 0, 0, 0, null));
        packets = packets.AppendContacts(contacts.Select(contact => new NativeBattleContactPacket(contact, null)));
        return packets.Publish(count).State;
    }

    private static NativeBattleWaterContextServices Services(NativeBattleActionPacketsState packets) =>
        NativeBattleCinematicTypedTestFixture.Services() with { ReadCurrentHeader = _ => new BattleActionHeaderReference(0), ReadPackets = () => packets };

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, NativeBattleActionPacketsState packets) =>
        NativeBattleCinematicContactBranch46.Execute(frame, Program, Services(packets), (_, _) => throw new InvalidOperationException());

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
        return NativeBattleCinematicTypedTestFixture.BindActor(frame with { Arena = frame.Arena.Write(0x1000, program) }, OriginalOwner(3));
    }
}
