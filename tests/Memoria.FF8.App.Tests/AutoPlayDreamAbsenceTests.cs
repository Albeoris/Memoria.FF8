using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDreamAbsenceTests
{
    [Fact]
    public void NativeUnavailableDreamArrivalRestoresItsSourceWithoutCompletionCreditAndSelectsAnotherGateway()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayDreamAbsenceTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameState source = GameSession.Load(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(741, run.Session.State.FieldId);
        Assert.True(run.Session.CharacterVitals!.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        AutoPlayEventCoverage Entry() => run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "741:223").Events.Single(item => item.Event.Id == "741:gateway:1");
        for (int tick = 0; tick < 32 && Entry().UnavailableAttempts.Count == 0 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.Contains(742, run.ObservedFields.AsEnumerable());
        Assert.Equal(741, run.Session.State.FieldId);
        Assert.True(JsonElement.DeepEquals(source.EventState!.Value, run.World.Events.Capture()));
        Assert.Null(run.Controller.State.ActiveEvent);
        AutoPlayEventCoverage entry = Entry();
        AutoPlayUnavailableAttempt attempt = Assert.Single(entry.UnavailableAttempts);
        Assert.Equal("741:gateway:1:0:0", attempt.TargetId);
        Assert.Equal("742:223", attempt.DestinationVisitKey);
        Assert.False(entry.Completed);
        Assert.False(entry.Unavailable);
        Assert.Null(entry.BlockedReason);
        Assert.Empty(entry.Routes);
        Assert.Empty(entry.Conditions!.ConsumedEntries);
        Assert.Empty(entry.Conditions.ConsumedExits);
        Assert.DoesNotContain(run.Controller.State.Coverage.Visits, visit => visit.Visit.Key == "742:223");
        for (int tick = 0; tick < 128 && !Entry().Unavailable && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        entry = Entry();
        Assert.True(entry.Unavailable);
        Assert.False(entry.Completed);
        Assert.Equal(entry.Event.Variants!.Order(StringComparer.Ordinal), entry.UnavailableAttempts.Select(item => item.TargetId!).Order(StringComparer.Ordinal));
        Assert.Equal(741, run.Session.State.FieldId);
        Assert.True(run.Session.CharacterVitals!.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.Equal(new[] { 10, 11, 12 }, run.World.Events.Scene.FieldPartyNativeActorIds);
        Assert.DoesNotContain(run.Controller.State.Coverage.Visits, visit => visit.Visit.Key == "742:223");
        AutoPlayPlanner planner = new(run.Controller.State.Coverage, (visit, item) => AutoPlayTraversalPolicy.PhaseOrder(visit.Region, item.Order), AutoPlayTraversalPolicy.ProgressionEntryOrder);
        AutoPlayPlanStep next = planner.Plan("741:223")!;
        Assert.StartsWith("741:gateway:", next.Event.Id, StringComparison.Ordinal);
        Assert.NotEqual(entry.Event.Id, next.Event.Id);
        AutoPlayEventCoverage alternative = run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "741:223").Events.Single(item => item.Event.Id == next.Event.Id);
        Assert.True(alternative.Available);
        if (next.IsTravel)
        {
            Assert.NotNull(next.Route);
            Assert.Contains(next.Route, alternative.Routes);
            Assert.Equal(next.Event.ConditionSignature, next.Route.ConditionSignature);
        }
        else
        {
            Assert.DoesNotContain(next.Event.ConditionSignature!, alternative.Conditions!.ConsumedEntries);
        }
        Assert.False(alternative.Unavailable);
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-dream-absence-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public HashSet<int> ObservedFields { get; } = [];

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    world.Events.CommandExecuted += command => ObservedFields.Add(command.FieldId);
                }
                return world;
            }
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
