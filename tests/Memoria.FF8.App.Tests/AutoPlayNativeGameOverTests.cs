using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNativeGameOverTests
{
    [Fact]
    public void OriginalLossPublishesItsRetainedPacketAndColdRestoresWithoutEndingOrTitleInitialization()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNativeGameOverTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Null(run.Session.State.NativeGameOver);
        NativeFieldRequestPacket packet = run.Session.State.FieldRequestPacket!;
        GameState before = run.Session.State;

        for (int callback = 0; callback < 64 && run.Session.State.NativeGameOver is null; callback++)
        {
            run.World.Events.Step();
            Assert.Null(run.World.Events.LastFault);
        }

        NativeGameOverReceipt loss = Assert.IsType<NativeGameOverReceipt>(run.Session.State.NativeGameOver);
        Assert.Equal(75, loss.FieldId);
        Assert.Equal(0, loss.ObjectId);
        Assert.Equal(1, loss.ScriptId);
        Assert.Equal(137, loss.Operation);
        Assert.Equal(4, loss.RequestKind);
        Assert.Equal(loss, run.World.Events.NativeGameOver);
        Assert.Equal(packet with { Kind = 4 }, run.Session.State.FieldRequestPacket);
        Assert.Equal(SceneKind.Field, run.Session.State.Scene);
        Assert.Equal(before.FieldId, run.Session.State.FieldId);
        Assert.Equal(before.CharacterVitals, run.Session.State.CharacterVitals);
        Assert.Equal(before.EventMemory, run.Session.State.EventMemory);
        Assert.False(run.Controller.State.ReachedEnding);
        JsonElement native = run.World.Events.Capture();
        JsonElement owner = native.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == loss.ObjectId && fiber.GetProperty("ScriptId").GetInt32() == loss.ScriptId);
        Assert.Equal(loss.Operation, owner.GetProperty("Position").GetInt32());
        Assert.False(owner.GetProperty("Complete").GetBoolean());
        run.World.Events.Step();
        Assert.True(JsonElement.DeepEquals(native, run.World.Events.Capture()));
        run.Controller.SaveAndStop();

        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.Equal(loss, cold.Session.State.NativeGameOver);
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        cold.World.Events.Step();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.False(cold.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-native-loss-" + Guid.NewGuid().ToString("N"));

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
        internal GameWorld World => world ??= new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
        internal bool Verified { get; set; }

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
