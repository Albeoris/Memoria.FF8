using System.Diagnostics;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PrivateEvidencePathTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "private-evidence-path-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Ignored_repository_output_supports_store_and_pilot_admission()
    {
        string repository = CreateRepository();
        string output = Path.Combine(repository, "test-output", "evidence");

        Assert.Equal(output, PrivateEvidencePath.Validate(output));
        EvidenceStore store = new(output);
        Assert.Equal(output, store.Root);
        Assert.True(Directory.Exists(output));

        string build = Path.Combine(repository, "test-output", "build");
        string game = Path.Combine(repository, "test-output", "game");
        Directory.CreateDirectory(build);
        Directory.CreateDirectory(game);
        string executable = Path.Combine(build, "synthetic.exe");
        File.WriteAllText(executable, "synthetic validation input; never launched");
        PilotManifest manifest = new(1, executable, build, game, Path.Combine(repository, "test-output", "pilot"), new(1000, 1000, 1000, 1000, 1000, 10000, 1000), [new("blocked", null, "PLANNED_BLOCKED", ["Synthetic path admission only."])]);

        PilotRunner.ValidateManifest(manifest);
        Assert.False(Directory.Exists(manifest.OutputRoot));
    }

    [Fact]
    public void Ordinary_repository_output_is_rejected_before_store_creation()
    {
        string repository = CreateRepository();
        string output = Path.Combine(repository, "product", "evidence");

        Assert.Throws<ArgumentException>(() => PrivateEvidencePath.Validate(output));
        Assert.Throws<ArgumentException>(() => new EvidenceStore(output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Malformed_nested_worktree_marker_cannot_inherit_parent_ignore_permission()
    {
        string repository = CreateRepository();
        string nested = Path.Combine(repository, "test-output", "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, ".git"), "gitdir: missing-worktree-metadata");
        string output = Path.Combine(nested, "evidence");

        Assert.Throws<ArgumentException>(() => PrivateEvidencePath.Validate(output));
        Assert.Throws<ArgumentException>(() => new EvidenceStore(output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Nested_repository_must_also_ignore_the_output()
    {
        string repository = CreateRepository();
        string nested = Path.Combine(repository, "test-output", "nested");
        Directory.CreateDirectory(nested);
        Git(nested, "init", "--quiet");
        string output = Path.Combine(nested, "evidence");

        Assert.Throws<ArgumentException>(() => PrivateEvidencePath.Validate(output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Relative_output_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => PrivateEvidencePath.Validate(Path.Combine("test-output", "evidence")));
    }

    [Fact]
    public async Task Ignored_repository_channels_launch_and_reap_a_synthetic_owned_child()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows job-object test.");
        }

        string repository = CreateRepository();
        string channels = Path.Combine(repository, "test-output", "owned-child");
        string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        SupervisionBudgets budgets = new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10));
        SupervisionRequest request = new("ignored-output-smoke", executable, ["/d", "/s", "/c", "exit /b 0"], Path.GetDirectoryName(executable)!, Path.Combine(channels, "settings"), Path.Combine(channels, "output"), Path.Combine(channels, "control"), budgets);

        SupervisionOutcome outcome = await OwnedChildSupervisor.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("MISSING_CANDIDATE", outcome.ErrorCategory);
        Assert.Equal(0, outcome.ExitCode);
        Assert.NotNull(outcome.Process);
        Assert.True(outcome.ProcessExited);
        Assert.True(File.Exists(Path.Combine(request.OutputDirectory, "started.json")));
        Assert.True(File.Exists(outcome.ResultPath));
    }

    private string CreateRepository()
    {
        Directory.CreateDirectory(directory);
        Git(directory, "init", "--quiet");
        File.WriteAllText(Path.Combine(directory, ".gitignore"), "test-output/\n");
        return directory;
    }

    private static void Git(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch fixture Git.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Fixture Git exceeded its deadline.");
        }
        Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult() + output.GetAwaiter().GetResult());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(directory, recursive: true);
        }
    }
}
