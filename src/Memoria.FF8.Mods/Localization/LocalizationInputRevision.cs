namespace Memoria.FF8.Mods.Localization;

/// <summary>A bounded metadata snapshot used again immediately before publication.</summary>
public sealed class LocalizationInputRevision
{
    private sealed record Stamp(long Length, long Modified, LocalizationSnapshotIdentity? Identity);
    private readonly IReadOnlyDictionary<string, Stamp> files;
    private readonly string[] roots;

    private LocalizationInputRevision(string[] roots, IReadOnlyDictionary<string, Stamp> files)
    {
        this.roots = roots;
        this.files = files;
    }

    public static LocalizationInputRevision Capture(IEnumerable<string> packageDirectories)
    {
        string[] roots = packageDirectories.Select(path => Path.Combine(path, "Localization")).ToArray();
        return new(roots, Read(roots));
    }

    public void Validate()
    {
        IReadOnlyDictionary<string, Stamp> current = Read(roots);
        if (current.Count != files.Count || files.Any(pair => !current.TryGetValue(pair.Key, out Stamp? stamp) || stamp != pair.Value))
        {
            throw new IOException("Localization inputs changed after preparation. Discard this generation and retry.");
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
