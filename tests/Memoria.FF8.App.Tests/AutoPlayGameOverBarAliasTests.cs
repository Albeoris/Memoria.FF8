using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayGameOverBarAliasTests
{
    [Fact]
    public void OriginalGameOverScriptExecutesItsAdjacentWritesAndColdRestoresWithoutClaimingGameOverCompletion()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayGameOverBarAliasTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(75, run.Session.State.FieldId);
        Assert.Null(run.Session.State.EventState);
        run.World.EnsureField();
        Assert.Equal(0, run.World.Events.Frame);
        Assert.Equal(NativeFieldInitializationStage.Allocated, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        NativeFieldBarsState initialBars = run.Session.State.FieldBars!;
        Assert.Null(initialBars.First);
        Assert.Null(initialBars.Second);
        uint expectedMask = (run.Session.State.VibrationProcess!.ResourcePointerAndMask ?? uint.MaxValue) & 0xFFFFFF7Fu;

        for (int callback = 0; callback < 32 && !run.ReachedAlias; callback++)
        {
            run.World.Events.Step();
            Assert.Null(run.World.Events.LastFault);
        }

        Assert.True(run.ReachedAlias);
        Assert.Contains(run.Commands, command => command is { FieldId: 75, ObjectId: 0, ScriptId: 1, Operation: 122, Name: "KILLBAR" } && command.Arguments.SequenceEqual(new[] { 2 }));
        Assert.DoesNotContain(run.Commands, command => command.Name == "GAMEOVER");
        Assert.Equal(75, run.Session.State.FieldId);
        Assert.Equal(NativeFieldInitializationStage.Completed, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        Assert.Equal((ushort)0, run.Session.State.FieldBars!.AdjacentMirrorWord);
        Assert.Equal(initialBars.First, run.Session.State.FieldBars.First);
        Assert.Equal(initialBars.Second, run.Session.State.FieldBars.Second);
        Assert.Equal(expectedMask, run.Session.State.VibrationProcess!.ResourcePointerAndMask);
        Assert.False(run.Controller.State.ReachedEnding);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        JsonElement owner = native.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
        Assert.True(owner.GetProperty("Position").GetInt32() > 122);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.Equal(run.Session.State.FieldBars, cold.Session.State.FieldBars);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(run.Session.State.VibrationProcess), JsonSerializer.SerializeToElement(cold.Session.State.VibrationProcess)));

        NativeFieldBarsState bars = run.Session.State.FieldBars!;
        NativeVibrationProcessState vibration = run.Session.State.VibrationProcess!;
        NativeFieldBarsState changedBars = bars with { AdjacentMirrorWord = 0x1234 };
        NativeVibrationProcessState changedVibration = vibration.AndResourcePointer(0xFFFF0FFF);
        run.Session.SetNativeFieldBarAliasOwners(changedBars, changedVibration);
        JsonElement changed = run.World.Events.Capture();
        run.World.Events.RestoreFieldState(native);
        Assert.Equal(changedBars, run.Session.State.FieldBars);
        Assert.Equal(changedVibration, run.Session.State.VibrationProcess);
        Assert.True(JsonElement.DeepEquals(changed, run.World.Events.Capture()));
        run.World.Events.Restore(native);
        Assert.True(JsonElement.DeepEquals(native, run.World.Events.Capture()));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-game-over-bar-alias-" + Guid.NewGuid().ToString("N"));

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
        internal bool ReachedAlias { get; private set; }
        internal bool Verified { get; set; }

        internal GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
                    world.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        if (command is { FieldId: 75, ObjectId: 0, ScriptId: 1, Operation: 122, Name: "KILLBAR" })
                        {
                            ReachedAlias = true;
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
