using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNativeInitializationAbsenceTests
{
    [Fact]
    public void CompletedNativeInitializationCertifiesAnAbsentPlayerWithoutMutatingTheDestinationOrSource()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNativeInitializationAbsenceTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        GameState state = session.State;

        NativePlayerAbsenceReceipt receipt = Assert.IsType<NativePlayerAbsenceReceipt>(world.Events.InspectNativePlayerAbsence());

        Assert.Equal(765, receipt.FieldId);
        Assert.Equal(world.ReadAutoPlayFieldProgramHash(765), receipt.ProgramHash);
        Assert.Equal(receipt.Fingerprint, world.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
        Assert.True(session.State.CharacterVitals!.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, world.Events.Scene.Party);
        Assert.Null(world.Events.PlayerActor);
        Assert.Equal(PlayerTriggerKind.Waiting, world.Events.NextTrigger.Kind);
        Assert.All(Enumerable.Range(0, 3), slot => Assert.Equal(255, world.Events.Scene.PartyBindingMemory!.At(slot)));
        Assert.All(world.Events.Scene.NativeActorObjectIds, objectId => Assert.Equal(-2, world.Events.Actors[objectId].CharacterId));
        Assert.Same(state, session.State);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        string sourcePath = TestResources.FixturePath("App.Tests.AutoPlayNativeInitializationAbsenceTests.2");
        byte[] originalSource = File.ReadAllBytes(sourcePath);
        GameSession sourceSession = new(GameSession.Load(sourcePath));
        using GameWorld source = new(sourceSession, TestResources.GameDirectory, validateWorldPresentation: false);
        source.EnsureField();
        Assert.Equal(762, sourceSession.State.FieldId);
        JsonElement sourceBefore = source.Events.Capture();
        GameState sourceState = sourceSession.State;
        byte[] originalProgram = source.Field!.Jsm.ToArray();

        Assert.True(source.CanCacheAutoPlayUnavailableSource("762:line:8", 8));
        Assert.Equal(receipt.Fingerprint, source.Events.ReadPersistentPredicateFingerprint(receipt.Reads));

        Assert.Same(sourceState, sourceSession.State);
        Assert.True(JsonElement.DeepEquals(sourceBefore, source.Events.Capture()));
        Assert.Equal(originalProgram, source.Field.Jsm);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        Assert.Equal(originalSource, File.ReadAllBytes(sourcePath));
    }

    [Fact]
    public void AnAuthenticAbsentArrivalRestoresItsSourceWithoutCompletionAndPreservesExistingParticipantPolicy()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNativeInitializationAbsenceTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameState expectedSource = GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayNativeInitializationAbsenceTests.2"));
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(765, run.Session.State.FieldId);
        AutoPlayParticipantPolicyDeferral[] originalPolicies = Policies(run);
        Assert.Equal(12, originalPolicies.Length);
        string policyMetadata = JsonSerializer.Serialize(originalPolicies, AutoPlayController.JsonOptions);
        AutoPlayEventCoverage Entry() => run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "762:223").Events.Single(item => item.Event.Id == "762:line:8");

        for (int tick = 0; tick < 8 && Entry().UnavailableAttempts.Count == 0 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        Assert.Equal(762, run.Session.State.FieldId);
        Assert.True(JsonElement.DeepEquals(expectedSource.EventState!.Value, run.World.Events.Capture()));
        Assert.Null(run.Controller.State.ActiveEvent);
        AutoPlayUnavailableAttempt attempt = Assert.Single(Entry().UnavailableAttempts);
        Assert.Equal("765:223", attempt.DestinationVisitKey);
        Assert.Equal(saved.ActiveTargetId, attempt.TargetId);
        AssertNoCompletion(Entry());
        Assert.Equal(policyMetadata, JsonSerializer.Serialize(Policies(run), AutoPlayController.JsonOptions));

        for (int tick = 0; tick < 128 && !Entry().Unavailable && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.True(Entry().Unavailable);
        Assert.Equal(Entry().Event.Variants!.Order(StringComparer.Ordinal), Entry().UnavailableAttempts.Select(item => item.TargetId!).Order(StringComparer.Ordinal));
        AssertNoCompletion(Entry());
        Assert.Equal(policyMetadata, JsonSerializer.Serialize(Policies(run), AutoPlayController.JsonOptions));
        Assert.Equal(762, run.Session.State.FieldId);
        Assert.True(run.Session.State.CharacterVitals!.DreamEnabled);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.DoesNotContain(run.Controller.State.Coverage.Visits, visit => visit.Visit.Key == "765:223");
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Null(run.World.Events.LastFault);
        run.ActiveEvents.Clear();
        for (int tick = 0; tick < 16 && run.Controller.State.ActiveEvent is null && run.ActiveEvents.Count == 0 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.State.ActiveEvent is not null || run.ActiveEvents.Count > 0);
        Assert.NotEqual("762:line:8", run.Controller.State.ActiveEvent);
        Assert.DoesNotContain("762:line:8", run.ActiveEvents);
        Assert.All(run.IssuedRequests, request => Assert.InRange(request.LocalSelector, 0, request.DeclaredScriptCount - 1));
        Assert.All(run.PartySamples, party => Assert.Equal(new[] { 8, 9, 10 }, party));
        Assert.All(run.DreamSamples, dream => Assert.True(dream));
        Assert.Equal(policyMetadata, JsonSerializer.Serialize(Policies(run), AutoPlayController.JsonOptions));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private static AutoPlayParticipantPolicyDeferral[] Policies(InstalledRun run) => run.Controller.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals).ToArray();

    private static void AssertNoCompletion(AutoPlayEventCoverage entry)
    {
        Assert.False(entry.Completed);
        Assert.Empty(entry.Routes);
        Assert.Empty(entry.Conditions!.ConsumedEntries);
        Assert.Empty(entry.Conditions.ConsumedExits);
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-initialization-absence-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<NativePartyActionRequest> Requests { get; } = [];
        public List<NativePartyActionRequest> IssuedRequests { get; } = [];
        public List<int[]> PartySamples { get; } = [];
        public List<bool> DreamSamples { get; } = [];
        public List<string> ActiveEvents { get; } = [];

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    observed.Events.BeforeNativePartyActionRequest += Requests.Add;
                    observed.Events.CommandExecuted += command =>
                    {
                        if (command.Name is "PREQ" or "PREQSW" or "PREQEW")
                        {
                            IssuedRequests.AddRange(Requests.Where(request => request.FieldId == command.FieldId && request.Frame == observed.Events.Frame && request.CallerObjectId == command.ObjectId && request.ScriptId == command.ScriptId && request.Operation == command.Operation));
                        }
                        PartySamples.Add(observed.Events.Scene.Party.ToArray());
                        DreamSamples.Add(Session.State.CharacterVitals!.DreamEnabled);
                        if (Controller.State.ActiveEvent is string activeEvent)
                        {
                            ActiveEvents.Add(activeEvent);
                        }
                    };
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
