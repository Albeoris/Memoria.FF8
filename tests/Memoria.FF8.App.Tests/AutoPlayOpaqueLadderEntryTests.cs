using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayOpaqueLadderEntryTests
{
    [Fact]
    public void AnActualLadderEntryOverwritesItsUnknownAnimationByteNaturallyAndColdResumes()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayOpaqueLadderEntryTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(734, run.Session.State.FieldId);
        Assert.Equal(28, run.World.Events.Frame);
        ActorState player = Assert.IsType<ActorState>(run.World.Events.PlayerActor);
        Assert.Equal(10, player.ObjectId);
        Assert.Equal(new[] { 8, 255, 255 }, run.World.Events.Scene.Party);
        PartyTraceState trace = Assert.IsType<PartyTraceState>(run.World.Events.Scene.PartyTrace);
        Assert.Equal(0, trace.Cursor);
        Assert.Equal(-1, trace.FirstTrail[0]!.Animation);
        Assert.Equal(-1, trace.SecondTrail[0]!.Animation);

        run.Controller.Advance(1);

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.True(player.NativeLadder is { Active: true, Up: true });
        for (int tick = 0; tick < 4 && player.NativeMovementCompletion542 == 0 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.Null(run.World.Events.LastFault);
        Assert.Equal(3, run.World.Events.Scene.PartyTrace!.FirstTrail[0]!.Animation);
        Assert.Equal(3, run.World.Events.Scene.PartyTrace.SecondTrail[0]!.Animation);
        Assert.Equal(new[] { 8, 255, 255 }, run.World.Events.Scene.Party);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        cold.Controller.Advance(1);
        Assert.Null(cold.Controller.Error);
        Assert.Null(cold.World.Events.LastFault);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string output = Path.Combine(TestResources.ArtifactDirectory, "opaque-ladder-entry-" + Guid.NewGuid().ToString("N"));
        private GameWorld? world;

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
