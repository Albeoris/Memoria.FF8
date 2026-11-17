using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleTypedOccupancyTests
{
    [Fact]
    public void Current_participant_facts_roundtrip_distinct_occupancy_and_alias()
    {
        BattleSnapshot snapshot = CreateBattle().Capture();
        NativeBattleParticipantSnapshot participant = snapshot.Combatants[2].NativeParticipant!;
        NativeBattleParticipantSnapshot recorded = JsonSerializer.Deserialize<NativeBattleParticipantSnapshot>(JsonSerializer.Serialize(participant))!;
        Assert.Equal(BattleRosterSlot.Enemy(1), recorded.OccupiedSlot);
        Assert.Equal(NativeBattleModelSlot.Decode(4), recorded.OriginalSlot);
        NativeBattleParticipantSnapshot unaliased = recorded with { OccupiedSlot = BattleRosterSlot.Enemy(4), OriginalSlot = null };
        snapshot.Combatants[2] = snapshot.Combatants[2] with { NativeParticipant = unaliased };

        BattleSession current = BattleSession.Restore(snapshot);
        BattleCombatant actor = current.Combatants.Single(combatant => combatant.Id == "other");
        Assert.Equal(BattleRosterSlot.Enemy(4), actor.Slot);
        Assert.Null(actor.NativeActorIndex);
        BattleCombatant forked = current.ForkForContinuation().Combatants.Single(combatant => combatant.Id == actor.Id);
        Assert.Equal(actor.Slot, forked.Slot);
        Assert.Null(forked.NativeActorIndex);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("mismatched-alias")]
    [InlineData("out-of-range")]
    [InlineData("wrong-side")]
    [InlineData("missing-occupancy")]
    public void Invalid_participant_bindings_fail_before_restored_publication(string defect)
    {
        BattleSnapshot snapshot = CreateBattle().Capture();
        NativeBattleParticipantSnapshot participant = snapshot.Combatants[2].NativeParticipant!;
        participant = defect switch
        {
            "duplicate" => participant with { OccupiedSlot = BattleRosterSlot.Enemy(0), OriginalSlot = null },
            "mismatched-alias" => participant with { OriginalSlot = NativeBattleModelSlot.Decode(3) },
            "out-of-range" => participant with { OccupiedSlot = BattleRosterSlot.Enemy(ExpandedBattleFormation.MaximumEnemies), OriginalSlot = null },
            "wrong-side" => participant with { OccupiedSlot = BattleRosterSlot.Ally(0), OriginalSlot = null },
            _ => participant with { OccupiedSlot = null, OriginalSlot = null }
        };
        snapshot.Combatants[2] = snapshot.Combatants[2] with { NativeParticipant = participant };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot));
    }

    private static BattleSession CreateBattle()
    {
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "first", Name = "First" }, new() { Id = "other", Name = "Other" }]
        };
        return new(definition, BattleAtbMode.TurnBased);
    }
}
