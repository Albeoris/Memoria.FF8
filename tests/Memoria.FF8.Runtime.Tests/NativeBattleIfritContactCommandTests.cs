using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritContactCommandTests
{
    [Theory]
    [InlineData(0x0022)]
    [InlineData(0x1022)]
    [InlineData(0xF022)]
    public void Every_non_all_mode_uses_current_slot_first_match_without_reading_Water_saved_list(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 3, 2, 4, 4);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x174, [2, 6]).MarkUnknown(NativeBattleScriptedEffectMemory.Address + 0x17A, 1) };
        List<uint> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services() with
        {
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFE);
            }
        });
        Assert.Equal(new[] { Address(0x6018) }, calls);
        Assert.Equal(0u, Pc(result));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void All_mode_uses_captured_signed_count_and_contact_base_preserves_each_callback_mutation()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8022, 3);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x201B, 1).MarkUnknown(0x6000, 0x100) };
        List<uint> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services() with
        {
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                current = WaterSchedulerFixture.CoreDword(current, 0x2797450, Pc(current) + 4);
                return current with { Arena = current.Arena.Write(0x5008, BitConverter.GetBytes(Address(0x7000))).Write(0x5010, [0]).Write(0x2100 + calls.Count, [(byte)calls.Count]) };
            }
        });
        Assert.Equal(new[] { Address(0x6000), Address(0x6018), Address(0x6030) }, calls);
        Assert.Equal(0x100Eu, Pc(result));
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Arena.Read(0x2101, 3));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
    }

    [Theory]
    [InlineData(0x8022, 0)]
    [InlineData(0x8022, 0x80)]
    [InlineData(0x8022, 0xFF)]
    [InlineData(0x1022, 0)]
    [InlineData(0x1022, 0xFF)]
    public void Zero_and_negative_counts_do_not_consume_contact_records(ushort instruction, byte count)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, count);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x6000, 0x100) };
        Assert.Equal(0x1002u, Pc(NativeBattleIfritContactCommand.Execute(frame, Services())));
    }

    [Fact]
    public void Missing_match_applies_nothing_and_restored_action_owners_repeat_next_step()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, 2, 1, 3);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services());
        Assert.Equal(0x1002u, Pc(result));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleIfritContactCommand.Execute(restored, Services())));
    }

    [Fact]
    public void External_action_reads_are_addressed_exactly_and_wrong_extents_fail()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, 0x30000000);
        List<(uint, int)> reads = [];
        NativeBattleWaterContextServices services = Services() with
        {
            ReadActionBytes = (_, address, length) =>
            {
                reads.Add((address, length));
                return address switch
                {
                    0x30000008 => BitConverter.GetBytes(0x30000100u),
                    0x30000010 => [1],
                    0x30000100 => [3],
                    _ => throw new InvalidOperationException()
                };
            }
        };
        Assert.Equal(0x1002u, Pc(NativeBattleIfritContactCommand.Execute(frame, services)));
        Assert.Equal(new[] { (0x30000008u, 4), (0x30000010u, 1), (0x30000100u, 1) }, reads);
        Assert.Throws<InvalidDataException>(() => NativeBattleIfritContactCommand.Execute(frame, services with { ReadActionBytes = (_, _, _) => [] }));
        Assert.Throws<NotSupportedException>(() => NativeBattleIfritContactCommand.Execute(frame.WriteTemporaryWord(0x14A, 0x1039), services));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, byte count, params byte[] actors)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279775C, Address(0x5000));
        frame = frame with { Arena = frame.Arena.Write(0x201B, [4]).Write(0x5008, BitConverter.GetBytes(Address(0x6000))).Write(0x5010, [count]) };
        for (int index = 0; index < actors.Length; index++)
        {
            frame = frame with { Arena = frame.Arena.Write(0x6000 + index * 0x18, [actors[index]]) };
        }
        return frame;
    }

    private static NativeBattleWaterContextServices Services() => new((_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException());
    private static uint Address(int offset) => NativeBattleEffectNodeMemory.ArenaAddress + (uint)offset;
    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
