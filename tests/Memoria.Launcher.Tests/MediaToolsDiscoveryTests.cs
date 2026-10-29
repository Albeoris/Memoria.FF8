using Memoria.FF8;

namespace Memoria.Launcher.Tests;

[Collection("Media tools environment")]
public sealed class MediaToolsDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.MediaDiscovery.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExplicitDirectoryWinsOverEnvironmentWithoutProbingOrCreation()
    {
        const string variable = "MEMORIA_FF8_MEDIA_TOOLS";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, Path.Combine(root, "environment"));
            string selected = Path.Combine(root, "selected");
            Assert.Equal(selected, await MediaToolsInstallation.ResolveDirectoryAsync(selected, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(Path.Combine(root, "environment"), await MediaToolsInstallation.ResolveDirectoryAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task ManagedPairRemainsPreferredAndDoesNotInspectPath()
    {
        string managed = Path.Combine(root, "managed");
        MediaToolsDiscoveryOptions options = new(managed, null, Path.Combine(root, "system"));
        List<string> checkedPairs = [];
        string selected = await MediaToolsInstallation.ResolveDirectoryAsync(options, directory =>
        {
            checkedPairs.Add(directory);
            return directory == managed;
        }, (directory, _) => Task.FromResult(true), TestContext.Current.CancellationToken);
        Assert.Equal(managed, selected);
        Assert.Equal([managed], checkedPairs);
    }

    [Fact]
    public async Task InvalidManagedPairRemainsTheRepairDestination()
    {
        string managed = Path.Combine(root, "managed");
        string selected = await MediaToolsInstallation.ResolveDirectoryAsync(new(managed, null, Path.Combine(root, "system")), _ => true, (_, _) => Task.FromResult(false), TestContext.Current.CancellationToken);
        Assert.Equal(managed, selected);
    }

    [Fact]
    public async Task InvalidManagedPairCanUseAnApprovedSystemPairWithoutRepair()
    {
        string managed = Path.Combine(root, "managed");
        string system = Path.Combine(root, "system");
        string selected = await MediaToolsInstallation.ResolveDirectoryAsync(new(managed, null, system), _ => true, (directory, _) => Task.FromResult(directory == system), TestContext.Current.CancellationToken);
        Assert.Equal(system, selected);
        Assert.False(Directory.Exists(managed));
    }

    [Fact]
    public async Task EmptyRelativeAndUncPathEntriesAreNeverInspected()
    {
        string managed = Path.Combine(root, "managed");
        string system = Path.Combine(root, "system");
        string path = string.Join(Path.PathSeparator, "", ".", "relative", @"\\server\share", "//server/share", system);
        List<string> inspected = [];
        string selected = await MediaToolsInstallation.ResolveDirectoryAsync(new(managed, null, path), directory =>
        {
            inspected.Add(directory);
            return directory == system;
        }, (_, _) => Task.FromResult(true), TestContext.Current.CancellationToken);
        Assert.Equal(system, selected);
        Assert.Equal([managed, system], inspected);
    }

    [Fact]
    public async Task MissingInvalidAndGplSystemPairsAreSkippedForALgplPair()
    {
        string managed = Path.Combine(root, "managed");
        string missing = Path.Combine(root, "missing-probe");
        string wrongArchitecture = Path.Combine(root, "wrong-architecture");
        string gpl = Path.Combine(root, "gpl");
        string lgpl = Path.Combine(root, "lgpl");
        string path = string.Join(Path.PathSeparator, missing, wrongArchitecture, gpl, lgpl);
        List<string> executed = [];
        Task<bool> Validate(string directory, CancellationToken token) => MediaToolsInstallation.ValidateToolsAsync(directory, _ => directory != wrongArchitecture, (executable, _) =>
        {
            executed.Add(executable);
            string tool = Path.GetFileNameWithoutExtension(executable);
            return Task.FromResult(new MediaToolProbeResult(0, tool + " version 7.1\nconfiguration: --disable-gpl --disable-nonfree" + (directory == gpl ? " --enable-gpl" : "")));
        }, cancellationToken: token);
        string selected = await MediaToolsInstallation.ResolveDirectoryAsync(new(managed, null, path), directory => directory != managed && directory != missing, Validate, TestContext.Current.CancellationToken);
        Assert.Equal(lgpl, selected);
        Assert.DoesNotContain(executed, executable => executable.StartsWith(wrongArchitecture, StringComparison.Ordinal));
        Assert.Equal(3, executed.Count);
    }

    [Fact]
    public async Task NoUsableSystemPairReturnsManagedDestinationWithoutCreatingIt()
    {
        string managed = Path.Combine(root, "managed");
        string system = Path.Combine(root, "system");
        string selected = await MediaToolsInstallation.ResolveDirectoryAsync(new(managed, null, system), directory => directory == system, (_, _) => Task.FromResult(false), TestContext.Current.CancellationToken);
        Assert.Equal(managed, selected);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task DiscoveryCancellationFlowsThroughTheVersionProbe()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string managed = Path.Combine(root, "managed");
        string system = Path.Combine(root, "system");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MediaToolsInstallation.ResolveDirectoryAsync(new(managed, null, system), directory => directory == system, (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }, cancellation.Token));
    }

    [Theory]
    [InlineData("--enable-gpl")]
    [InlineData("--enable-nonfree")]
    [InlineData("--enable-gpl=yes")]
    [InlineData("'--enable-gpl'")]
    public async Task ActualBuildConfigurationRejectsGplAndNonfree(string flag)
    {
        List<string> diagnostics = [];
        bool ready = await MediaToolsInstallation.ValidateToolsAsync(root, _ => true, (executable, _) => Task.FromResult(new MediaToolProbeResult(0, Path.GetFileNameWithoutExtension(executable) + " version 7.1\nconfiguration: --enable-version3 " + flag)), diagnostics.Add, TestContext.Current.CancellationToken);
        Assert.False(ready);
        Assert.Contains("requires an LGPL build", Assert.Single(diagnostics));
    }

    [Fact]
    public async Task BothNativeToolsMustReportVerifiableLgplConfiguration()
    {
        List<string> tools = [];
        bool ready = await MediaToolsInstallation.ValidateToolsAsync(root, _ => true, (executable, _) =>
        {
            tools.Add(Path.GetFileNameWithoutExtension(executable));
            return Task.FromResult(new MediaToolProbeResult(0, tools[^1] + " version 7.1", "configuration: --enable-version3 --disable-gpl --disable-nonfree"));
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(ready);
        Assert.Equal(["ffmpeg", "ffprobe"], tools);
    }

    [Fact]
    public async Task UnreportedConfigurationAndNonNativeImagesFailBeforeUse()
    {
        List<string> diagnostics = [];
        Assert.False(await MediaToolsInstallation.ValidateToolsAsync(root, _ => true, (executable, _) => Task.FromResult(new MediaToolProbeResult(0, "ffmpeg version 7.1")), diagnostics.Add, TestContext.Current.CancellationToken));
        Assert.Contains("cannot be verified", Assert.Single(diagnostics));
        Assert.False(await MediaToolsInstallation.ValidateToolsAsync(root, _ => false, (_, _) => throw new InvalidOperationException("Wrong-ABI tools must never run."), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FailedSystemSelectionCannotBecomeTheInstallDestination()
    {
        string system = Path.Combine(root, "system");
        string managed = Path.Combine(root, "managed");
        Directory.CreateDirectory(system);
        File.WriteAllText(Path.Combine(system, "keep.txt"), "system file");
        const string variable = "MEMORIA_FF8_FFMPEG_RELEASE_URL";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "not-a-uri");
            List<string> reports = [];
            string selected = MediaToolsInstallation.PrepareForStartup(system, _ => throw new InvalidOperationException("No network setup is authorized."), reports.Add, managed);
            Assert.Equal(managed, selected);
            Assert.True(Directory.Exists(managed));
            Assert.Equal(["keep.txt"], Directory.GetFiles(system).Select(Path.GetFileName));
            Assert.Contains(managed, string.Join(Environment.NewLine, reports));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task InvalidPathDirectoryCannotReceiveAnAutomaticInstallation()
    {
        const string variable = "PATH";
        string? previous = Environment.GetEnvironmentVariable(variable);
        string system = Path.Combine(root, "system");
        try
        {
            Environment.SetEnvironmentVariable(variable, system);
            using System.Net.Http.HttpClient client = new();
            MediaToolsInstallation installation = new(client, new FfmpegDownloadOptions(new Uri("https://example.invalid/"), "ffmpeg-test-win64-lgpl.zip"), validate: _ => Task.FromResult(false));
            List<string> reports = [];
            Assert.False(await installation.EnsureAsync(system, _ => throw new InvalidOperationException("System directories must never receive an installer prompt."), reports.Add, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Contains("cannot replace files", Assert.Single(reports));
            Assert.False(Directory.Exists(system));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task NativeSystemSymlinkPairIsReadWithoutChangingItsTargets()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix system symlink discovery; Windows symlinks require additional machine privileges.");
        Directory.CreateDirectory(root);
        string target = Environment.ProcessPath!;
        DateTime written = File.GetLastWriteTimeUtc(target);
        foreach (string tool in new[] { "ffmpeg", "ffprobe" })
        {
            File.CreateSymbolicLink(MediaToolsInstallation.ResolveExecutable(root, tool), target);
        }
        bool ready = await MediaToolsInstallation.ValidateToolsAsync(root, MediaToolsInstallation.IsHostExecutable, (executable, _) => Task.FromResult(new MediaToolProbeResult(0, Path.GetFileNameWithoutExtension(executable) + " version 7.1\nconfiguration: --disable-gpl --disable-nonfree")), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(ready);
        Assert.Equal(written, File.GetLastWriteTimeUtc(target));
        Assert.Equal(target, new FileInfo(MediaToolsInstallation.ResolveExecutable(root, "ffmpeg")).LinkTarget);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SelectedSystemDirectorySurvivesGameProcessArguments()
    {
        string system = Path.Combine(root, "system tools");
        var start = GameInstallationSelection.CreateStartInfo(root, root, mediaToolsDirectory: system);
        Assert.Equal(system, start.ArgumentList[5]);
    }
}
