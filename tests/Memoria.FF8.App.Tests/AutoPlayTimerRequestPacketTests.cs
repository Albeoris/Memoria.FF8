using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTimerRequestPacketTests
{
    [Fact]
    public void ActualTimerExpiryPreservesItsRawPacketThroughFreshAllocationAndColdRestoreBeforeInitialization()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayTimerRequestPacketTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string sourceDirectory = Path.GetDirectoryName(manifest)!;
        string snapshot = Path.Combine(sourceDirectory, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        string sourceImport = Path.GetFullPath(saved.ActiveBeforeCheckpoint!, sourceDirectory);
        byte[] originalImport = File.ReadAllBytes(sourceImport);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(902, run.Session.State.FieldId);
        Assert.Equal(1075, run.World.Events.Frame);
        NativeFieldRequestPacket packet = Assert.IsType<NativeFieldRequestPacket>(run.Session.State.FieldRequestPacket);
        Assert.Equal(new NativeFieldRequestPacket { Kind = 0, FieldId = 902, X = -1183, Y = 2455, ExtraWord = 923, Triangle = 48, DirectionWord = 0 }, packet);

        run.Controller.Advance(1);

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        Assert.Equal(75, run.Session.State.FieldId);
        Assert.Null(run.Session.State.EventState);
        JsonElement departure = run.Session.State.DepartingFieldState!.Value;
        Assert.Equal(902, departure.GetProperty("FieldId").GetInt32());
        Assert.Equal(1076, departure.GetProperty("Frame").GetInt64());
        Assert.True(departure.GetProperty("TransitionRequested").GetBoolean());
        NativeFieldRequestPacket expired = packet with { Kind = 1, FieldId = 75, Triangle = 0x7FFF };
        Assert.Equal(expired, run.Session.State.FieldRequestPacket);
        Assert.Equal(new FieldTransition(75, -1183, 2455, 0x7FFF, 0, 923), run.Session.State.FieldArrival);
        Assert.All(run.Session.State.ExamTimer!.Alternatives, alternative => Assert.Equal(0, alternative.Remaining));

        // Allocation preserves the pending raw arrival; neither INIT nor its first ordinary default has executed yet.
        run.World.EnsureField();

        Assert.Equal(75, run.World.Events.FieldId);
        Assert.Equal(0, run.World.Events.Frame);
        Assert.True(run.World.HasCurrentFieldAllocation);
        Assert.Equal(NativeFieldInitializationStage.Allocated, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        Assert.Equal(expired.WithConsumedRequest(), run.Session.State.FieldRequestPacket);
        JsonElement allocated = run.World.Events.Capture();
        Assert.False(allocated.GetProperty("DefaultsStarted").GetBoolean());
        Assert.False(allocated.GetProperty("TransitionRequested").GetBoolean());
        Assert.Equal(new FieldTransition(75, -1183, 2455, 0x7FFF, 0, 923), allocated.GetProperty("PendingArrival").Deserialize<FieldTransition>());
        Assert.Null(run.World.Events.LastFault);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        string checkpointDirectory = Path.GetDirectoryName(run.Controller.ReproductionPath!)!;
        Dictionary<string, byte[]> imports = new(StringComparer.Ordinal);
        foreach (string reference in ReferencedCheckpoints(run.Controller.State))
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(reference, checkpointDirectory));
            imports.TryAdd("import-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".json", bytes);
        }
        Assert.NotEmpty(imports);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.Equal(run.Session.State.FieldRequestPacket, cold.Session.State.FieldRequestPacket);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(run.Session.State.ExamTimer), JsonSerializer.SerializeToElement(cold.Session.State.ExamTimer)));
        foreach ((string filename, byte[] bytes) in imports)
        {
            string imported = Assert.Single(Directory.GetFiles(cold.Output, filename, SearchOption.AllDirectories));
            Assert.Equal(bytes, File.ReadAllBytes(imported));
        }

        // Exercise process-owner restore semantics without executing gameplay or changing a field-local body.
        NativeFieldRequestPacket current = run.Session.State.FieldRequestPacket!;
        NativeFieldRequestPacket changedProcessOwner = current with { X = 17, Y = -23, DirectionWord = 0xABCD };
        run.Session.SetFieldRequestPacket(changedProcessOwner);
        JsonElement beforeFieldRestore = run.World.Events.Capture();
        run.World.Events.RestoreFieldState(native);
        Assert.Equal(changedProcessOwner, run.Session.State.FieldRequestPacket);
        Assert.True(JsonElement.DeepEquals(beforeFieldRestore, run.World.Events.Capture()));
        run.World.Events.Restore(native);
        Assert.Equal(current, run.Session.State.FieldRequestPacket);
        Assert.True(JsonElement.DeepEquals(native, run.World.Events.Capture()));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        Assert.Equal(originalImport, File.ReadAllBytes(sourceImport));
        run.Verified = true;
        cold.Verified = true;
    }

    private static IEnumerable<string> ReferencedCheckpoints(AutoPlayRunState state)
    {
        IEnumerable<string?> choices = state.Coverage.Choices.Where(choice => !choice.Finished).SelectMany(choice => new[] { choice.BeforeCheckpoint, choice.ContinuationCheckpoint });
        IEnumerable<string?> policy = state.Coverage.Choices.SelectMany(choice => choice.PolicyCheckpoints.Values).SelectMany(checkpoint => new[] { checkpoint.BeforeCheckpoint, checkpoint.ContinuationCheckpoint });
        return choices.Concat(policy).Append(state.PrimaryContinuation).Append(state.ActiveBeforeCheckpoint).OfType<string>().Distinct(StringComparer.Ordinal);
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        internal string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-timer-request-packet-" + Guid.NewGuid().ToString("N"));

        internal InstalledRun()
        {
            Controller = new(Session, () => World, state =>
            {
                world?.Dispose();
                world = null;
                Session.Restore(state);
            }, Output);
        }

        internal GameSession Session { get; } = GameSession.CreateColdProcess();
        internal GameWorld World => world ??= new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
        internal AutoPlayController Controller { get; }
        internal bool Verified { get; set; }

        public void Dispose()
        {
            world?.Dispose();
            if (Verified && Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
