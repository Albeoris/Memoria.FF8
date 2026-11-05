using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayFrontierContinuationTests
{
    [Fact]
    public void OriginalOneWayStudioEntryReturnsToItsStandaloneSourceWithoutChangingQuestProgress()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayFrontierContinuationTests.1");
        string checkpoint = TestResources.FixturePath("App.Tests.AutoPlayFrontierContinuationTests.2");
        byte[] original = SHA256.HashData(File.ReadAllBytes(checkpoint));
        AutoPlayRunState observed = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        GameSession session = new(GameSession.Load(checkpoint));
        Assert.Equal(927, session.State.FieldId);
        Assert.Equal(285, session.State.WorldEntryMemory!.StoryWord);
        GameWorld? world = null;
        string output = Path.Combine(Path.GetTempPath(), "autoplay-frontier-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, TestResources.GameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output, deadlineTime: new InspectionClock());
            GetWorld().EnsureField();
            session.SetEventState(GetWorld().Events.Capture());
            controller.StartFromCurrentState();
            AutoPlayPlannerState coverage = controller.State.Coverage;
            coverage.Visits.AddRange(observed.Coverage.Visits);
            coverage.Choices.AddRange(observed.Coverage.Choices);
            coverage.NavigationVisits.AddRange(observed.Coverage.NavigationVisits);
            coverage.NavigationRoutes.AddRange(observed.Coverage.NavigationRoutes);
            coverage.NavigationRetirements.AddRange(observed.Coverage.NavigationRetirements);
            controller.State.NavigationReceipts = observed.NavigationReceipts;
            controller.State.VisitGuards = observed.VisitGuards;
            controller.State.ExhaustedSurveyInspections = observed.ExhaustedSurveyInspections;
            AutoPlayVisitCoverage source = coverage.Visits.Single(visit => visit.Visit.Key == "927:285");
            foreach (AutoPlayEventCoverage item in source.Events)
            {
                if (item.Event.Id == "927:line:13")
                {
                    item.Completed = false;
                    item.Conditions = new();
                    item.Routes.Clear();
                }
                else
                {
                    item.Completed = true;
                    item.BlockedReason = "This fixture isolates the original studio transition.";
                    item.Conditions ??= new();
                    item.Conditions.ConsumedEntries.Add(GetWorld().ReadAutoPlayConditions(item.Event.Id, item.Event.Id.Contains(":gateway:", StringComparison.Ordinal) ? -1 : 9).Fingerprint);
                }
            }
            for (int step = 0; step < 200 && controller.State.NavigationExcursionBlocks.Count == 0; step++)
            {
                controller.Advance(1);
                Assert.True(controller.Error is null, controller.Error);
            }
            Assert.NotEmpty(controller.State.NavigationExcursionBlocks);
            Assert.Equal(927, session.State.FieldId);
            Assert.Equal(285, session.State.WorldEntryMemory!.StoryWord);
            Assert.Null(controller.State.NavigationExcursion);
            Assert.Null(source.Events.Single(item => item.Event.Id == "927:line:13").BlockedReason);
            Assert.DoesNotContain(controller.State.NavigationExcursionBlocks, blocked => blocked.SourceVisit != "927:285");
            Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(checkpoint)));
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private sealed class InspectionClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
