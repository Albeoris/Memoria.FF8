using System.Diagnostics;

namespace Memoria.FF8.LinuxTesting;

public static class ProcessRunner
{
    public static RunningProcess Start(string executable, IEnumerable<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment, string logPath)
    {
        ProcessStartInfo info = new(ResolveExecutable(executable, workingDirectory, environment))
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        foreach ((string key, string value) in environment)
        {
            info.Environment[key] = value;
        }
        return new RunningProcess(info, logPath);
    }

    private static string ResolveExecutable(string executable, string workingDirectory, IReadOnlyDictionary<string, string> environment)
    {
        if (Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar) || !environment.TryGetValue("PATH", out string? searchPath))
        {
            return executable;
        }
        string[] extensions = OperatingSystem.IsWindows() && !Path.HasExtension(executable) ? ["", ".exe", ".com"] : [""];
        foreach (string directory in searchPath.Split(Path.PathSeparator))
        {
            string baseDirectory = Path.GetFullPath(directory.Length == 0 ? "." : directory, workingDirectory);
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(baseDirectory, executable + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        throw new System.ComponentModel.Win32Exception(2, "Executable was not found in the configured PATH: " + executable);
    }

    public static async Task<int?> CaptureAsync(string executable, IEnumerable<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment, string logPath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using RunningProcess process = Start(executable, arguments, workingDirectory, environment, logPath);
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            try
            {
                return await process.WaitAsync(deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            await File.WriteAllTextAsync(logPath, error.Message, cancellationToken);
            return null;
        }
    }
}

public sealed class RunningProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly StreamWriter log;
    private readonly CancellationTokenSource outputCancellation = new();
    private readonly SemaphoreSlim logLock = new(1);
    private readonly Task output;
    private int ownedGroup;

    internal RunningProcess(ProcessStartInfo info, string logPath)
    {
        log = new StreamWriter(logPath, false) { AutoFlush = true };
        string? ready = null;
        if (OperatingSystem.IsLinux())
        {
            ready = logPath + ".group-" + Guid.NewGuid().ToString("N");
            info = OwnedCommand.Wrap(info, ready);
        }
        process = new Process { StartInfo = info };
        try
        {
            process.Start();
            output = Task.WhenAll(CopyAsync(process.StandardOutput), CopyAsync(process.StandardError));
            if (ready is not null)
            {
                Stopwatch startup = Stopwatch.StartNew();
                while (!File.Exists(ready) && !process.HasExited && startup.Elapsed < TimeSpan.FromSeconds(10))
                {
                    Thread.Sleep(10);
                }
                if (!File.Exists(ready) || File.ReadAllText(ready) != process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    throw new IOException("The target command did not establish its owned Linux session.");
                }
                ownedGroup = process.Id;
                File.Delete(ready);
            }
        }
        catch
        {
            process.Dispose();
            log.Dispose();
            outputCancellation.Dispose();
            logLock.Dispose();
            throw;
        }
    }

    public int Id => process.Id;
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;

    public async Task<int> WaitAsync(CancellationToken cancellationToken = default)
    {
        await process.WaitForExitAsync(cancellationToken);
        OwnedCommand.TerminateGroup(Interlocked.Exchange(ref ownedGroup, 0));
        await output.WaitAsync(cancellationToken);
        return process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            OwnedCommand.TerminateGroup(Interlocked.Exchange(ref ownedGroup, 0));
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
            }
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cleanup.Token);
        }
        finally
        {
            outputCancellation.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await output;
            }
            catch (OperationCanceledException) when (outputCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                process.Dispose();
                await log.DisposeAsync();
                outputCancellation.Dispose();
                logLock.Dispose();
            }
        }
    }

    private async Task CopyAsync(StreamReader source)
    {
        while (await source.ReadLineAsync(outputCancellation.Token) is string line)
        {
            await logLock.WaitAsync(outputCancellation.Token);
            try
            {
                await log.WriteLineAsync(line);
            }
            finally
            {
                logLock.Release();
            }
        }
    }
}
