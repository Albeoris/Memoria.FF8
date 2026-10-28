using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

/// <summary>Owns release-specific storage, localization and replacement selection.</summary>
public sealed partial class RemasteredGameDataSource : IResourceSource, IGameArchiveSource, IGameFileSource, IFieldModelSource, IBattleModelSource, ITextureReplacementResolver
{
    private readonly string gameDirectory;
    private readonly string language;
    private readonly ZzzArchive main;
    private readonly ZzzArchive other;
    private readonly Dictionary<string, IFlArchive> archives = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ResourceKey, ResourceContent?> catalog = [];
    private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly string cacheDirectory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Resources", Guid.NewGuid().ToString("N"));
    private bool disposed;

    public RemasteredGameDataSource(string gameDirectory, string language = "en")
    {
        if (language is not ("en" or "fr" or "de" or "es" or "it" or "ja"))
        {
            throw new ArgumentException("Unsupported resource language.", nameof(language));
        }
        this.gameDirectory = Path.GetFullPath(gameDirectory);
        this.language = language == "ja" ? "jp" : language;
        main = new ZzzArchive(Path.Combine(this.gameDirectory, "main.zzz"));
        try
        {
            other = new ZzzArchive(Path.Combine(this.gameDirectory, "other.zzz"));
        }
        catch
        {
            main.Dispose();
            throw;
        }
    }

    public ResourceSourceDescriptor Descriptor => new("remastered", GameDataEdition.Remastered, ["classic-world-texl", "classic-world-road-region", "classic-world-water-region", "classic-field-tile", "classic-model", "classic-battle", "demaster-card-artwork", "native-battle-stage-page", "native-battle-model-sheet", "native-battle-body-sheet", "native-field-model-sheet", "native-field-background-page", "classic-ffnx", "demaster-content-upload"], language);

