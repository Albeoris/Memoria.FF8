using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTrainSceneTests
{
    [Fact]
    public void DiagnosticPartyPlacementPreservesTheNativeFollowerStagingAndRequestCompletion()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayTrainSceneTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(114, run.Session.State.FieldId);
        int[] originalParty = run.World.Events.Scene.Party.ToArray();
        byte[] originalBindings = Enumerable.Range(0, 3).Select(run.World.Events.Scene.PartyBindingMemory!.At).ToArray();
        Assert.Equal(new[] { 0, 1, 5 }, originalParty);
        for (int tick = 0; tick < 128 && run.Controller.Error is null && !run.NativeStagingCompleted; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(originalParty, run.PartyAtContact);
        Assert.Equal(originalBindings, run.BindingsAtContact);
        Assert.Equal(new[] { (int)originalBindings[0], 255, 255 }, run.RuntimeBindingsAtContact);
        Assert.Equal(originalParty, run.World.Events.Scene.Party);
        Assert.True(run.GetInfoY[(4, 60)] >= 400);
        Assert.True(run.GetInfoY[(9, 101)] >= 400);
        Assert.Contains(run.Branches, branch => branch is { FieldId: 114, ObjectId: 4, ScriptId: 60, Operation: 1382, ConditionPassed: true });
        Assert.Contains(run.Branches, branch => branch is { FieldId: 114, ObjectId: 9, ScriptId: 101, Operation: 1999, ConditionPassed: true });
        Assert.Contains((4, 60, 1388), run.CompletedMoves.AsEnumerable());
        Assert.Contains((9, 101, 2005), run.CompletedMoves.AsEnumerable());
        Assert.Contains((4, 60, 1403), run.CompletedMoves.AsEnumerable());
        Assert.Contains((9, 101, 2011), run.CompletedMoves.AsEnumerable());
        Assert.True(run.RequestReleased);
        Assert.DoesNotContain(run.World.Events.Capture().GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 14 && fiber.GetProperty("ScriptId").GetInt32() == 140 && !fiber.GetProperty("Complete").GetBoolean() && fiber.GetProperty("Position").GetInt32() == 3341 && fiber.GetProperty("WaitReason").GetString() == "RequestEnd");
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-train-scene-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<BranchDecision> Branches { get; } = [];
        public HashSet<(int Object, int Script, int Operation)> CompletedMoves { get; } = [];
        public Dictionary<(int Object, int Script), int> GetInfoY { get; } = [];
        public int[]? PartyAtContact { get; private set; }
        public byte[]? BindingsAtContact { get; private set; }
        public int[]? RuntimeBindingsAtContact { get; private set; }
        public bool RequestReleased { get; private set; }
        public bool NativeStagingCompleted => RequestReleased && CompletedMoves.Contains((4, 60, 1403)) && CompletedMoves.Contains((9, 101, 2011));

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    world.Events.BranchEvaluated += Branches.Add;
                    world.Events.CommandExecuted += command => ObserveCommand(observed, command);
                }
                return world;
            }
        }

        private void ObserveCommand(GameWorld observed, ScriptCommand command)
        {
            if (command.FieldId != 114)
            {
                return;
            }
            if (command is { ObjectId: 14, ScriptId: 140, Name: "UCOFF" })
            {
                PartyAtContact ??= observed.Events.Scene.Party.ToArray();
                BindingsAtContact ??= Enumerable.Range(0, 3).Select(observed.Events.Scene.PartyBindingMemory!.At).ToArray();
                RuntimeBindingsAtContact ??= observed.Events.Scene.FieldPartyNativeActorIds.ToArray();
            }
            if (command.Name == "GETINFO" && command.Operation is 1378 or 1995)
            {
                JsonElement fiber = observed.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == command.ObjectId && fiber.GetProperty("ScriptId").GetInt32() == command.ScriptId && !fiber.GetProperty("Complete").GetBoolean());
                GetInfoY[(command.ObjectId, command.ScriptId)] = fiber.GetProperty("Results").GetProperty("1").GetInt32();
            }
            if (command.Name == "MOVE")
            {
                ActorState actor = observed.Events.Actors[command.ObjectId];
                if (actor.NativeScriptMovement is { Active: false } movement && movement.Operation == command.Operation && actor.NativeMovementCompletion542 == 2)
                {
                    CompletedMoves.Add((command.ObjectId, command.ScriptId, command.Operation));
                }
            }
            if (command is { ObjectId: 14, ScriptId: 140, Operation: 3343, Name: "PREQEW" })
            {
                RequestReleased = true;
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
