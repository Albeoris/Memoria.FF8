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
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services());
        Assert.Equal(0x1002u, Pc(result));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleIfritContactCommand.Execute(restored, Services())));
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

    [Fact]
    public void Saved_actor_mode_walks_reverse_list_reloads_contact_base_and_retains_native_count_and_header()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, 3, 1, 2, 3);
        uint header = NativeBattleScriptedEffectMemory.Address;
        frame = frame with { Core = frame.Core.Write(header + 0x174, [2, 3, 2]).Write(header + 0x17A, [3]), Arena = frame.Arena.MarkUnknown(0x201B, 1).Write(0x7000, [3]).Write(0x7018, [2]) };
        List<uint> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services() with
        {
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                current = current with { Arena = current.Arena.Write(0x5008, BitConverter.GetBytes(Address(0x7000))).Write(0x5010, [0]) };
                current = WaterSchedulerFixture.CoreDword(current, 0x279744C, header + 0x300);
                current = WaterSchedulerFixture.CoreDword(current, 0x279775C, Address(0x8000));
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, uint.MaxValue - 1);
            }
        });
        Assert.Equal(new[] { Address(0x6018), Address(0x7000), Address(0x7018) }, calls);
        Assert.Equal(0u, Pc(result));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Equal(header + 0x300, WaterSchedulerFixture.ReadCoreDword(result, 0x279744C));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x80)]
    [InlineData(0xFF)]
    public void Saved_actor_zero_falls_back_to_slot_while_negative_count_reads_neither_slot_nor_records(byte saved)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, 1, 4);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x17A, [saved]).MarkUnknown(NativeBattleScriptedEffectMemory.Address + 0x174, 6) };
        if (saved != 0)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(0x201B, 1).MarkUnknown(0x6000, 1) };
        }
        int applied = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services() with { ApplyContact506690 = (current, _) =>
        {
            applied++;
            return current;
        } });
        Assert.Equal(saved == 0 ? 1 : 0, applied);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Theory]
    [InlineData(0x0E22, 1)]
    [InlineData(0x8E22, 3)]
    [InlineData(0x9E22, 1)]
    public void Saved_variant_other_modes_ignore_saved_header_and_native_modifier_bits(ushort instruction, int expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 3, 4, 4, 4);
        frame = frame with { Core = frame.Core.MarkUnknown(NativeBattleScriptedEffectMemory.Address + 0x17A, 1) };
        List<uint> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services() with { ApplyContact506690 = (current, contact) =>
        {
            calls.Add(contact);
            return current;
        } });
        Assert.Equal(expected, calls.Count);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void Saved_actor_zero_contact_count_still_reads_each_saved_actor_and_live_action_base()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, 0);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x17A, [1]).MarkUnknown(NativeBattleScriptedEffectMemory.Address + 0x174, 1), Arena = frame.Arena.MarkUnknown(0x201B, 1).MarkUnknown(0x6000, 0x100) };
        Assert.Throws<NotSupportedException>(() => NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services()));
    }

    [Fact]
    public void Saved_actor_snapshot_and_Water_entry_use_the_same_current_contact_kernel()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, 2, 3, 4);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x174, [3, 4]).Write(NativeBattleScriptedEffectMemory.Address + 0x17A, [2]) };
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleWaterContextServices services = Services() with
        {
            ApplyContact506690 = (current, _) => current.WriteTemporaryWord(0x100, unchecked((ushort)(current.ReadTemporaryWord(0x100) + 1)))
        };
        NativeBattleWaterExecutionFrame expected = NativeBattleIfritContactCommand.ExecuteSavedActors(NativeBattleTestSnapshots.Independent(frame), services);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleIfritContactCommand.ExecuteSavedActors(restored, services)));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleWaterContextCommands.Execute(frame, new(0, 0, 0), services)));
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
