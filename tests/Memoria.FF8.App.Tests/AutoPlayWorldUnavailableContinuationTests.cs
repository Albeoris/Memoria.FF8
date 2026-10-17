using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldUnavailableContinuationTests
{
    [Fact]
    public void AnActuallyAbsentWorldBodyRestoresOnlyItsAuthenticSourceAndColdPersistsTheUnavailableAttempt()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayWorldUnavailableContinuationTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string directory = Path.GetDirectoryName(manifest)!;
        string snapshot = Path.Combine(directory, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameState before = GameSession.Load(Path.Combine(directory, saved.ActiveBeforeCheckpoint!));
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.Controller.Advance(1);

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.Controller.State.ActiveEvent);
        Assert.Equal(SceneKind.Field, run.Session.State.Scene);
        Assert.Equal(before.FieldId, run.Session.State.FieldId);
        Assert.True(run.World.Events.CanAcceptMovementInput);
        Assert.True(JsonElement.DeepEquals(before.EventState!.Value, run.World.Events.Capture()));
        Assert.Equal(0, run.World.Events.EventMemory.KnownValues[720]);
        KeyValuePair<string, AutoPlayWorldAbsenceReceipt> guard = Assert.Single(run.Controller.State.WorldUnavailabilityGuards);
        Assert.Equal(0, guard.Value.EntityType);
        Assert.Equal(170, guard.Value.Story);
        Assert.Equal(guard.Value.AllocationFingerprint, run.World.ReadAutoPlayWorldAbsenceFingerprint(guard.Value));
        AutoPlayEventCoverage interaction = run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == saved.ActiveVisit).Events.Single(item => item.Event.Id == saved.ActiveEvent);
        AutoPlayUnavailableAttempt attempt = Assert.Single(interaction.UnavailableAttempts);
        Assert.Equal(saved.ActiveTargetId, attempt.TargetId);
        Assert.Equal(saved.ActiveConditionSignature, attempt.SourceConditionSignature);
        Assert.Equal(guard.Key, attempt.GuardKey);
        Assert.False(interaction.Completed);
        Assert.Empty(interaction.Routes);
        Assert.Null(interaction.Conditions?.ActiveEntry);
        Assert.Equal(JsonSerializer.Serialize(saved.Coverage.NavigationRoutes, AutoPlayController.JsonOptions), JsonSerializer.Serialize(run.Controller.State.Coverage.NavigationRoutes, AutoPlayController.JsonOptions));
        Assert.Equal(saved.Coverage.Choices.Count, run.Controller.State.Coverage.Choices.Count);
        Assert.Equal(saved.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count), run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count));

        string guards = JsonSerializer.Serialize(run.Controller.State.WorldUnavailabilityGuards, AutoPlayController.JsonOptions);
        run.Controller.SaveAndStop();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(guards, JsonSerializer.Serialize(cold.Controller.State.WorldUnavailabilityGuards, AutoPlayController.JsonOptions));
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-autoplay-world-unavailable-" + Guid.NewGuid().ToString("N"));

        internal InstalledRun()
        {
            Controller = new(Session, () => World, state =>
            {
                world?.Dispose();
                world = null;
                Session.Restore(state);
            }, output);
        }

        internal GameSession Session { get; } = GameSession.CreateColdProcess();
        internal GameWorld World => world ??= new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
        internal AutoPlayController Controller { get; }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
