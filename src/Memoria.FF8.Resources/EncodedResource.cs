using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Resources;

/// <summary>A frozen encoded resource selected before its consumers are initialized.</summary>
public sealed class EncodedResource
{
    public EncodedResource(ResourceKey key, string format, long revision, string provenance, ReadOnlySpan<byte> bytes)
    {
        byte[] owned = bytes.ToArray();
        Fingerprint = Convert.ToHexString(SHA256.HashData(owned));
        Content = new(key, format, revision, provenance, () => new MemoryStream(owned, false));
    }

    public ResourceKey Key => Content.Key;
    public ResourceContent Content { get; }
    public string Fingerprint { get; }
}

public sealed class ResourceReloadRequiredException(ImmutableArray<ResourceKey> resources) : InvalidOperationException("Restart the game to apply changes to resources that are already loaded.")
{
    public ImmutableArray<ResourceKey> Resources { get; } = resources;
}
