using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayGatewayReconciliationIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void AuthenticGatewayArrivalRetiresOnlyTheStaleRecordedDestinationWithoutReplaying()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayGatewayReconciliationIntegrationTests.1");
        string directory = TestResources.GameDirectory;
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        AutoPlayEventRoute stale = Assert.IsType<AutoPlayEventRoute>(saved.ActiveTravelRoute);
        string sourceVisit = Assert.IsType<string>(saved.ActiveVisit);
        string sourceNavigation = Assert.IsType<string>(saved.ActiveNavigationKey);
        string eventId = Assert.IsType<string>(saved.ActiveEvent);
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalManifest = Hash(manifest);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        int restores = 0;
        string capture = Path.Combine(Path.GetTempPath(), "autoplay-gateway-destination-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new GameWorld(session, directory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
                restores++;
            }
            AutoPlayController controller = new(session, GetWorld, Restore, capture, deadlineTime: new RoutingClock());
            controller.ResumeFrom(manifest);
            Assert.Equal(910, session.State.FieldId);
            Assert.Equal("911:285", stale.DestinationVisitKey);
            Assert.Empty(saved.ActiveSelections!);
            int initialRestores = restores;

            controller.Advance(1);

            Assert.True(controller.Error is null, controller.Error);
            Assert.True(controller.Running);
            Assert.Equal(initialRestores, restores);
            AutoPlayEventCoverage gateway = controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == sourceVisit).Events.Single(item => item.Event.Id == eventId);
            Assert.DoesNotContain(gateway.Routes, route => SameRoute(route, stale));
            Assert.Contains(gateway.Routes, route => route.DestinationVisitKey == "910:285" && route.ConditionSignature == stale.ConditionSignature && route.TargetId == saved.ActiveTargetId && route.Selections is { Count: 0 });
            Assert.DoesNotContain(controller.State.Coverage.NavigationRoutes, edge => edge.SourceKey == sourceNavigation && edge.EventId == eventId && SameRoute(edge.Route, stale));
            Assert.Contains(controller.State.Coverage.NavigationRetirements, retired => retired.Route.SourceKey == sourceNavigation && retired.Route.EventId == eventId && SameRoute(retired.Route.Route, stale) && retired.ActualDestinationKey.StartsWith("910:285:nav:", StringComparison.Ordinal));
            Assert.True(gateway.Completed);
            Assert.Null(world?.Events.LastFault);
            output.WriteLine($"Retired={stale.DestinationVisitKey}; observed=910:285; source={eventId}; routes={gateway.Routes.Count}; additionalRestores={restores - initialRestores}");
        }
        finally
        {
            world?.Dispose();
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
            if (Directory.Exists(capture))
            {
                Directory.Delete(capture, recursive: true);
            }
        }
    }

    private static bool SameRoute(AutoPlayEventRoute left, AutoPlayEventRoute right)
    {
        return left.DestinationVisitKey == right.DestinationVisitKey && left.ConditionSignature == right.ConditionSignature && left.ObservedStoryChanged == right.ObservedStoryChanged && left.TargetId == right.TargetId && (left.Selections is null ? right.Selections is null : right.Selections is not null && left.Selections.SequenceEqual(right.Selections));
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private sealed class RoutingClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
