using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Resources;

/// <summary>A frozen encoded resource selected before its consumers are initialized.</summary>
public sealed class EncodedResource
{
    public EncodedResource(ResourceKey key, string format, long revision, string provenance, ReadOnlySpan<byte> bytes, ResourceKey? exclusiveScope = null)
    {
        byte[] owned = bytes.ToArray();
        Fingerprint = Convert.ToHexString(SHA256.HashData(owned));
        Content = new(key, format, revision, provenance, () => new MemoryStream(owned, false));
        ExclusiveScope = exclusiveScope;
    }

    internal EncodedResource(ResourceContent content, string fingerprint, ResourceKey? exclusiveScope)
    {
        Content = content;
        Fingerprint = fingerprint;
        ExclusiveScope = exclusiveScope;
    }

    public ResourceKey Key => Content.Key;
    public ResourceContent Content { get; }
    public string Fingerprint { get; }
    /// <summary>A complete namespace whose absent members must not be supplied by the installed source.</summary>
    public ResourceKey? ExclusiveScope { get; }

    internal static ImmutableArray<string> ScopePrefixes(IEnumerable<EncodedResource> resources) => resources.Where(resource => resource.ExclusiveScope.HasValue).Select(resource => resource.ExclusiveScope!.Value.Path + "/").Distinct(StringComparer.Ordinal).ToImmutableArray();

    internal static bool IsInScope(ResourceKey key, ImmutableArray<string> prefixes) => prefixes.Any(prefix => key.Path.StartsWith(prefix, StringComparison.Ordinal));
}

public sealed class ResourceReloadRequiredException(ImmutableArray<ResourceKey> resources) : InvalidOperationException("Restart the game to apply changes to resources that are already loaded.")
{
    public ImmutableArray<ResourceKey> Resources { get; } = resources;
}
