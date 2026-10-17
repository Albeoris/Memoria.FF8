using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPhysicalRequestTests
{
    [Fact]
    public void AnActualPhysicalRequestCheckpointSurvivesColdRestoreWithoutCompletionCredit()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayPhysicalRequestTests.2");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(764, run.Session.State.FieldId);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.Equal(9, run.World.Events.EventMemory.KnownValues[340]);
        Assert.True(run.World.Events.Actors[9].LineEnabled);
        byte[] originalBindings = Enumerable.Range(0, 3).Select(run.World.Events.Scene.PartyBindingMemory!.At).ToArray();
        Assert.Equal(new byte[] { 6, 7, 8 }, originalBindings);
        Assert.Equal(new[] { 22, 23, 24 }, originalBindings.Select(index => run.World.Events.Scene.NativeActorObjectIds[index]));
        AutoPlayEventCoverage Entry() => run.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "764:223").Events.Single(item => item.Event.Id == "764:line:9");
        Assert.False(Entry().Completed);

        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(764, run.Session.State.FieldId);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.All(run.PartySamples, party => Assert.Equal(new[] { 8, 9, 10 }, party));
        Assert.Equal(originalBindings, Enumerable.Range(0, 3).Select(run.World.Events.Scene.PartyBindingMemory!.At));
        Assert.False(run.World.Events.IsUserControlEnabled);
        Assert.False(Entry().Completed);
        Assert.Empty(Entry().Routes);
        Assert.Empty(Entry().UnavailableAttempts);
        Assert.Equal("764:line:9", run.Controller.State.ActiveEvent);
        Assert.False(run.Controller.State.ReachedEnding);
        run.Controller.SaveAndStop();
        string pendingManifest = run.Controller.ReproductionPath!;
        byte[] pendingManifestBytes = File.ReadAllBytes(pendingManifest);
        AutoPlayRunState pendingRun = JsonSerializer.Deserialize<AutoPlayRunState>(pendingManifestBytes, AutoPlayController.JsonOptions)!;
        string pendingSnapshot = Path.Combine(Path.GetDirectoryName(pendingManifest)!, pendingRun.Snapshot!);
        byte[] pendingBytes = File.ReadAllBytes(pendingSnapshot);
        GameState pendingState = GameSession.Load(pendingSnapshot);
        AssertRequestReceipt(run.World.Events.Capture());
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(pendingManifest);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.Equal(run.World.Apply(new(PlayerActionKind.Wait, Frames: 1)), cold.World.Apply(new(PlayerActionKind.Wait, Frames: 1)));
        Assert.Contains(run.Commands.Concat(cold.Commands), command => command is { FieldId: 764, ObjectId: 22, ScriptId: 162, Operation: 2525, Name: "HALT" });
        Assert.True(JsonElement.DeepEquals(run.World.Events.Capture(), cold.World.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(run.Session.State), JsonSerializer.SerializeToElement(cold.Session.State)));
        Assert.Equal(run.Controller.State.ActiveEvent, cold.Controller.State.ActiveEvent);
        Assert.False(cold.Controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "764:223").Events.Single(item => item.Event.Id == "764:line:9").Completed);
        Assert.False(cold.World.Events.IsUserControlEnabled);
        AssertRequestReceipt(cold.World.Events.Capture());
        AssertMalformedReceiptRejected(pendingState, removeReceipt: false);
        AssertMalformedReceiptRejected(pendingState, removeReceipt: true);
        Assert.Equal(pendingManifestBytes, File.ReadAllBytes(pendingManifest));
        Assert.Equal(pendingBytes, File.ReadAllBytes(pendingSnapshot));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private static void AssertRequestReceipt(JsonElement state)
    {
        JsonElement fiber = state.GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ObjectId").GetInt32() == 22 && item.GetProperty("ScriptId").GetInt32() == 162 && !item.GetProperty("Complete").GetBoolean());
        JsonElement receipt = fiber.GetProperty("NativePartyRequestPointer");
        Assert.Equal(1, receipt.GetProperty("Version").GetInt32());
        Assert.Equal(6, receipt.GetProperty("LocalSelector").GetInt32());
        Assert.Equal(162, receipt.GetProperty("PointerIndex").GetInt32());
    }

    private static void AssertMalformedReceiptRejected(GameState source, bool removeReceipt)
    {
        JsonObject state = JsonNode.Parse(source.EventState!.Value.GetRawText())!.AsObject();
        JsonObject fiber = state["Fibers"]!.AsArray().Single(item => item!["ObjectId"]!.GetValue<int>() == 22 && item["ScriptId"]!.GetValue<int>() == 162 && !item["Complete"]!.GetValue<bool>())!.AsObject();
        if (removeReceipt)
        {
            fiber.Remove("NativePartyRequestPointer");
        }
        else
        {
            fiber["NativePartyRequestPointer"]!["PointerIndex"] = 163;
        }
        GameState invalid = source with { EventState = JsonSerializer.SerializeToElement(state) };
        using InstalledRun rejected = new();
        rejected.Session.Restore(invalid);
        Assert.Throws<InvalidDataException>(() => rejected.World.EnsureField());
        Assert.True(JsonElement.DeepEquals(invalid.EventState!.Value, rejected.Session.State.EventState!.Value));
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-physical-request-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<ScriptCommand> Commands { get; } = [];
        public List<BranchDecision> Branches { get; } = [];
        public List<int[]> PartySamples { get; } = [];

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    world.Events.BranchEvaluated += Branches.Add;
                    world.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        PartySamples.Add(observed.Events.Scene.Party.ToArray());
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
