using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationIntegrationTests
{
    [Fact]
    public void OriginalArrivalsKeepDisconnectedNavigationScopesWithoutChangingTheNativeState()
    {
        AutoPlayNavigationReceipt main = InspectArrival("App.Tests.AutoPlayNavigationIntegrationTests.1", 244, 354, [0, 1, 2, 4, 5]);
        AutoPlayNavigationReceipt western = InspectArrival("App.Tests.AutoPlayNavigationIntegrationTests.2", 212, 26, [3]);

        Assert.Equal(main.Version, western.Version);
        Assert.Equal(main.FieldId, western.FieldId);
        Assert.Equal(main.ProgramHash, western.ProgramHash);
        Assert.Equal(main.WalkmeshHash, western.WalkmeshHash);
        Assert.Equal(main.LockedTriangles, western.LockedTriangles);
        Assert.Empty(main.ReachableTriangles.Intersect(western.ReachableTriangles));
        Assert.NotEqual(main.Key("165:142"), western.Key("165:142"));
    }

    private static AutoPlayNavigationReceipt InspectArrival(string fixture, int playerTriangle, int reachableCount, int[] gatewayIndices)
    {
        string path = TestResources.FixturePath(fixture);
        byte[] originalSnapshot = File.ReadAllBytes(path);
        GameSession session = new(GameSession.Load(path));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(165, session.State.FieldId);
        Assert.Equal(playerTriangle, world.Events.PlayerActor!.WalkmeshTriangle);
        JsonElement before = world.Events.Capture();
        GameState state = session.State;
        byte[] originalProgram = world.Field!.Jsm.ToArray();
        int executedCommands = 0;
        world.Events.CommandExecuted += _ => executedCommands++;

        AutoPlayNavigationReceipt receipt = world.CaptureAutoPlayNavigationReceipt();
        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();

        Assert.Equal(1, receipt.Version);
        Assert.Equal(165, receipt.FieldId);
        Assert.Equal(world.ReadAutoPlayFieldProgramHash(165), receipt.ProgramHash);
        Assert.Equal(world.Field.WalkmeshHash, receipt.WalkmeshHash);
        Assert.Equal(new[] { 190, 191 }, receipt.LockedTriangles);
        Assert.Equal(reachableCount, receipt.ReachableTriangles.Count);
        Assert.Contains(playerTriangle, receipt.ReachableTriangles);
        Assert.Equal(gatewayIndices.Select(index => $"165:gateway:{index}").Order(StringComparer.Ordinal), targets.Where(target => target.Kind == AutoPlayTargetKind.Gateway).Select(target => target.CoverageId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.All(targets, target =>
        {
            Assert.Contains(target.Triangle, receipt.ReachableTriangles);
            if (target.ApproachTriangle is int approach)
            {
                Assert.Contains(approach, receipt.ReachableTriangles);
            }
        });
        Assert.Equal(receipt.Key("165:142"), world.CaptureAutoPlayNavigationReceipt().Key("165:142"));
        Assert.Equal(0, executedCommands);
        Assert.Same(state, session.State);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(originalProgram, world.Field.Jsm);
        Assert.Equal(originalSnapshot, File.ReadAllBytes(path));
        Assert.Null(world.Events.LastFault);
        return receipt;
    }
}
