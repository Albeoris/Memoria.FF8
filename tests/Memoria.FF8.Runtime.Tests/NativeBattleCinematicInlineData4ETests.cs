using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicInlineData4ETests
{
    private static uint Address(int offset) => unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset);
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 6, Address(0x1000));

    [Theory]
    [InlineData((short)-48, (short)-3)]
    [InlineData((short)0, (short)0)]
    [InlineData((short)3, (short)5)]
    [InlineData((short)18, short.MaxValue)]
    [InlineData((short)-1, short.MinValue)]
    public void Signed_selectors_can_address_adjacent_slot_bytes_and_signed_extents_move_the_opcode_pc(short selector, short words)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(selector, words);
        int destination = 0x2000 + 0xB8 + 4 * selector;
        frame = frame with { Arena = frame.Arena.MarkUnknown(destination, 4).MarkUnknown(0x1006, 32) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicInlineData4E.Execute(frame, Program);
        Assert.Equal(Address(0x1006), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(destination, 4)));
        Assert.Equal(unchecked(Address(0x1000) + (uint)(6 + 2 * words)), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0xA5A5, result.ReadTemporaryWord(0x13E));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x1006, 1));
    }

    [Fact]
    public void Aliasing_destination_publishes_the_payload_pointer_before_rereading_the_signed_word_extent()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(-1069, 1);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicInlineData4E.Execute(frame, Program);
        Assert.Equal(Address(0x1006), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x1004, 4)));
        short replacedCount = unchecked((short)Address(0x1006));
        Assert.Equal(unchecked(Address(0x1000) + (uint)(6 + 2 * replacedCount)), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.NotEqual(Address(0x1008), WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    [Fact]
    public void Only_the_selected_pointer_dword_becomes_known()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(3, 9);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x20C0, 12) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicInlineData4E.Execute(frame, Program);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x20C3, 1));
        Assert.Equal(Address(0x1006), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x20C4, 4)));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x20C8, 1));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short selector, short words)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, Address(0x1000));
        frame = frame.WriteTemporaryWord(0x14A, 0x4E);
        byte[] bytes = [0x4E, 0, 0, 0, 0, 0];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), selector);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), words);
        return frame with { Arena = frame.Arena.Write(0x1000, bytes) };
    }
}
