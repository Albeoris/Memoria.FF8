namespace Memoria.FF8.Mods.Localization;

/// <summary>A bounded metadata snapshot used again immediately before publication.</summary>
public sealed class LocalizationInputRevision : IDisposable
{
    private sealed record Stamp(long Length, long Modified, LocalizationSnapshotIdentity? Identity);
    private readonly IReadOnlyDictionary<string, Stamp> files;
    private readonly string[] roots;
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly bool watching;
    private long changes;
    private long validatedChanges;
    private bool disposed;

    private LocalizationInputRevision(string[] roots, bool watch)
    {
        this.roots = roots;
        watching = watch;
        try
        {
            if (watch)
            {
                foreach (string root in roots)
                {
                    FileSystemWatcher watcher = new(root)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        InternalBufferSize = 64 * 1024
                    };
                    watchers.Add(watcher);
                    watcher.Changed += (_, args) => Changed(args.FullPath);
                    watcher.Created += (_, args) => Changed(args.FullPath);
                    watcher.Deleted += (_, args) => Changed(args.FullPath);
                    watcher.Renamed += (_, args) =>
                    {
                        Changed(args.OldFullPath);
                        Changed(args.FullPath);
                    };
                    watcher.Error += (_, _) => Interlocked.Increment(ref changes);
                    watcher.EnableRaisingEvents = true;
                }
            }
            long before = Volatile.Read(ref changes);
            files = Read(roots);
            validatedChanges = before;
            ValidatePublicationVersion();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public static LocalizationInputRevision Capture(IEnumerable<string> packageDirectories, bool watchForPublication = false)
    {
        string[] roots = packageDirectories.Select(path => Path.Combine(path, "Localization")).ToArray();
        return new(roots, watchForPublication);
    }

    public void Validate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        long before = Volatile.Read(ref changes);
        IReadOnlyDictionary<string, Stamp> current = Read(roots);
        if (before != Volatile.Read(ref changes) || current.Count != files.Count || files.Any(pair => !current.TryGetValue(pair.Key, out Stamp? stamp) || stamp != pair.Value))
        {
            throw new IOException("Localization inputs changed after preparation. Discard this generation and retry.");
        }
        Volatile.Write(ref validatedChanges, before);
    }

    /// <summary>Checks the background-validated generation without opening files on the publication thread.</summary>
    public void ValidatePublication()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!watching)
        {
            throw new InvalidOperationException("Publication requires a watched localization input revision.");
        }
        ValidatePublicationVersion();
    }

    private void ValidatePublicationVersion()
    {
        if (Volatile.Read(ref validatedChanges) != Volatile.Read(ref changes))
        {
            throw new IOException("Localization inputs changed after preparation. Discard this generation and retry.");
        }
    }

    private void Changed(string path)
    {
        string name = Path.GetFileName(path);
        if (!name.StartsWith('~') && !name.StartsWith('.') && (!Path.HasExtension(path) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
        {
            Interlocked.Increment(ref changes);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        foreach (FileSystemWatcher watcher in watchers)
        {
            watcher.Dispose();
        }
    }

    private static IReadOnlyDictionary<string, Stamp> Read(IEnumerable<string> roots)
    {
        Dictionary<string, Stamp> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(path => !Path.GetFileName(path).StartsWith('~') && !Path.GetFileName(path).StartsWith('.') && (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))))
            {
                FileInfo file = new(path);
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                result.Add(Path.GetFullPath(path), new(file.Length, file.LastWriteTimeUtc.Ticks, LocalizationSnapshotIdentity.Capture(stream)));
            }
        }
        return result;
    }
}
