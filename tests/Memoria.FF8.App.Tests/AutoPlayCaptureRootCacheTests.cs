using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCaptureRootCacheTests
{
    [Fact]
    public void CommittedBundleDependenciesSurviveMutationAndPartialReplacementUntilBothManifestsCommit()
    {
        using TestRun test = new();
        string dependency = test.WriteDependency("committed-before.json");
        test.Controller.State.ActiveBeforeCheckpoint = Path.GetFileName(dependency);
        string committed = test.Controller.SaveBundle("committed", test.Session.State);
        test.Controller.State.ActiveBeforeCheckpoint = null;
        string blockedTemporary = Path.Combine(test.RunDirectory, "latest.run.json.tmp");
        Directory.CreateDirectory(blockedTemporary);
        try
        {
            Exception? failure = Record.Exception(() => test.Controller.SaveBundle("partial-replacement", test.Session.State));
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }
        finally
        {
            Directory.Delete(blockedTemporary);
        }

        test.Controller.MaintainCapture();

        Assert.True(File.Exists(dependency));
        Assert.True(File.Exists(committed));
        AutoPlayRunState persisted = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(Path.Combine(test.RunDirectory, "latest.run.json")), AutoPlayController.JsonOptions)!;
        Assert.Equal("committed-before.json", persisted.ActiveBeforeCheckpoint);
        Assert.Equal("committed.json", persisted.Snapshot);
        Assert.Equal(test.Session.State.FieldId, AutoPlayCheckpointStore.Load(dependency).FieldId);
        Assert.False(File.Exists(Path.Combine(test.RunDirectory, "partial-replacement.run.json")));

        test.Controller.SaveBundle("replacement", test.Session.State);
        test.Controller.MaintainCapture();

        Assert.False(File.Exists(dependency));
        Assert.False(File.Exists(committed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RollbackDependenciesSurviveMutationAndFailedSerializationOrDependencyCollection(bool failDependencies)
    {
        using TestRun test = new();
        string dependency = test.WriteDependency("rollback-before.json");
        test.Controller.State.ActiveBeforeCheckpoint = Path.GetFileName(dependency);
        ReadOnlyMemory<byte> rollback = test.Controller.CaptureControllerRollback();
        byte[] retained = rollback.ToArray();
        test.Controller.State.ActiveBeforeCheckpoint = null;
        if (failDependencies)
        {
            test.Controller.State.Coverage.Choices.Add(new() { Key = "invalid-policy", PolicyCheckpoints = null! });
            Assert.Throws<NullReferenceException>(() => test.Controller.CaptureControllerRollback());
            test.Controller.State.Coverage.Choices.Clear();
        }
        else
        {
            test.Controller.State.ActiveTravelRoute = new("destination", null, null, new ThrowingSelections());
            Assert.Throws<InvalidOperationException>(() => test.Controller.CaptureControllerRollback());
            test.Controller.State.ActiveTravelRoute = null;
        }
        Assert.Equal(retained, rollback.ToArray());
        test.Controller.SaveBundle("after-failure", test.Session.State);

        test.Controller.MaintainCapture();

        Assert.True(File.Exists(dependency));
        Assert.Equal(test.Session.State.FieldId, AutoPlayCheckpointStore.Load(dependency).FieldId);
        test.Controller.CaptureControllerRollback();
        test.Controller.SaveBundle("after-replacement", test.Session.State);
        test.Controller.MaintainCapture();
        Assert.False(File.Exists(dependency));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartingOrResumingAnotherRunResetsThePreviousRoots(bool resume)
    {
        using TestRun test = new();
        string dependency = test.WriteDependency("previous-run-before.json");
        test.Controller.State.ActiveBeforeCheckpoint = Path.GetFileName(dependency);
        string manifest = test.Controller.SaveBundle("previous-run", test.Session.State);
        test.Controller.CaptureControllerRollback();
        string previousDirectory = test.RunDirectory;
        if (resume)
        {
            test.Controller.ResumeFrom(manifest);
        }
        else
        {
            test.Controller.StartFromCurrentState();
        }

        test.Controller.MaintainCapture();

        Assert.True(File.Exists(dependency));
        Assert.Contains(Directory.EnumerateDirectories(test.Directory), directory => directory != previousDirectory);
        Assert.Null(test.Controller.Error);
    }

    private sealed class ThrowingSelections : IReadOnlyList<AutoPlayChoiceSelection>
    {
        public int Count => 1;
        public AutoPlayChoiceSelection this[int index] => throw new InvalidOperationException("Synthetic serialization failure.");

        public IEnumerator<AutoPlayChoiceSelection> GetEnumerator()
        {
            throw new InvalidOperationException("Synthetic serialization failure.");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class TestRun : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "autoplay-root-cache-" + Guid.NewGuid().ToString("N"));
        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public AutoPlayController Controller { get; }
        public string RunDirectory { get; }

        public TestRun()
        {
            Session.Restore(Session.State with { Scene = SceneKind.Field, FieldId = 130 });
            Controller = new(Session, () => throw new InvalidOperationException("Retention must not allocate a world."), Session.Restore, Directory);
            Controller.StartFromCurrentState();
            RunDirectory = System.IO.Directory.GetDirectories(Directory).Single();
        }

        public string WriteDependency(string name)
        {
            string path = Path.Combine(RunDirectory, name);
            Session.Save(path, compact: true);
            return path;
        }

        public void Dispose()
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
