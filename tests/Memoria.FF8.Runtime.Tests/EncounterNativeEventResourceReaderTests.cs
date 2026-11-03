using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterNativeEventResourceReaderTests
{
    [Fact]
    public void Binds_selected_text_ids_and_cadence_and_retains_both_deferred_party_lines()
    {
        EncounterNativeEventResources bound = EncounterNativeEventResourceReader.Bind(Resource());
        Assert.Equal(new EncounterNativeTextCue(1, 12), Assert.Single(bound.Opening));
        Assert.Equal(new[] { new EncounterNativeTextCue(2, 18), new EncounterNativeTextCue(4, 25) }, bound.Party);
        Assert.Equal(new EncounterNativeTextCue(5, 7), Assert.Single(bound.LowHpTerminal));
        Assert.Equal(new EncounterNativeTextCue(6, 11), Assert.Single(bound.DeathTerminal));
        EncounterNativeEventResources restored = JsonSerializer.Deserialize<EncounterNativeEventResources>(JsonSerializer.Serialize(bound))!;
        Assert.Equal(bound.Party.ToArray(), restored.Party.ToArray());
        Assert.Equal(bound.DeathTerminal.ToArray(), restored.DeathTerminal.ToArray());
    }

    [Fact]
    public void Rejects_rewritten_terminal_contact_order_and_out_of_bank_text()
    {
        NativeBattleGraphicsResource baseline = Resource();
        int terminal = baseline.SequenceEnd(15) - 3;
        NativeBattleGraphicsResource reordered = baseline with { SequenceSection = baseline.SequenceSection.SetItem(terminal, 0xAA).SetItem(terminal + 1, 0x9D) };
        Assert.Contains("terminal control structure", Assert.Throws<InvalidDataException>(() => EncounterNativeEventResourceReader.Bind(reordered)).Message);
        int openingText = baseline.SequenceStart(13) + 2;
        NativeBattleGraphicsResource invalidText = baseline with { SequenceSection = baseline.SequenceSection.SetItem(openingText, 200) };
        Assert.Contains("text index", Assert.Throws<InvalidDataException>(() => EncounterNativeEventResourceReader.Bind(invalidText)).Message);
    }

    [Fact]
    public void Delayed_lines_keep_native_order_one_per_admitted_turn_across_snapshot_restore()
    {
        EncounterNativeEventResources resources = EncounterNativeEventResourceReader.Bind(Resource());
        EncounterNativeEventState state = new(PartyPending: true);
        Assert.Null(state.Admit(4, 5, resources).Line);
        (state, EncounterNativeTextCue? first) = state.Admit(5, 5, resources);
        Assert.Equal(resources.Opening[0], first);
        Assert.Null(state.Admit(5, 5, resources).Line);
        state = JsonSerializer.Deserialize<EncounterNativeEventState>(JsonSerializer.Serialize(state))!;
        (state, EncounterNativeTextCue? second) = state.Admit(6, 5, resources);
        Assert.Equal(resources.Party[0], second);
        state = JsonSerializer.Deserialize<EncounterNativeEventState>(JsonSerializer.Serialize(state))!;
        (state, EncounterNativeTextCue? third) = state.Admit(7, 5, resources);
        Assert.Equal(resources.Party[1], third);
        Assert.False(state.PartyPending);
        Assert.Null(state.Admit(8, 5, resources).Line);
    }

    private static NativeBattleGraphicsResource Resource()
    {
        List<byte> bytes = Enumerable.Repeat((byte)0, 36).ToList();
        bytes[0] = 17;
        Add(13, true, [new(1, 12)], [0xAA, 0xA2]);
        Add(14, true, [new(2, 18), new(4, 25)], [0xAA, 0xA2]);
        Add(15, true, [new(5, 7)], [0x9D, 0xAA, 0xA2]);
        Add(17, false, [new(6, 11)], [0xAA, 0x9D, 0xA2]);
        return new(bytes.ToImmutableArray()) { TextBank = Enumerable.Range(0, 7).Select(index => $"Synthetic line {index}").ToImmutableArray() };

        void Add(int sequence, bool title, EncounterNativeTextCue[] lines, byte[] ending)
        {
            byte[] pointer = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(pointer, checked((ushort)bytes.Count));
            bytes[sequence * 2] = pointer[0];
            bytes[sequence * 2 + 1] = pointer[1];
            if (title)
            {
                bytes.Add(0xBB);
            }
            foreach (EncounterNativeTextCue line in lines)
            {
                bytes.AddRange([0x91, line.Text, line.Duration, 0xC1, line.Duration, 0xE5, 0x7F, 0xA0, 0, 0xA1, 0xC3, 0x7F, 0xC5, 0xFF, 0xE5, 0x7F, 0xE7, 0xF7]);
            }
            bytes.AddRange(ending);
        }
    }
}
