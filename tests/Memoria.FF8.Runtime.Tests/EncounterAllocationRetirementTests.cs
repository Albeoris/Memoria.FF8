using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterAllocationRetirementTests
{
    [Fact]
    public void PublishedCopyHeaderRetainsResourcesUntilItsEventCountsReset()
    {
        EncounterAllocationRetirement retirement = new(4);
        NativeBattleActionPacketsState packets = Header(4, 0);
        NativeBattleGraphicsState graphics = Graphics();
        Assert.True(retirement.HasAcceptedConsumers("copy", packets, graphics));
        Assert.False(retirement.HasAcceptedConsumers("copy", packets.ResetCounts(), graphics));
        Assert.Equal(retirement, JsonSerializer.Deserialize<EncounterAllocationRetirement>(JsonSerializer.Serialize(retirement)));
    }

    [Fact]
    public void UnrelatedFourthActorCannotStarveRetirementButAcceptedCopyContactsRetainIt()
    {
        EncounterAllocationRetirement retirement = new(4);
        NativeBattleGraphicsState graphics = Graphics() with { ActionActorId = "fourth", ActionContext = new(6, 1) { PhysicalTargetMask = 1 } };
        BattleActionPresentation active = new("fourth", "party", BattleActionPhase.Windup, 10, BattleAbility.EnemyAbility, 1) { NativeTargetMask = 1 };
        Assert.False(retirement.HasAcceptedConsumers("copy", Header(6, 0), graphics, active));
        Assert.True(retirement.HasAcceptedConsumers("copy", Header(6, 4), graphics, active));
        Assert.True(retirement.HasAcceptedConsumers("copy", NativeBattleActionPacketsState.Fresh(), graphics with { PendingContacts = [Contact(4)] }, active));
    }

    private static NativeBattleActionPacketsState Header(byte actor, byte target)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(actor, 8, 7, 0, 1, 1, null));
        (packets, _) = packets.AppendContact(Contact(target));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleContactPacket Contact(byte target)
    {
        return new(new(target, 0, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0));
    }

    private static NativeBattleGraphicsState Graphics()
    {
        return new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray(), new string?[] { "party", null, null, "original", "copy", null, "fourth" }.ToImmutableArray(), 0, 0);
    }
}
