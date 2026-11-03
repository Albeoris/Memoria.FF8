using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Memoria.FF8;

/// <summary>The shared portable music worker owns synthesis and device timing, without owning game state.</summary>
internal sealed class MusicWorkerOutput : IDisposable
{
    private readonly Channel<PendingCommand> commands;
    private readonly CancellationTokenSource stopping = new();
    private readonly TimeSpan timeout;
    private readonly Task worker;
    private Process? process;
    private Exception? error;
    private int disposed;

    public MusicWorkerOutput(string directory) : this(CreateStartInfo(directory), TimeSpan.FromSeconds(10))
    {
    }

    internal MusicWorkerOutput(ProcessStartInfo start, TimeSpan timeout, int capacity = 128)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        this.timeout = timeout;
        commands = Channel.CreateBounded<PendingCommand>(new BoundedChannelOptions(capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        worker = Task.Run(() => Run(start));
    }

    public Exception? Error => Volatile.Read(ref error);

    public void Play(string path, int volume, int startPoint = 0) => Enqueue(new { Command = "Play", Path = path, Volume = volume, StartPoint = startPoint });
    public void Volume(int hundredthDecibels) => Enqueue(new { Command = "Volume", Volume = hundredthDecibels });
    public void Stop() => Enqueue(new { Command = "Stop" });
    public void Pause() => Enqueue(new { Command = "Pause" });
    public void Resume() => Enqueue(new { Command = "Resume" });

    // Diagnostic queries fence earlier queued commands. Gameplay presentation never waits for this property.
    public bool IsPlaying
    {
        get
        {
            TaskCompletionSource<bool> reply = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Enqueue(new { Command = "Playing" }, reply);
            return reply.Task.WaitAsync(timeout).GetAwaiter().GetResult();
        }
    }

    private static ProcessStartInfo CreateStartInfo(string directory)
    {
        string helper = Path.Combine(Memoria.FF8.Platform.ApplicationInstallation.DataDirectory, "audio-host", OperatingSystem.IsWindows() ? "Memoria.FF8.Music.exe" : "Memoria.FF8.Music");
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(directory);
        return start;
    }

    private void Enqueue(object command, TaskCompletionSource<bool>? reply = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (Error is Exception failure)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (!commands.Writer.TryWrite(new PendingCommand(JsonSerializer.Serialize(command), reply)))
        {
            InvalidOperationException queueFailure = new("Music output command queue is full or closed.");
            Fail(queueFailure);
            ExceptionDispatchInfo.Capture(Error ?? queueFailure).Throw();
        }
    }

    private async Task Run(ProcessStartInfo start)
    {
        Process? helper = null;
        PendingCommand? current = null;
        try
        {
            stopping.Token.ThrowIfCancellationRequested();
            helper = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Music output process.");
            Volatile.Write(ref process, helper);
            stopping.Token.ThrowIfCancellationRequested();
            await ReadResult(helper);
            await foreach (PendingCommand command in commands.Reader.ReadAllAsync(stopping.Token))
            {
                current = command;
                await helper.StandardInput.WriteLineAsync(command.Json.AsMemory(), stopping.Token).WaitAsync(timeout, stopping.Token);
                await helper.StandardInput.FlushAsync(stopping.Token).WaitAsync(timeout, stopping.Token);
                bool result = await ReadResult(helper);
                command.Reply?.TrySetResult(result);
                current = null;
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
        finally
        {
            commands.Writer.TryComplete();
            Exception failure = Error ?? new ObjectDisposedException(nameof(MusicWorkerOutput));
            current?.Reply?.TrySetException(failure);
            while (commands.Reader.TryRead(out PendingCommand? command))
            {
                command.Reply?.TrySetException(failure);
            }
            if (helper is not null)
            {
                StopProcess(helper);
                helper.Dispose();
            }
        }
    }

    private async Task<bool> ReadResult(Process helper)
    {
        string? response = await helper.StandardOutput.ReadLineAsync(stopping.Token).AsTask().WaitAsync(timeout, stopping.Token);
        if (response is null)
        {
            throw new IOException("Music output process closed its command channel.");
        }
        using JsonDocument document = JsonDocument.Parse(response);
        if (document.RootElement.TryGetProperty("Error", out JsonElement failure))
        {
            throw new InvalidOperationException($"Music output: {failure.GetString()}");
        }
        return document.RootElement.GetProperty("Result").GetBoolean();
    }

    private void Fail(Exception failure)
    {
        Interlocked.CompareExchange(ref error, failure, null);
        commands.Writer.TryComplete();
        stopping.Cancel();
    }

    private static void StopProcess(Process helper)
    {
        try
        {
            if (!helper.HasExited)
            {
                helper.Kill(entireProcessTree: true);
                helper.WaitForExit(2000);
            }
        }
        catch (InvalidOperationException)
        {
            // The worker may have completed disposal while the owner was cancelling it.
        }
        catch (System.ComponentModel.Win32Exception) when (helper.HasExited)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        commands.Writer.TryComplete();
        stopping.Cancel();
        if (Volatile.Read(ref process) is Process helper)
        {
            StopProcess(helper);
        }
        worker.Wait(TimeSpan.FromSeconds(2));
    }

    private sealed record PendingCommand(string Json, TaskCompletionSource<bool>? Reply);
}
