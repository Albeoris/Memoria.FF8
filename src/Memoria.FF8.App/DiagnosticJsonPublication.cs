namespace Memoria.FF8;

internal static class DiagnosticJsonPublication
{
    private const int RequiredAttempts = 5;

    internal static bool Publish(string path, byte[] bytes, bool progress, Action<string, string, bool>? move = null, Action<TimeSpan>? delay = null)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + ".tmp";
        move ??= PublishFile;
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

    private static void PublishFile(string temporary, string destination, bool overwrite)
    {
        if (overwrite && File.Exists(destination))
        {
            // ReplaceFile preserves atomic publication while delete-sharing readers
            // retain the old record. Move(overwrite: true) can reject those readers.
            File.Replace(temporary, destination, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporary, destination);
        }
    }

    private static bool IsTransientPublicationConflict(IOException exception)
    {
        int error = exception.HResult & 0xFFFF;
        return error is 32 or 33 or 1175 or 1176;
    }
}
