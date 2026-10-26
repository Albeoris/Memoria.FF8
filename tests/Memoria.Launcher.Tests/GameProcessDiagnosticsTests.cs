using System.Diagnostics;
using System.Text;

namespace Memoria.Launcher.Tests;

public sealed class GameProcessDiagnosticsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.Diagnostics.Tests", Guid.NewGuid().ToString("N"));

    public GameProcessDiagnosticsTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public async Task Large_console_streams_are_drained_and_final_unterminated_error_is_persisted()
    {
        string script = Path.Combine(directory, "crash.ps1");
        await File.WriteAllTextAsync(script, """
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            for ($i = 0; $i -lt 128; $i++) {
                [Console]::Out.Write(('o' * 4096))
                [Console]::Error.Write(('e' * 4096))
            }
            [Console]::Error.Write("System.InvalidOperationException: battle failed; inner exception: bad operand — ошибка боя")
            exit -1
            """, new UTF8Encoding(true), TestContext.Current.CancellationToken);
        using StringWriter console = new();
        using GameProcessDiagnostics diagnostics = new(directory, console: console);
        using Process process = Start(script);
        Task capture = diagnostics.CaptureAsync(process);
        try
        {
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await capture.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            string message = diagnostics.Complete(process.ExitCode);
            Assert.Equal(-1, process.ExitCode);
            Assert.Contains("0xFFFFFFFF", message);
            Assert.Contains("System.InvalidOperationException: battle failed; inner exception: bad operand — ошибка боя", message);
            Assert.Contains(diagnostics.LogPath!, message);
            string saved = await File.ReadAllTextAsync(diagnostics.LogPath!, TestContext.Current.CancellationToken);
            Assert.True(saved.Length > 1024 * 1024);
            Assert.Contains("bad operand", saved);
            Assert.Contains("ошибка боя", saved);
            Assert.Contains("code -1", saved);
            string visible = console.ToString();
            Assert.Equal(2 * 128 * 4096 + "System.InvalidOperationException: battle failed; inner exception: bad operand — ошибка боя".Length, visible.Length);
            Assert.Contains("System.InvalidOperationException: battle failed; inner exception: bad operand — ошибка боя", visible);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None);
            await capture;
        }
    }

    [Fact]
    public async Task Packaged_engine_exception_preserves_its_stack_and_unicode_path()
    {
        string? executable = Environment.GetEnvironmentVariable("MEMORIA_FF8_APP_PATH");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(executable) && File.Exists(executable), "Set MEMORIA_FF8_APP_PATH to check packaged engine error reporting.");
        string package = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(executable!)))!;
        string missingGame = Path.Combine(directory, "Несуществующая игра");
        ProcessStartInfo start = GameInstallationSelection.CreateStartInfo(package, missingGame);
        start.FileName = Path.GetFullPath(executable!);
        using GameProcessDiagnostics diagnostics = new(directory);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the packaged engine.");
        Task capture = diagnostics.CaptureAsync(process);
        try
        {
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await capture.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            string message = diagnostics.Complete(process.ExitCode);
            Assert.Equal(1, process.ExitCode);
            Assert.Contains("System.IO.DirectoryNotFoundException", message);
            Assert.Contains(missingGame, message);
            Assert.Contains("Memoria.FF8.Program.Main", message);
            Assert.Contains(missingGame, await File.ReadAllTextAsync(diagnostics.LogPath!, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None);
            await capture;
        }
    }

    [Fact]
    public void Silent_abnormal_exit_is_explicit_and_a_new_launch_has_its_own_log()
    {
        using GameProcessDiagnostics first = new(directory);
        using GameProcessDiagnostics second = new(directory);
        string message = second.Complete(-1);
        Assert.NotEqual(first.LogPath, second.LogPath);
        Assert.Contains("without reporting an error", message);
        Assert.Contains("code -1 (0xFFFFFFFF)", File.ReadAllText(second.LogPath!));
        Assert.DoesNotContain(first.LogPath!, message);
        Assert.DoesNotContain("without reporting an error", second.Complete(0));
    }

    [Fact]
    public void Unwritable_primary_location_falls_back_to_a_separate_directory()
    {
        string blocked = Path.Combine(directory, "blocked");
        File.WriteAllText(blocked, "not a directory");
        string fallback = Path.Combine(directory, "fallback");
        using GameProcessDiagnostics diagnostics = new(blocked, fallback);
        Assert.Equal(fallback, Path.GetDirectoryName(diagnostics.LogPath));
        Assert.DoesNotContain("Could not save", diagnostics.Complete(-1));
    }

    [Fact]
    public async Task Unavailable_storage_keeps_the_error_in_memory()
    {
        string blocked = Path.Combine(directory, "blocked");
        File.WriteAllText(blocked, "not a directory");
        string script = Path.Combine(directory, "error.ps1");
        await File.WriteAllTextAsync(script, "[Console]::Error.Write('unsaved battle error'); exit 1", TestContext.Current.CancellationToken);
        using GameProcessDiagnostics diagnostics = new(blocked, blocked);
        using Process process = Start(script);
        Task capture = diagnostics.CaptureAsync(process);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await capture;
        string message = diagnostics.Complete(process.ExitCode);
        Assert.Null(diagnostics.LogPath);
        Assert.Contains("unsaved battle error", message);
        Assert.Contains("No game log could be saved", message);
        Assert.Contains("Could not save", message);
    }

    private static Process Start(string script)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows PowerShell process diagnostic probe.");
        ProcessStartInfo start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start test child process.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_console_does_not_interrupt_full_log_capture(bool disposed)
    {
        string script = Path.Combine(directory, "console-failure.ps1");
        await File.WriteAllTextAsync(script, "[Console]::Error.Write(('x' * 9000) + [Environment]::NewLine + 'System.Exception: failure' + [Environment]::NewLine + '   at Example.Run()'); exit 1", TestContext.Current.CancellationToken);
        using FailingConsole console = new(disposed);
        using GameProcessDiagnostics diagnostics = new(directory, console: console);
        using Process process = Start(script);
        Task capture = diagnostics.CaptureAsync(process);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await capture.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        string completion = diagnostics.Complete(process.ExitCode);
        string saved = await File.ReadAllTextAsync(diagnostics.LogPath!, TestContext.Current.CancellationToken);
        Assert.True(saved.Count(character => character == 'x') >= 9000);
        Assert.Contains("at Example.Run()", saved);
        Assert.Contains("at Example.Run()", completion);
    }

    private sealed class FailingConsole(bool disposed) : StringWriter
    {
        public override void Write(string? value)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(FailingConsole));
            }
            throw new IOException("Console unavailable");
        }
    }

    [Fact]
    public void Console_disposal_tolerates_a_failed_final_flush_and_closes_the_stream()
    {
        using FailingConsoleStream stream = new();
        GameConsole.ConsoleWriter console = new(stream);
        console.Write("pending error output");

        console.Dispose();

        Assert.False(stream.CanWrite);
    }

    private sealed class FailingConsoleStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new IOException("Console unavailable");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new IOException("Console unavailable");
        }
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}
