namespace Memoria.FF8;

internal static class DiagnosticJsonPublication
{
    private const int RequiredAttempts = 5;

    internal static bool Publish(string path, byte[] bytes, bool progress, Action<string, string, bool>? move = null, Action<TimeSpan>? delay = null)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + ".tmp";
        move ??= static (source, destination, overwrite) => File.Move(source, destination, overwrite);
        delay ??= Thread.Sleep;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    if (!progress)
                    {
                        stream.Flush(flushToDisk: true);
                    }
                }
                // A same-directory rename publishes a whole record without copying the
                // destination's metadata, as ReplaceFile does on Windows.
                move(temporary, full, progress);
                return true;
            }
            catch (IOException exception) when (IsTransientPublicationConflict(exception))
            {
                if (progress)
                {
                    // The next heartbeat replaces this temporary record with current facts.
                    // Never delay a presentation callback for an optional progress channel.
                    return false;
                }
                if (attempt >= RequiredAttempts)
                {
                    throw;
                }
                // Required reports are published after the measured executions complete.
                delay(TimeSpan.FromMilliseconds(10 * attempt));
            }
        }
    }

    private static bool IsTransientPublicationConflict(IOException exception)
    {
        int error = exception.HResult & 0xFFFF;
        return error is 32 or 33 or 1175 or 1176;
    }
}
