using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

/// <summary>A complete indexed text table in the current resource publication.</summary>
public sealed record StaticTextResource(ResourceKey Key, ImmutableArray<string> Texts, string PackageId, string RelativePath)
{
    public static ResourceKey CardNames { get; } = new("text/exe/card_names");

    public string? At(int index) => (uint)index < Texts.Length ? Texts[index] : null;

    public static ResourceKey? Address(string path)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        if (normalized.StartsWith("direct/", StringComparison.Ordinal))
        {
            normalized = normalized[7..];
        }
        const string prefix = "exe/";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal) || !normalized.EndsWith(".msd", StringComparison.Ordinal))
        {
            return null;
        }
        return new ResourceKey("text/exe/" + normalized[prefix.Length..^4]);
    }
}
