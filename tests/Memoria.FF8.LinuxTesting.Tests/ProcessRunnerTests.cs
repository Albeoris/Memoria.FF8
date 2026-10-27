using Memoria.FF8.LinuxTesting;

namespace Memoria.FF8.LinuxTesting.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task CapturePreservesArgumentsOutputAndExitCode()
    {
        string directory = CreateDirectory();
        try
        {
            string log = Path.Combine(directory, "process.log");
            int? result = await CaptureHelperAsync(directory, log, 0, 17, "argument with spaces; $(literal)", TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.Equal(17, result);
            string text = await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken);
            Assert.Contains("argument with spaces; $(literal)", text);
            Assert.Contains("stderr", text);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CaptureTimeoutTerminatesProcessAndClosesLog()
    {
        string directory = CreateDirectory();
        try
        {
            string log = Path.Combine(directory, "process.log");
            int? result = await CaptureHelperAsync(directory, log, 60000, 0, "started", TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            Assert.Null(result);
            using FileStream exclusive = new(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CaptureCancellationPropagatesAndDisposesActiveProcess()
    {
        string directory = CreateDirectory();
        try
        {
            string log = Path.Combine(directory, "process.log");
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CaptureHelperAsync(directory, log, 60000, 0, "started", TimeSpan.FromSeconds(30), cancellation.Token));
            using FileStream exclusive = new(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task CancellationBeforeStartDoesNotCreateLog()
    {
        string directory = CreateDirectory();
        try
        {
            string log = Path.Combine(directory, "process.log");
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CaptureHelperAsync(directory, log, 0, 0, "unused", TimeSpan.FromSeconds(30), cancellation.Token));
            Assert.False(File.Exists(log));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ExplicitPathResolvesExecutableOutsideParentSearchPath()
    {
        string directory = CreateDirectory();
        try
        {
            string executable = Path.Combine(directory, OperatingSystem.IsWindows() ? "configured-command.exe" : "configured-command");
            string[] arguments;
            if (OperatingSystem.IsWindows())
            {
                File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), executable);
                arguments = ["/d", "/c", "exit", "29"];
            }
            else
            {
                await File.WriteAllTextAsync(executable, "#!/bin/sh\nexit 29\n", TestContext.Current.CancellationToken);
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                arguments = [];
            }
            int? result = await ProcessRunner.CaptureAsync("configured-command", arguments, directory, new Dictionary<string, string> { ["PATH"] = directory }, Path.Combine(directory, "process.log"), TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.Equal(29, result);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-process-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static Task<int?> CaptureHelperAsync(string directory, string log, int milliseconds, int exitCode, string echo, TimeSpan timeout, CancellationToken token = default)
    {
        string script = Path.Combine(directory, OperatingSystem.IsWindows() ? "helper.ps1" : "helper.sh");
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(script, "param([int]$Milliseconds, [int]$ExitCode, [string]$Echo)\n[Console]::WriteLine($Echo)\n[Console]::Error.WriteLine('stderr')\nStart-Sleep -Milliseconds $Milliseconds\nexit $ExitCode\n");
            return ProcessRunner.CaptureAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-File", script, milliseconds.ToString(), exitCode.ToString(), echo], directory, new Dictionary<string, string>(), log, timeout, token);
        }
        File.WriteAllText(script, "printf '%s\\n' \"$3\"\nprintf 'stderr\\n' >&2\nsleep \"$1\"\nexit \"$2\"\n");
        return ProcessRunner.CaptureAsync("sh", [script, (milliseconds / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture), exitCode.ToString(), echo], directory, new Dictionary<string, string>(), log, timeout, token);
    }
}
