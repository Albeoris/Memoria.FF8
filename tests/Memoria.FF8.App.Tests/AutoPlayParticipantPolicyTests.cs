using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayParticipantPolicyTests
{
    [Fact]
    public void MissingAuthoredPartyActionDefersRealInputAlternativesWithoutExecutingForeignCodeOrCreditingCompletion()
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
        HashSet<string> previousChoices = run.Controller.State.Coverage.Choices.Select(choice => choice.Key).ToHashSet(StringComparer.Ordinal);
        AutoPlayEventCoverage Entry(InstalledRun current) => current.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "764:223").Events.Single(item => item.Event.Id == "764:line:9");
        Assert.False(Entry(run).Completed);

        for (int tick = 0; tick < 1024 && !Entry(run).PolicyDeferred && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }

        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        AutoPlayEventCoverage entry = Entry(run);
        Assert.True(entry.PolicyDeferred);
        Assert.NotEmpty(entry.ParticipantPolicyDeferrals);
        Assert.False(entry.Completed);
        Assert.False(entry.Unavailable);
        Assert.Empty(entry.UnavailableAttempts);
        Assert.Empty(entry.Routes);
        Assert.Equal(entry.Event.Variants!.Order(StringComparer.Ordinal), entry.PolicyDeferredVariants.Order(StringComparer.Ordinal));
        Assert.All(entry.ParticipantPolicyDeferrals, receipt =>
        {
            Assert.Contains(receipt.TargetId, entry.Event.Variants!);
            Assert.Equal(764, receipt.Request.FieldId);
            Assert.Equal(9, receipt.Request.CallerObjectId);
            Assert.Equal(81, receipt.Request.ScriptId);
            Assert.Equal(1668, receipt.Request.Operation);
            Assert.Equal(0, receipt.Request.PartySlot);
            Assert.Equal(6, receipt.Request.LocalSelector);
            Assert.Equal(22, receipt.Request.ReceiverObjectId);
            Assert.Equal(8, receipt.Request.ReceiverCharacterId);
            Assert.Equal(6, receipt.Request.PhysicalBinding);
            Assert.Equal(5, receipt.Request.DeclaredScriptCount);
            Assert.Equal(162, receipt.Request.PhysicalPointerIndex);
            Assert.NotEmpty(receipt.Selections);
        });
        AutoPlayChoiceCoverage[] inputs = run.Controller.State.Coverage.Choices.Where(choice => !previousChoices.Contains(choice.Key) && choice.Key.StartsWith("input:764:", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(inputs);
        Assert.All(inputs, choice =>
        {
            Assert.True(choice.PolicyExhausted);
            Assert.False(choice.Finished);
            Assert.Empty(choice.CompletedOptions);
            Assert.Equal(Enumerable.Range(choice.FirstOption, choice.OptionCount), choice.PolicyDeferredOptions.Order());
        });
        Assert.Contains(run.InputMasks, mask => mask == 64);
        Assert.Contains(run.InputMasks, mask => mask == 128);
        Assert.NotEmpty(run.Requests);
        AssertNativePolicyBoundary(run);
        Assert.Equal(bindings, Bindings(run));
        Assert.Null(run.Controller.State.ActiveEvent);
        Assert.False(run.Controller.State.ReachedEnding);

        string receipts = JsonSerializer.Serialize(entry.ParticipantPolicyDeferrals, AutoPlayController.JsonOptions);
        run.Controller.SaveAndStop();
        string portableManifest = run.Controller.ReproductionPath!;
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(portableManifest);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(receipts, JsonSerializer.Serialize(Entry(cold).ParticipantPolicyDeferrals, AutoPlayController.JsonOptions));
        Assert.True(Entry(cold).PolicyDeferred);
        Assert.Equal(bindings, Bindings(cold));
        long before = cold.Controller.State.Steps;
        for (int tick = 0; tick < 16 && cold.Controller.State.ActiveEvent is null && cold.ActiveEvents.Count == 0 && cold.Controller.Error is null; tick++)
        {
            cold.Controller.Advance(1);
        }
        Assert.Null(cold.Controller.Error);
        Assert.True(cold.Controller.State.Steps > before);
        Assert.True(cold.Controller.State.ActiveEvent is not null || cold.ActiveEvents.Count > 0);
        Assert.NotEqual("764:line:9", cold.Controller.State.ActiveEvent);
        Assert.DoesNotContain("764:line:9", cold.ActiveEvents);
        Assert.Empty(cold.Requests);
        Assert.Equal(receipts, JsonSerializer.Serialize(Entry(cold).ParticipantPolicyDeferrals, AutoPlayController.JsonOptions));
        AssertNativePolicyBoundary(cold);
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
