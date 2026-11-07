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

    [Fact]
    public void AnAttemptRemainsInMemoryUntilACommittedLocationNeedsIt()
    {
        using TestRun test = new();
        GameState original = test.Session.State with { X = 42 };
        string name = test.Controller.StageAttempt("attempt.json", original);
        test.Controller.State.ActiveBeforeCheckpoint = name;
        string path = Path.Combine(test.RunDirectory, name);
        test.Controller.MaintainCapture();
        Assert.False(File.Exists(path));

        string manifest = test.Controller.SaveBundle("entry", test.Session.State);
        test.Controller.MaintainCapture();
        Assert.Equal(42, AutoPlayCheckpointStore.Load(path).X);

        test.Controller.ResumeFrom(manifest);
        string resumedDirectory = Path.GetDirectoryName(Assert.Single(Directory.GetFiles(test.Directory, "resume-start.run.json", SearchOption.AllDirectories)))!;
        Assert.Equal(42, AutoPlayCheckpointStore.Load(Path.Combine(resumedDirectory, test.Controller.State.ActiveBeforeCheckpoint!)).X);
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
