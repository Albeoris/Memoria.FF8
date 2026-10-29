using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class ClassicGameDataSource : IResourcePrefixSource
{
    public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
    {
        lock (archiveGate)
        {
            return ReadPrefixCore(key, maximumBytes);
        }
    }

    private ResourcePrefix? ReadPrefixCore(ResourceKey key, int maximumBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        string[] parts = key.Path.Split('/');
        if (parts[0] != "field" || parts.Length is not (2 or 3))
        {
            return null;
        }
        if (!archives.TryGetValue("field", out IFlArchive? outer))
        {
            outer = OpenArchive("field");
            archives.Add("field", outer);
        }
        if (parts.Length == 2)
        {
            return ReadEntryPrefix(outer, parts[1], maximumBytes);
        }
        if (!HasEntry(outer, parts[1] + ".fl") || !HasEntry(outer, parts[1] + ".fi") || !HasEntry(outer, parts[1] + ".fs"))
        {
            return null;
        }
        byte[] listing = ReadEntry(outer, parts[1] + ".fl");
        byte[] metrics = ReadEntry(outer, parts[1] + ".fi");
        IFlArchiveEntry content = outer.Entries.Single(entry => NativeHeaderBasename(entry.RelativePath).Equals(parts[1] + ".fs", StringComparison.OrdinalIgnoreCase));
        using IFlArchive nested = NativeActorInfoArenaReader.OpenHeaderArchive(listing, metrics, content.OpenForRead(), content.Size);
        return ReadEntryPrefix(nested, parts[2], maximumBytes);
    }

    private static ResourcePrefix? ReadEntryPrefix(IFlArchive archive, string basename, int maximumBytes)
    {
        IFlArchiveEntry? entry = archive.Entries.SingleOrDefault(entry => NativeHeaderBasename(entry.RelativePath).Equals(basename, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }
        byte[] prefix = new byte[(int)Math.Min(entry.Size, maximumBytes)];
        if (prefix.Length > 0)
        {
            using Stream stream = entry.OpenForRead();
            stream.ReadExactly(prefix);
        }
        return new ResourcePrefix(prefix, entry.Size);
    }

    private static string NativeHeaderBasename(string path)
    {
        return path.Replace('\\', '/').Split('/')[^1];
    }
}
