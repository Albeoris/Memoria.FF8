using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayParticipantPolicyTests
{
    [Fact]
    public void NaturalComponentTraversalUsesTheNativeExitWithoutAttemptingDisconnectedParticipantActions()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayParticipantPolicyTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(764, run.Session.State.FieldId);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        byte[] bindings = Bindings(run);
        Assert.Equal(new byte[] { 6, 7, 8 }, bindings);
        Assert.Equal(new[] { 22, 23, 24 }, bindings.Select(index => run.World.Events.Scene.NativeActorObjectIds[index]));
        AutoPlayEventCoverage Entry() => run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "764:223").Events.Single(item => item.Event.Id == "764:line:9");
        Assert.False(Entry().Completed);
        Assert.DoesNotContain(run.World.DiscoverAutoPlayTargets(), target => target.CoverageId is "764:line:9" or "764:talk:17");
        Assert.Empty(run.Controller.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals));

        for (int tick = 0; tick < 32 && run.Session.State.FieldId == 764 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(762, run.Session.State.FieldId);
        Assert.NotEmpty(run.ActiveEvents);
        Assert.Equal("764:line:10", run.ActiveEvents[0]);
        Assert.DoesNotContain("764:line:9", run.ActiveEvents);
        AutoPlayEventCoverage entry = Entry();
        Assert.False(entry.Available);
        Assert.False(entry.Completed);
        Assert.False(entry.Unavailable);
        Assert.Empty(entry.UnavailableAttempts);
        Assert.Empty(entry.Routes);
        Assert.False(entry.PolicyDeferred);
        Assert.Empty(entry.PolicyDeferredVariants);
        Assert.Empty(entry.ParticipantPolicyDeferrals);
        Assert.True(entry.Conditions is null || entry.Conditions.ConsumedEntries.Count == 0 && entry.Conditions.ConsumedExits.Count == 0);
        Assert.Empty(run.Controller.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals));
        Assert.DoesNotContain(run.Requests, request => request.PhysicalCodeObjectId != request.ReceiverObjectId);
        Assert.Empty(run.InputMasks);
        AssertNativePolicyBoundary(run);
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }
    private static byte[] Bindings(InstalledRun run) => Enumerable.Range(0, 3).Select(run.World.Events.Scene.PartyBindingMemory!.At).ToArray();

    private static void AssertNativePolicyBoundary(InstalledRun run)
    {
        Assert.Null(run.World.Events.LastFault);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.All(run.PartySamples, party => Assert.Equal(new[] { 8, 9, 10 }, party));
        Assert.DoesNotContain(run.Commands, command => command is { FieldId: 764, ObjectId: 9, ScriptId: 81, Operation: 1668, Name: "PREQ" });
        Assert.DoesNotContain(run.Commands, command => command is { FieldId: 764, ObjectId: 22, ScriptId: 162 });
        Assert.DoesNotContain(run.Commands, command => command is { FieldId: 764, ObjectId: 24, ScriptId: 171 });
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-participant-policy-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<ScriptCommand> Commands { get; } = [];
        public List<NativePartyActionRequest> Requests { get; } = [];
        public List<int[]> PartySamples { get; } = [];
        public List<int> InputMasks { get; } = [];
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
                        Commands.Add(command);
                        PartySamples.Add(observed.Events.Scene.Party.ToArray());
                        if (Controller.State.ActiveEvent is string activeEvent)
                        {
                            ActiveEvents.Add(activeEvent);
                        }
                        if (command is { FieldId: 764, ObjectId: 9, ScriptId: 81, Operation: 1660, Name: "KEYSCAN" })
                        {
                            InputMasks.Add(Session.State.FieldButtonInput!.PhysicalHeld);
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
