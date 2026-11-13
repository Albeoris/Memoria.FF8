using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayExhaustedSurveyIntegrationTests
{
    [Fact]
    public void ExhaustedRetainedFrontierStartsAnAuthenticReachableResurvey()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOPLAY_FRONTIER_MANIFEST");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest), "Configure a retained frontier manifest.");
        string game = TestResources.GameDirectory;
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest!), AutoPlayController.JsonOptions)!;
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        string directory = Path.Combine(Path.GetTempPath(), "autoplay-exhaustion-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, game, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, directory);
            controller.ResumeFrom(manifest!);
            GameWorld current = GetWorld();
            current.EnsureField();
            string visit = $"{session.State.FieldId}:{session.State.Variables.GetValueOrDefault(256)}";
            string navigation = current.CaptureAutoPlayNavigationReceipt().Key(visit);
            AutoPlayPlanner planner = new(controller.State.Coverage, (source, item) => AutoPlayTraversalPolicy.PhaseOrder(source.Region, item.Order), (visit, item) => AutoPlayTraversalPolicy.ProgressionEntryOrder(visit, item));
            Assert.Null(planner.PlanNavigation(navigation));
            Assert.Contains(controller.State.VisitGuards, item => controller.State.InspectedVisitsSinceProgress.Contains(item.Key) && current.Events.ReadPersistentPredicateFingerprint(item.Value.Reads) is string fingerprint && fingerprint != item.Value.Fingerprint);

            controller.Advance(1);

            Assert.Null(controller.Error);
            Assert.True(controller.Running);
            Assert.Equal(saved.Steps + 1, controller.State.Steps);
            Assert.NotNull(controller.State.RefreshTargetVisit);
            Assert.NotNull(controller.State.PendingExhaustedSurvey);
            Assert.NotNull(controller.State.ActiveEvent);
            Assert.NotEqual(visit, controller.State.RefreshTargetVisit);
            controller.Stop();
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