    public IFlArchive OpenArchive(string family)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string name = new ResourceKey(family).Path;
        if (name.Contains('/'))
        {
            throw new ArgumentException("An archive family must be a single logical name.", nameof(family));
        }
        string localized = $"data/lang-{language}/{name}";
        string shared = $"data/{name}";
        string stem = Contains(localized + ".fl") ? localized : shared;
        Stream? listing = null;
        Stream? metrics = null;
        Stream? content = null;
        try
        {
            listing = Open(stem + ".fl");
            metrics = Open(stem + ".fi");
            content = Open(stem + ".fs");
            return FlArchive.Open(listing, metrics, content, false, FlArchiveRepresentation.Files);
        }
        catch
        {
            listing?.Dispose();
            metrics?.Dispose();
            content?.Dispose();
            throw;
        }
    }

    public ResourceContent? Find(ResourceKey key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (catalog.TryGetValue(key, out ResourceContent? cached))
        {
            return cached;
        }
        ResourceContent? result = FindCore(key);
        catalog.Add(key, result);
        return result;
    }

    private ResourceContent? FindCore(ResourceKey key)
    {
        if (FindNamedTable(key) is { } table)
        {
            return table;
        }
        string[] parts = key.Path.Split('/');
        if (parts.Length < 2)
        {
            return null;
        }
        if (parts[0] == "magic" && parts.Length == 2 && Contains("data/exe_dat/" + parts[1]))
        {
            return Content(key, "data/exe_dat/" + parts[1]);
        }
        if (parts[0] == "files")
        {
            string file = "data/" + string.Join('/', parts.Skip(1));
            return Contains(file) ? Content(key, file) : null;
        }
        string family = parts[0];
        if (!archives.TryGetValue(family, out IFlArchive? archive))
        {
            if (!Contains($"data/lang-{language}/{family}.fl") && !Contains($"data/{family}.fl"))
            {
                return null;
            }
            archive = OpenArchive(family);
            archives.Add(family, archive);
        }
        for (int level = 1; level < parts.Length - 1; level++)
        {
            string nestedKey = string.Join('/', parts.Take(level + 1));
            if (!archives.TryGetValue(nestedKey, out IFlArchive? nested))
            {
                IFlArchiveEntry? fl = Entry(archive, parts[level] + ".fl");
                IFlArchiveEntry? fi = Entry(archive, parts[level] + ".fi");
                IFlArchiveEntry? fs = Entry(archive, parts[level] + ".fs");
                if (fl is null || fi is null || fs is null)
                {
                    return null;
                }
                nested = FlArchive.Open(new MemoryStream(Read(fl), false), new MemoryStream(Read(fi), false), new MemoryStream(Read(fs), false), false, FlArchiveRepresentation.Files);
                archives.Add(nestedKey, nested);
            }
            archive = nested;
        }
        string basename = parts[^1];
        string extension = Path.GetExtension(basename);
        if (family == "field" && (extension is ".jsm" or ".msd" or ".map" or ".mim" || language == "jp" && extension == ".inf"))
        {
            string localized = Path.GetFileNameWithoutExtension(basename) + "_" + language + extension;
            if (Entry(archive, localized) is { } translated)
            {
                return new ResourceContent(key, extension.TrimStart('.'), 1, $"{Descriptor.Id}:{key}", translated.OpenForRead);
            }
            if (extension is ".jsm" or ".msd")
            {
                return null;
            }
        }
        IFlArchiveEntry? entry = Entry(archive, basename);
        return entry is null ? null : new ResourceContent(key, Path.GetExtension(parts[^1]).TrimStart('.'), 1, $"{Descriptor.Id}:{key}", entry.OpenForRead);
    }

    private ResourceContent Content(ResourceKey key, string path)
    {
        return new ResourceContent(key, Path.GetExtension(path).TrimStart('.'), 1, $"{Descriptor.Id}:{key}", () => Open(path));
    }

    private bool Contains(string path) => main.Find(path) is not null || other.Find(path) is not null;

    private Stream Open(string path)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ZzzArchiveEntry? first = main.Find(path);
        ZzzArchiveEntry? second = other.Find(path);
        if (first is not null && second is not null)
        {
            throw new InvalidDataException($"Ambiguous mounted resource '{path}'.");
        }
        return first is not null ? main.Open(first) : second is not null ? other.Open(second) : throw new FileNotFoundException($"Mounted resource '{path}' is unavailable.");
    }

    private static IFlArchiveEntry? Entry(IFlArchive archive, string basename)
    {
        IFlArchiveEntry[] matches = archive.Entries.Where(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(basename, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidDataException($"Ambiguous archive resource '{basename}'.");
        }
        return matches.SingleOrDefault();
    }

    private static byte[] Read(IFlArchiveEntry entry)
    {
        using Stream stream = entry.OpenForRead();
        return Read(stream);
    }

    private static byte[] Read(Stream stream)
    {
        using MemoryStream result = new();
        stream.CopyTo(result);
        return result.ToArray();
    }

    public string ResolveFile(string relativeDataPath)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string relative = new ResourceKey(relativeDataPath).Path;
        if (files.TryGetValue(relative, out string? cached))
        {
            return cached;
        }
        string mountedRelative = relative;
        if (relative.StartsWith("music/dmusic/", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(relative).Equals(".sgt", StringComparison.OrdinalIgnoreCase))
        {
            mountedRelative = "music/dmusic/ogg/" + Path.GetFileNameWithoutExtension(relative) + ".ogg";
        }
        else if (relative.Equals("music/eyes_on_me.wav", StringComparison.OrdinalIgnoreCase))
        {
            mountedRelative = "music/dmusic/ogg/eyes_on_me.ogg";
        }
        string localized = $"data/lang-{language}/{mountedRelative}";
        string path = Contains(localized) ? localized : "data/" + mountedRelative;
        if (!Contains(path))
        {
            throw new FileNotFoundException($"Data resource '{relative}' is unavailable.");
        }
        Directory.CreateDirectory(cacheDirectory);
        string target = Path.Combine(cacheDirectory, Guid.NewGuid().ToString("N") + Path.GetExtension(path));
        using (Stream source = Open(path))
        using (FileStream destination = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            source.CopyTo(destination);
        }
        files.Add(relative, target);
        return target;
    }

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
        artworkImages.Clear();
        iconAtlases.Clear();
        main.Dispose();
        other.Dispose();
        if (Directory.Exists(cacheDirectory))
        {
            Directory.Delete(cacheDirectory, true);
        }
    }
}
