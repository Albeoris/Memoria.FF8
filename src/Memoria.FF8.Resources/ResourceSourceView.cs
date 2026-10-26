using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

/// <summary>A non-owning encoded snapshot for inspecting candidates without changing the active resource service.</summary>
public sealed class ResourceSourceView : IResourceSource, IResourcePrefixSource
{
    private readonly IResourceSource source;
    private readonly ImmutableDictionary<ResourceKey, EncodedResource> encoded;
    private readonly ImmutableArray<string> exclusiveScopes;

    public ResourceSourceView(IResourceSource source, IEnumerable<EncodedResource> encoded)
    {
        this.source = source;
        this.encoded = encoded.ToImmutableDictionary(resource => resource.Key);
        exclusiveScopes = EncodedResource.ScopePrefixes(this.encoded.Values);
    }

    public ResourceSourceDescriptor Descriptor => source.Descriptor;

    public ResourceContent? Find(ResourceKey key) => encoded.GetValueOrDefault(key)?.Content ?? (EncodedResource.IsInScope(key, exclusiveScopes) ? null : source.Find(key));

    public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (encoded.TryGetValue(key, out EncodedResource? resource))
        {
            using Stream input = resource.Content.OpenRead();
            long? length = input.CanSeek ? input.Length : null;
            using MemoryStream output = new();
            byte[] buffer = new byte[Math.Min(maximumBytes, 81920)];
            int remaining = maximumBytes;
            while (remaining > 0)
            {
                int read = input.Read(buffer, 0, Math.Min(remaining, buffer.Length));
                if (read == 0)
                {
                    break;
                }
                output.Write(buffer, 0, read);
                remaining -= read;
            }
            return new ResourcePrefix(output.ToArray(), length);
        }
        return EncodedResource.IsInScope(key, exclusiveScopes) ? null : (source as IResourcePrefixSource)?.ReadPrefix(key, maximumBytes);
    }

    public void Dispose()
    {
    }
}
