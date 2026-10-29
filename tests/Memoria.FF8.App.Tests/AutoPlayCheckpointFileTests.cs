using System.Diagnostics;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCheckpointFileTests
{
    [Fact]
    public async Task AReaderWithoutDeleteSharingDelaysReplacementUntilItReleasesTheFile()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows delete sharing governs replacement while a reader is open.");
        using CheckpointFiles files = new();
        File.WriteAllText(files.Target, "old");
        File.WriteAllText(files.Temporary, "new");
        using FileStream reader = new(files.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task commit = Task.Run(() =>
        {
            started.SetResult();
            AutoPlayController.CommitCheckpointFile(files.Temporary, files.Target);
        }, TestContext.Current.CancellationToken);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            await Task.Delay(30, TestContext.Current.CancellationToken);
            Assert.False(commit.IsCompleted);
            Assert.Equal("old", File.ReadAllText(files.Target));
            // A rename attempt can briefly own the source handle; its content is verified at the final target below.
            Assert.True(File.Exists(files.Temporary));
        }
        finally
        {
            reader.Dispose();
            await commit.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        Assert.Equal("new", File.ReadAllText(files.Target));
        Assert.False(File.Exists(files.Temporary));
    }

    [Fact]
    public void AContinuingSharingConflictFailsWithinTheBoundAndPreservesBothFiles()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows delete sharing governs replacement while a reader is open.");
        using CheckpointFiles files = new();
        File.WriteAllText(files.Target, "old");
        File.WriteAllText(files.Temporary, "new");
        using FileStream reader = new(files.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Stopwatch elapsed = Stopwatch.StartNew();

        Exception? error = Record.Exception(() => AutoPlayController.CommitCheckpointFile(files.Temporary, files.Target));

        elapsed.Stop();
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "A continuing sharing conflict must fail.");
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), $"The bounded replacement took {elapsed.Elapsed}.");
        Assert.Equal("old", File.ReadAllText(files.Target));
        Assert.Equal("new", File.ReadAllText(files.Temporary));
    }

    [Fact]
    public void AReadOnlyTargetFailsWithoutReplacingOrRemovingEitherFile()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows denies replacing a read-only target.");
        using CheckpointFiles files = new();
        File.WriteAllText(files.Target, "old");
        File.WriteAllText(files.Temporary, "new");
        File.SetAttributes(files.Target, File.GetAttributes(files.Target) | FileAttributes.ReadOnly);

        Exception? error = Record.Exception(() => AutoPlayController.CommitCheckpointFile(files.Temporary, files.Target));

        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "A read-only target must fail.");
        Assert.Equal("old", File.ReadAllText(files.Target));
        Assert.Equal("new", File.ReadAllText(files.Temporary));
        Assert.True((File.GetAttributes(files.Target) & FileAttributes.ReadOnly) != 0);
    }

    [Fact]
    public void ANewTargetReceivesTheCompleteTemporaryFile()
    {
        using CheckpointFiles files = new();
        File.WriteAllText(files.Temporary, "new");

        AutoPlayController.CommitCheckpointFile(files.Temporary, files.Target);

        Assert.Equal("new", File.ReadAllText(files.Target));
        Assert.False(File.Exists(files.Temporary));
    }

    private sealed class CheckpointFiles : IDisposable
    {
        private readonly string directory = Path.Combine(TestResources.ArtifactDirectory, "checkpoint-file-" + Guid.NewGuid().ToString("N"));

        public CheckpointFiles()
        {
            Directory.CreateDirectory(directory);
        }

        public string Target => Path.Combine(directory, "checkpoint.json");
        public string Temporary => Path.Combine(directory, "checkpoint.json.tmp");

        public void Dispose()
        {
            foreach (string path in new[] { Temporary, Target })
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            Directory.Delete(directory);
        }
    }
}
