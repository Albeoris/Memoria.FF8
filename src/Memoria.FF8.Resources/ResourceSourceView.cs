using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

/// <summary>A non-owning encoded snapshot for inspecting candidates without changing the active resource service.</summary>
public sealed class ResourceSourceView : IResourceSource
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

    public void Dispose()
    {
    }
}
