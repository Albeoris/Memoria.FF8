using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

public enum GameDataEdition
{
    Classic,
    Remastered
}

public sealed record ResourceSourceDescriptor(string Id, GameDataEdition Edition, ImmutableArray<string> Layouts, string Language = "en");

/// <summary>A game-relative identity, independent of its archive and installation.</summary>
public readonly record struct ResourceKey
{
    public ResourceKey(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains(':') || normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException("A resource identity must be a relative logical path without traversal.", nameof(path));
        }
        Path = normalized.ToLowerInvariant();
    }

    public string Path { get; }
    public override string ToString() => Path;
}

public sealed record ResourceContent(ResourceKey Key, string Format, long Revision, string Provenance, Func<Stream> OpenRead);

public sealed record ResourcePrefix(byte[] Bytes, long? Length);

/// <summary>Optional bounded reads for sources whose ordinary resources require materializing nested storage.</summary>
public interface IResourcePrefixSource
{
    ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes);
}

public interface IResourceSource : IDisposable
{
    ResourceSourceDescriptor Descriptor { get; }
    ResourceContent? Find(ResourceKey key);
}

public sealed class InMemoryResourceSource : IResourceSource
{
    private readonly Dictionary<ResourceKey, ResourceContent> entries = [];
    private long revision;

    public InMemoryResourceSource(ResourceSourceDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public ResourceSourceDescriptor Descriptor { get; }

    public void Set(ResourceKey key, ReadOnlySpan<byte> content, string format)
    {
        byte[] owned = content.ToArray();
        entries[key] = new ResourceContent(key, format, ++revision, Descriptor.Id, () => new MemoryStream(owned, false));
    }

    public ResourceContent? Find(ResourceKey key) => entries.GetValueOrDefault(key);

    public void Dispose()
    {
        entries.Clear();
    }
}

public sealed record ResourceChangeSet(long OldGeneration, long NewGeneration, ImmutableArray<TextureKey> Textures, string Reason)
{
    public ImmutableArray<ResourceKey> Texts { get; init; } = [];
    public ImmutableArray<ResourceKey> Resources { get; init; } = [];
}

public interface IResourceChangeFeed
{
    IDisposable Subscribe(Action<ResourceChangeSet> callback);
}
