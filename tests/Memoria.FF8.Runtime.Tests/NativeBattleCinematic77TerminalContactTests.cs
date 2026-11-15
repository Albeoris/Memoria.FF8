using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematic77TerminalContactTests
{
    [Theory]
    [InlineData(3, 1)]
    [InlineData(0, 0)]
    public void Terminal_self_selector_uses_retained_header_count_after_global_count_clear_and_preserves_suppressed_glyphs(byte recipient, int expectedCalls)
    {
        BattleModelOwner caster = OriginalOwner(3);
        BattleModelOwner recipientOwner = OriginalOwner(recipient);
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(caster, 8, 18, 0, 77, 0, null));
        (packets, BattleContactOccurrence occurrence) = packets.AppendContact(new(new(recipientOwner, 0, 1, 0, 0, 7, 0), null));
        (packets, _) = packets.Publish(1);
        packets = packets.ActivateHeader(header).ResetCounts();

        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x0022).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x20DFC80);
        BattleActorModels models = new([new(caster, new NativeBattleGraphicsModel(3))]);
        frame = frame with { Core = frame.Core with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(models) } };
        frame = NativeBattleCinematicActorAccess.WriteSlotActor(frame, caster, OriginalAliases);

        NativeBattleEffectsState beforeEffects = OriginalEffects();
        NativeBattleEffectsState effects = beforeEffects;
        List<BattleContactOccurrence> applied = [];
        NativeBattleWaterContextServices services = new(
            (_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            (_, _, _) => throw new InvalidOperationException("Contact dispatch uses the live typed packet bank."),
            (current, reached) =>
            {
                Assert.Equal(occurrence, reached);
                applied.Add(reached);
                NativeBattleContactHalf half = packets.ReadContact(reached).Primary;
                Assert.Equal(caster, half.Recipient);
                Assert.Equal((byte)0, half.Sequence);
                Assert.Equal((ushort)1, half.ContactFlags);
                effects = effects.PrependDamageText(half.Recipient, half.ContactFlags, half.Amount6, packets.Roster);
                return current;
            },
            _ => throw new InvalidOperationException(),
            (_, _, _, _) => throw new InvalidOperationException(),
            _ => header,
            () => packets,
            () => packets.ModelAliases,
            BattleContactTaskStorageContract.OriginalArena);

        NativeBattleWaterExecutionFrame result = NativeBattleIfritContactCommand.Execute(frame, services);

        Assert.Equal(expectedCalls, applied.Count);
        Assert.Equal(0, packets.ContactCount);
        Assert.Equal((byte)1, packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal(0x20DFC82u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(beforeEffects, effects);
        Assert.Null(effects.Head);
        Assert.Empty(effects.TextDraws);
    }
}
