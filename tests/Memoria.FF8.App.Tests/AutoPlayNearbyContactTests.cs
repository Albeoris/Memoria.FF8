using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNearbyContactTests
{
    [Fact]
    public void NearbyNativeContactCanStartWithoutReachingItsBlockedApproachSample()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNearbyContactTests.1");
        string output = Path.Combine(Path.GetTempPath(), "ff8-nearby-contact-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new GameWorld(session, TestResources.GameDirectory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output);
            controller.ResumeFrom(manifest);
            GameWorld current = GetWorld();
            current.EnsureField();
            AutoPlayTarget target = current.DiscoverAutoPlayTargets().First(candidate => candidate.Kind == AutoPlayTargetKind.Contact && current.Events.IsAutoPlayHeldContactNearby(candidate.ObjectId));
            var before = current.Events.Scene.NativePlayerPosition;
            Assert.False(current.Events.HasAutoPlayHeldContact(target.ObjectId));

            controller.Advance(1);

            Assert.Null(controller.Error);
            Assert.Equal(target.CoverageId, controller.State.ActiveEvent);
            Assert.Equal(before, current.Events.Scene.NativePlayerPosition);
            Assert.True(current.Events.HasAutoPlayHeldContact(target.ObjectId));
            AutoPlayEventCoverage coverage = controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == controller.State.ActiveVisit).Events.Single(item => item.Event.Id == target.CoverageId);
            Assert.Null(coverage.BlockedReason);
            Assert.False(coverage.Completed);
            Assert.Null(current.Events.LastFault);
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
