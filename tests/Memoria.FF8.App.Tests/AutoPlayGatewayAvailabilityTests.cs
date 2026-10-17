using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayGatewayAvailabilityTests
{
    [Fact]
    public void OriginalUnavailableWorldOwnerDoesNotHideTheEnabledAuthoredExit()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayGatewayAvailabilityTests.1")));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(159, session.State.FieldId);
        Assert.Equal(40, session.State.WorldEntryMemory!.StoryWord);
        Assert.False(world.Events.Scene.MapJumpDisabled);
        Assert.True(world.Events.Actors[13].Active);
        Assert.True(world.Events.Actors[13].LineEnabled);
        JsonElement before = JsonSerializer.SerializeToElement(world.Events.Capture());
        GameState state = session.State;
        IReadOnlyList<AutoPlayTarget> unqualified = GameWorld.DiscoverAutoPlayTargets(world.Field!, world.Events, world.Events.PlayerActor!);
        Assert.Contains(unqualified, target => target.CoverageId == "159:gateway:2");

        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();
        Assert.DoesNotContain(targets, target => target.CoverageId == "159:gateway:2");
        Assert.Contains(targets, target => target.CoverageId == "159:line:13");
        Assert.Equal(state, session.State);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(world.Events.Capture())));
        Assert.False(world.Events.Scene.MapJumpDisabled);
        Assert.True(world.Events.Actors[13].Active);
        Assert.True(world.Events.Actors[13].LineEnabled);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void FilteringDependsOnPacketWidthAndSelectedShapesRatherThanFieldOrStoryConstants()
    {
        NativeWorldAllocationClause clause = new(9, 100, 28, 123, 456, ImmutableArray.Create<ushort>(0xFF01, 0, 0xFF02, 123, 0xFF03, 456, 0xFF04, 0, 0xFF13, 4, 0xFF13, 6, 0xFF05, 0));
        InfGateway[] gateways =
        [
            Gateway(17, 0),
            Gateway(18, 1),
            Gateway(19, 6),
            Gateway(20, 33),
            Gateway(106, 0),
            Gateway(21, 257)
        ];
        AutoPlayTarget[] targets = gateways.Select((gateway, index) => new AutoPlayTarget($"777:gateway:{index}:0", AutoPlayTargetKind.Gateway, 0, 0, 0, 0, DestinationField: gateway.Destination.FieldId)).Append(new AutoPlayTarget("777:line:13:0", AutoPlayTargetKind.Contact, 0, 0, 0, 0)).ToArray();
        IReadOnlyList<AutoPlayTarget> filtered = GameWorld.FilterAutoPlayWorldGatewayTargets(777, targets, gateways, clause);
        Assert.DoesNotContain(filtered, target => target.CoverageId == "777:gateway:0");
        Assert.Equal(targets.Skip(1), filtered);
    }

    [Fact]
    public void UnknownOnlyAvailableGatewaysDoNotRequireAnAllocationClauseLookup()
    {
        InfGateway[] gateways = [Gateway(17, 33), Gateway(18, 128), Gateway(19, 257), Gateway(106, 0)];
        AutoPlayTarget[] unknownOnly =
        [
            new("888:gateway:0:0", AutoPlayTargetKind.Gateway, 0, 0, 0, 0, DestinationField: 17),
            new("888:gateway:1:0", AutoPlayTargetKind.Gateway, 0, 0, 0, 0, DestinationField: 18),
            new("888:gateway:3:0", AutoPlayTargetKind.Gateway, 0, 0, 0, 0, DestinationField: 106)
        ];
        Assert.False(GameWorld.NeedsAutoPlayWorldGatewayOwnerLookup(888, unknownOnly, gateways));
        AutoPlayTarget[] withAvailableFoot = [.. unknownOnly, new("888:gateway:2:0", AutoPlayTargetKind.Gateway, 0, 0, 0, 0, DestinationField: 19)];
        Assert.True(GameWorld.NeedsAutoPlayWorldGatewayOwnerLookup(888, withAvailableFoot, gateways));
        Assert.False(GameWorld.NeedsAutoPlayWorldGatewayOwnerLookup(999, withAvailableFoot, gateways));
    }

    private static InfGateway Gateway(int destination, int mode)
    {
        return new InfGateway(Vector3.Zero, Vector3.One, new FieldTransition(destination, 0, 0, mode, 0));
    }
}
