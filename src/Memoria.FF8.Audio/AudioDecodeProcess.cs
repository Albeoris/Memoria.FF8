using System.Diagnostics;

namespace Memoria.FF8.Audio;

public static class AudioDecodeProcess
{
    public static async Task<byte[]> RunAsync(ProcessStartInfo start, byte[]? input, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        TimeSpan deadline = timeout ?? TimeSpan.FromMinutes(2);
        if (deadline <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        cancellationToken.ThrowIfCancellationRequested();
        using var expired = new CancellationTokenSource(deadline);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expired.Token);
        CancellationToken token = linked.Token;
        using Process process = Process.Start(start) ?? throw new IOException("Could not start the audio decoder.");
        using var output = new MemoryStream();
        Task<string> errors = process.StandardError.ReadToEndAsync(token);
        Task read = process.StandardOutput.BaseStream.CopyToAsync(output, token);
        Task write = WriteInputAsync();
        Task transfers = Task.WhenAll(errors, read, write);
        try
        {
            await Task.WhenAll(transfers, process.WaitForExitAsync()).WaitAsync(token).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"Audio decode failed: {await errors.ConfigureAwait(false)}");
            }
            if (output.Length == 0)
            {
                throw new InvalidDataException("Audio decode produced no PCM samples.");
            }
            return output.ToArray();
        }
        catch (OperationCanceledException) when (expired.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Audio decoding exceeded {deadline.TotalSeconds:g} seconds. Check the media file and configured FFmpeg installation.");
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The decoder can finish between the exit check and cancellation.
                }
            }
            try
            {
                await Task.WhenAll(transfers, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or IOException or ObjectDisposedException)
            {
                // Cleanup observes cancelled pipe tasks after the child has been terminated.
            }
        }

        async Task WriteInputAsync()
        {
            if (input is not null)
            {
                try
                {
                    await process.StandardInput.BaseStream.WriteAsync(input, token).ConfigureAwait(false);
                    await process.StandardInput.BaseStream.FlushAsync(token).ConfigureAwait(false);
                }
                finally
                {
                    process.StandardInput.Close();
                }
            }
        }
    }
}
