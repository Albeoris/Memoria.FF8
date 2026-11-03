namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterNativeTerminalWitnessTests
{
    [Theory]
    [InlineData(207, 15)]
    [InlineData(264, 17)]
    public void Recognizes_only_accepted_original_terminal_command_at_its_native_marker(int actionId, byte sequence)
    {
        NativeBattleActionHeaderSlot action = Header(actionId, sequence);
        Assert.True(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", action, 3, sequence, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "fourth", action, 4, sequence, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", action, 3, sequence, false, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", action, 3, sequence, true, false));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", null, 3, sequence, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", Header(actionId, sequence, 2), 3, sequence, true, true));
    }

    [Fact]
    public void Does_not_turn_cosmetic_taunts_or_unrelated_removals_into_a_terminal_result()
    {
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", Header(205, 13), 3, 13, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", Header(207, 17), 3, 17, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", Header(264, 15), 3, 15, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches("another.encounter", "source", "source", Header(264, 17), 3, 17, true, true));
        Assert.False(EncounterNativeTerminalWitness.Matches(EncounterBindingResolver.Disc1Edea, "source", "source", null, 3, 17, true, true));
    }

    private static NativeBattleActionHeaderSlot Header(int id, byte sequence, byte command = 8)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, int index) = packets.Prepare(new(3, command, sequence, -1, checked((ushort)id), 0, null));
        return packets.Headers[index];
    }
}
