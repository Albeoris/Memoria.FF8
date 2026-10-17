using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDoorFacingApproachTests
{
    [Fact]
    public void AConnectedSameSideApproachAdmitsTheOriginalDoorTimerCancellationAndColdResumes()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayDoorFacingApproachTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(932, run.Session.State.FieldId);
        Assert.Equal(8406, run.World.Events.Frame);
        Assert.True(run.World.Events.CanAcceptMovementInput);
        Assert.Equal(new[] { 0, 1, 5 }, run.World.Events.Scene.Party);
        JsonElement before = run.World.Events.Capture();
        GameState state = run.Session.State;
        int commands = run.Commands.Count;
        int operations = run.Operations;
        int[] locks = run.World.Events.Scene.LockedTriangles.Order().ToArray();

        AutoPlayTarget[] targets = run.World.DiscoverAutoPlayTargets().Where(target => target.CoverageId == "932:line:6").ToArray();

        Assert.Equal(3, targets.Length);
        Assert.Equal(new[] { -47f, 0f, 46f }, targets.Select(target => target.X).Order());
        Assert.All(targets, target =>
        {
            Assert.EndsWith(":facing", target.Id);
            Assert.Equal(AutoPlayTargetKind.Contact, target.Kind);
            Assert.Equal(6, target.ObjectId);
            Assert.False(target.RequiresConfirm);
            Assert.Equal(117, target.Y);
            Assert.Equal(125, target.ApproachY);
            Assert.Equal(target.X, target.ApproachX);
            Assert.Equal(7, target.Triangle);
            Assert.Equal(7, target.ApproachTriangle);
            Assert.DoesNotContain(target.Triangle, locks);
            Assert.DoesNotContain(target.ApproachTriangle!.Value, locks);
        });
        Assert.Equal(commands, run.Commands.Count);
        Assert.Equal(operations, run.Operations);
        Assert.Equal(locks, run.World.Events.Scene.LockedTriangles.Order());
        Assert.True(JsonElement.DeepEquals(before, run.World.Events.Capture()));
        Assert.Same(state, run.Session.State);

        for (int step = 0; step < 200 && !run.KilledTimer && run.Controller.Error is null; step++)
        {
            run.Controller.Advance(1);
        }

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.KilledTimer);
        int read = run.Commands.FindIndex(command => command is { FieldId: 932, ObjectId: 6, ScriptId: 31, Operation: 290, Name: "GETTIMER" });
        int kill = run.Commands.FindIndex(command => command is { FieldId: 932, ObjectId: 6, ScriptId: 31, Operation: 293, Name: "KILLTIMER" });
        Assert.True(read >= 0 && kill > read);
        Assert.False(run.Session.State.ExamTimer!.DisplayEnabled);
        Assert.Equal(0u, run.Session.State.WorldControls!.EventFlags104 & 0x40u);

        for (int callback = 0; callback < 8; callback++)
        {
            run.World.EnsureField();
            if (run.World.HasCurrentFieldAllocation && run.World.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
            {
                break;
            }
            Assert.True(run.World.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(run.World.Events.LastFault);
        }
        Assert.True(run.World.HasCurrentFieldAllocation);
        Assert.Equal(run.Session.State.FieldId, run.World.Events.FieldId);
        Assert.Equal(NativeFieldInitializationStage.Completed, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(run.Session.State.ExamTimer), JsonSerializer.SerializeToElement(cold.Session.State.ExamTimer)));
        Assert.Equal(run.Session.State.WorldControls, cold.Session.State.WorldControls);
        Assert.Equal(run.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-door-facing-approach-" + Guid.NewGuid().ToString("N"));

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
        internal AutoPlayController Controller { get; }
        internal List<ScriptCommand> Commands { get; } = [];
        internal int Operations { get; private set; }
        internal bool KilledTimer { get; private set; }
        internal bool Verified { get; set; }

        internal GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
                    world.Events.OperationCompleted += () => Operations++;
                    world.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        if (command is { FieldId: 932, ObjectId: 6, ScriptId: 31, Operation: 293, Name: "KILLTIMER" })
                        {
                            KilledTimer = true;
                        }
                    };
                }
                return world;
            }
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Verified && Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
