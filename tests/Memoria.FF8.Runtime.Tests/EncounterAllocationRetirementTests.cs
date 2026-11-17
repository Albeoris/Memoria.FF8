using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterAllocationRetirementTests
{
    private static readonly BattleModelOwner Party = new BattlePartyActorModel(new("party"));
    private static readonly BattleModelOwner Original = new BattleEnemyActorModel(new("original"));
    private static readonly BattleModelOwner Copy = new BattleEnemyActorModel(new("copy"));
    private static readonly BattleModelOwner Fourth = new BattleEnemyActorModel(new("fourth"));
    private static readonly BattleModelRoster Roster = new([Party, Original, Copy, Fourth]);
    private static readonly NativeBattleModelAliases Aliases = new(Roster,
        [(NativeBattleModelSlot.Decode(0), Party), (NativeBattleModelSlot.Decode(3), Original), (NativeBattleModelSlot.Decode(4), Copy), (NativeBattleModelSlot.Decode(6), Fourth)]);

    [Fact]
    public void PublishedCopyHeaderRetainsResourcesUntilItsEventCountsReset()
    {
        EncounterAllocationRetirement retirement = new(BattleRosterSlot.Enemy(1), 4);
        NativeBattleActionPacketsState packets = Header(Copy, Party);
        NativeBattleGraphicsState graphics = Graphics();
        Assert.True(retirement.HasAcceptedConsumers("copy", packets, graphics));
        Assert.False(retirement.HasAcceptedConsumers("copy", packets.ResetCounts(), graphics));
        Assert.Equal(retirement, JsonSerializer.Deserialize<EncounterAllocationRetirement>(JsonSerializer.Serialize(retirement)));
    }

    [Fact]
    public void UnrelatedFourthActorCannotStarveRetirementButAcceptedCopyContactsRetainIt()
    {
        EncounterAllocationRetirement retirement = new(BattleRosterSlot.Enemy(1), 4);
        BattleRetainedModelReference target = BattleRetainedModelReference.Capture(Party, Aliases);
        BattleRetainedModelSelection selection = BattleRetainedModelSelection.From([target]);
        NativeBattleGraphicsState graphics = Graphics() with { ActionActorId = "fourth", ActionContext = new(BattleRetainedModelReference.Capture(Fourth, Aliases), selection, false) { TargetActors = selection, Target = target } };
        BattleTargetRequest request = NativeBattleTargetRequestAdapter.Decode(1, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.None, samplesOriginalSlots: true);
        BattleActionPresentation active = new("fourth", "party", BattleActionPhase.Windup, 10, request, BattleAbility.EnemyAbility, 1);
        Assert.False(retirement.HasAcceptedConsumers("copy", Header(Fourth, Party), graphics, active));
        Assert.True(retirement.HasAcceptedConsumers("copy", Header(Fourth, Copy), graphics, active));
        Assert.True(retirement.HasAcceptedConsumers("copy", OriginalPackets(), graphics with { PendingContacts = [Contact(Copy)] }, active));
    }

    private static NativeBattleActionPacketsState Header(BattleModelOwner actor, BattleModelOwner target)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(Roster, NativeBattleRetainedActionPackets.Fresh(), Aliases);
        (packets, _) = packets.Prepare(new(actor, 8, 7, 0, 1, 1, null));
        (packets, _) = packets.AppendContact(Contact(target));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner target)
    {
        return new(new(target, 0, 0, 0, 0, 100, 0), null);
    }

    private static NativeBattleGraphicsState Graphics()
    {
        return NativeBattleHostedTestFixture.Graphics(Roster, Aliases, _ => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null), BattleContactTaskStorageContract.OriginalArena);
    }
}
