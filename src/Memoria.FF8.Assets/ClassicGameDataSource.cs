using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Owns Classic storage. Logical keys are archive family / nested archive / resource.</summary>
public sealed partial class ClassicGameDataSource : IResourceSource, IGameArchiveSource, IGameFileSource
{
    private readonly string gameDirectory;
    private readonly Dictionary<string, IFlArchive> archives = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResourceContent?> catalog = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public ClassicGameDataSource(string gameDirectory)
    {
        this.gameDirectory = Path.GetFullPath(gameDirectory);
    }

    public ResourceSourceDescriptor Descriptor { get; } = new("classic", GameDataEdition.Classic, ["classic-world-texl", "classic-world-road-region", "classic-world-water-region", "classic-field-tile", "classic-model", "classic-battle"]);

    public IFlArchive OpenArchive(string family)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string name = new ResourceKey(family).Path;
        if (name.Contains('/'))
        {
            throw new ArgumentException("An archive family must be a single logical name.", nameof(family));
        }
        return FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", name + ".fs"), FlArchiveRepresentation.Files);
    }

    public string ResolveFile(string relativeDataPath)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return Path.Combine(gameDirectory, "Data", new ResourceKey(relativeDataPath).Path.Replace('/', Path.DirectorySeparatorChar));
    }

    public ResourceContent? Find(ResourceKey key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (catalog.TryGetValue(key.Path, out ResourceContent? cached))
        {
            return cached;
        }
        if (key.Path.StartsWith("files/", StringComparison.Ordinal))
        {
            string path = ResolveFile(key.Path[6..]);
            ResourceContent? file = File.Exists(path) ? new ResourceContent(key, Path.GetExtension(path).TrimStart('.'), 1, $"{Descriptor.Id}:{key}", () => File.OpenRead(path)) : null;
            catalog.Add(key.Path, file);
            return file;
        }
        if (FindNamedTable(key) is { } table)
        {
            catalog.Add(key.Path, table);
            return table;
        }
        string[] parts = key.Path.Split('/');
        if (parts.Length < 2)
        {
            return null;
        }
        string family = parts[0];
        string archivePath = Path.Combine(gameDirectory, "Data", "lang-en", family + ".fs");
        if (!File.Exists(Path.ChangeExtension(archivePath, ".fl")))
        {
            return null;
        }
        if (!archives.TryGetValue(family, out IFlArchive? archive))
        {
            archive = FlArchive.OpenForRead(archivePath, FlArchiveRepresentation.Files);
            archives.Add(family, archive);
        }
        for (int level = 1; level < parts.Length - 1; level++)
        {
            string nestedKey = string.Join('/', parts.Take(level + 1));
            if (!archives.TryGetValue(nestedKey, out IFlArchive? nested))
            {
                if (!HasEntry(archive, parts[level] + ".fl") || !HasEntry(archive, parts[level] + ".fi") || !HasEntry(archive, parts[level] + ".fs"))
                {
                    catalog[key.Path] = null;
                    return null;
                }
                byte[] fl = ReadEntry(archive, parts[level] + ".fl");
                byte[] fi = ReadEntry(archive, parts[level] + ".fi");
                byte[] fs = ReadEntry(archive, parts[level] + ".fs");
                nested = FlArchive.Open(new MemoryStream(fl, false), new MemoryStream(fi, false), new MemoryStream(fs, false), false, FlArchiveRepresentation.Files);
                archives.Add(nestedKey, nested);
            }
            archive = nested;
        }
        string basename = parts[^1];
        IFlArchiveEntry[] entries = archive.Entries.Where(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Length > 1)
        {
            throw new InvalidDataException($"Ambiguous Classic resource {key}.");
        }
        ResourceContent? result = entries.Length == 0 ? null : new ResourceContent(key, Path.GetExtension(basename).TrimStart('.'), 1, $"classic:{key}", entries[0].OpenForRead);
        catalog.Add(key.Path, result);
        return result;
    }

    private static byte[] ReadEntry(IFlArchive archive, string basename)
    {
        IFlArchiveEntry entry = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase));
        using Stream stream = entry.OpenForRead();
        using MemoryStream result = new();
        stream.CopyTo(result);
        return result.ToArray();
    }

    private static bool HasEntry(IFlArchive archive, string basename) => archive.Entries.Any(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        foreach (IFlArchive archive in archives.Values.Reverse())
        {
            archive.Dispose();
        }
        archives.Clear();
        catalog.Clear();
    }
}
