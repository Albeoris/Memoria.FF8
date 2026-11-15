using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritContactCommandTests
{
    [Theory]
    [InlineData(0x0022)]
    [InlineData(0x1022)]
    [InlineData(0xF022)]
    public void Every_non_all_mode_uses_the_current_slot_first_match_without_reading_the_saved_list(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        frame = WithSavedActors(frame, 2, 6);
        List<BattleContactOccurrence> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services(Packets(2, 4, 4)) with
        {
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFE);
            }
        });
        Assert.Equal(new[] { new BattleContactOccurrence(1) }, calls);
        Assert.Equal(0u, Pc(result));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
    }

    [Fact]
    public void All_mode_preserves_the_initial_count_and_rereads_the_current_packet_owner_after_each_callback()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x8022, bindSlot: false);
        NativeBattleActionPacketsState packets = Packets(2, 4, 6);
        List<BattleContactOccurrence> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services(packets) with
        {
            ReadPackets = () => packets,
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                packets = PacketsWithPrefix(3, 2, 4, 6, 0);
                current = WaterSchedulerFixture.CoreDword(current, 0x2797450, Pc(current) + 4);
                return current with { Arena = current.Arena.Write(0x2100 + calls.Count, [(byte)calls.Count]) };
            }
        });
        Assert.Equal(new[] { new BattleContactOccurrence(0), new BattleContactOccurrence(4), new BattleContactOccurrence(5) }, calls);
        Assert.Equal(0x100Eu, Pc(result));
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Arena.Read(0x2101, 3));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
    }

    [Theory]
    [InlineData(0x8022, 0)]
    [InlineData(0x8022, 128)]
    [InlineData(0x8022, 255)]
    [InlineData(0x1022, 0)]
    [InlineData(0x1022, 255)]
    public void Full_count_controls_all_occurrences_and_first_matching_slot(ushort instruction, int count)
    {
        bool all = instruction == 0x8022;
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, bindSlot: !all);
        int[] recipients = Enumerable.Repeat(2, count).ToArray();
        if (count > 0)
        {
            recipients[^1] = 4;
        }
        List<BattleContactOccurrence> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, Services(Packets(recipients)) with
        {
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                return current;
            }
        });
        IEnumerable<BattleContactOccurrence> expected = all ? Enumerable.Range(0, count).Select(index => new BattleContactOccurrence(index)) : count == 0 ? [] : [new BattleContactOccurrence(count - 1)];
        Assert.Equal(expected, calls);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void Missing_match_applies_nothing_and_private_continuation_repeats_the_next_step()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022);
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterContextServices services = Services(Packets(1, 3));
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, services);
        Assert.Equal(0x1002u, Pc(result));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleIfritContactCommand.Execute(restored, services)));
    }

    [Fact]
    public void Requires_a_live_header_and_ignores_the_retained_action_pointer()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(Fixture(0x1022), 0x279775C, 0x30000000);
        NativeBattleWaterContextServices services = Services(Packets(3));
        Assert.Equal(0x1002u, Pc(NativeBattleIfritContactCommand.Execute(frame, services)));
        Assert.Throws<InvalidDataException>(() => NativeBattleIfritContactCommand.Execute(frame, services with { ReadCurrentHeader = _ => null }));
        Assert.Throws<NotSupportedException>(() => NativeBattleIfritContactCommand.Execute(frame.WriteTemporaryWord(0x14A, 0x1039), services));
    }

    [Fact]
    public void Saved_actor_mode_walks_reverse_list_and_rereads_the_live_packet_owner()
    {
        NativeBattleWaterExecutionFrame frame = WithSavedActors(Fixture(0x1022, bindSlot: false), 2, 3, 2);
        NativeBattleActionPacketsState packets = Packets(1, 2, 3);
        List<BattleContactOccurrence> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services(packets) with
        {
            ReadPackets = () => packets,
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                packets = PacketsWithPrefix(3, 3, 2, 1);
                current = WaterSchedulerFixture.CoreDword(current, 0x279775C, 0x30000000);
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, uint.MaxValue - 1);
            }
        });
        Assert.Equal(new[] { new BattleContactOccurrence(1), new BattleContactOccurrence(3), new BattleContactOccurrence(4) }, calls);
        Assert.Equal(0u, Pc(result));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Equal(0x30000000u, WaterSchedulerFixture.ReadCoreDword(result, 0x279775C));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_saved_selection_uses_the_live_slot_and_explicit_saved_selection_does_not(bool saved)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, bindSlot: !saved);
        if (saved)
        {
            frame = WithSavedActors(frame, 3);
        }
        int applied = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services(Packets(4)) with
        {
            ApplyContact506690 = (current, _) =>
            {
                applied++;
                return current;
            }
        });
        Assert.Equal(saved ? 0 : 1, applied);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Theory]
    [InlineData(0x0E22, 1)]
    [InlineData(0x8E22, 3)]
    [InlineData(0x9E22, 1)]
    public void Saved_variant_other_modes_ignore_the_saved_selection_and_native_modifier_bits(ushort instruction, int expected)
    {
        NativeBattleWaterExecutionFrame frame = WithSavedActors(Fixture(instruction), 3);
        List<BattleContactOccurrence> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services(Packets(4, 4, 4)) with
        {
            ApplyContact506690 = (current, contact) =>
            {
                calls.Add(contact);
                return current;
            }
        });
        Assert.Equal(expected, calls.Count);
        Assert.Equal(0x1002u, Pc(result));
    }

    [Fact]
    public void Zero_contacts_still_validate_the_selected_slot_owner_in_non_all_mode()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x1022, bindSlot: false);
        Assert.Throws<InvalidDataException>(() => NativeBattleIfritContactCommand.ExecuteSavedActors(frame, Services(Packets())));
    }

    [Fact]
    public void Saved_actor_private_copy_and_Water_entry_use_the_same_current_contact_kernel()
    {
        NativeBattleWaterExecutionFrame frame = WithSavedActors(Fixture(0x1022), 3, 4);
        NativeBattleWaterExecutionFrame restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        NativeBattleWaterContextServices services = Services(Packets(3, 4)) with
        {
            ApplyContact506690 = (current, _) => current.WriteTemporaryWord(0x100, unchecked((ushort)(current.ReadTemporaryWord(0x100) + 1)))
        };
        NativeBattleWaterExecutionFrame expected = NativeBattleIfritContactCommand.ExecuteSavedActors(NativeBattleTestSnapshots.Independent(frame), services);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleIfritContactCommand.ExecuteSavedActors(restored, services)));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(NativeBattleWaterContextCommands.Execute(frame, new(0, 0, 0), services)));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, bool bindSlot = true)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction).WriteTemporaryWord(0x13E, 0x1234).WriteTemporaryWord(0x100, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        return bindSlot ? NativeBattleCinematicTypedTestFixture.BindActor(frame, OriginalOwner(4)) : NativeBattleCinematicTypedTestFixture.WithContext(frame);
    }

    private static NativeBattleWaterExecutionFrame WithSavedActors(NativeBattleWaterExecutionFrame frame, params int[] slots)
    {
        ImmutableArray<BattleModelOwner> actors = slots.Select(OriginalOwner).ToImmutableArray();
        return frame with { Core = frame.Core with { PresentationActors = frame.Core.PresentationActors!.WithSavedActors(actors) } };
    }

    private static NativeBattleActionPacketsState Packets(params int[] actors) => PacketsWithPrefix(0, actors);

    private static NativeBattleActionPacketsState PacketsWithPrefix(int prefix, params int[] actors)
    {
        NativeBattleActionPacketsState packets = OriginalPackets().AppendContacts(Enumerable.Repeat(Contact(0), prefix));
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 0, 0, 0, 0, 0, null));
        packets = packets.AppendContacts(actors.Select(Contact));
        return packets.Publish(actors.Length).State;
    }

    private static NativeBattleContactPacket Contact(int slot) => new(new(OriginalOwner(slot), 0, 0, 0, 0, 0, 0), null);

    private static NativeBattleWaterContextServices Services(NativeBattleActionPacketsState packets) =>
        NativeBattleCinematicTypedTestFixture.Services() with { ReadCurrentHeader = _ => new BattleActionHeaderReference(0), ReadPackets = () => packets };

    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
