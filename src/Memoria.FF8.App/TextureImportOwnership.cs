using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed record TextureImportOwnershipResult(int Consumers, int OwnedConsumers, IReadOnlyList<string> Owners)
{
    internal string Describe()
    {
        if (Consumers == 0)
        {
            return "Installed; nonresident source loads on demand";
        }
        if (OwnedConsumers == Consumers)
        {
            return "Active source from " + string.Join("; ", Owners);
        }
        if (OwnedConsumers != 0)
        {
            return $"Active for {OwnedConsumers} of {Consumers} resident consumers; ownership: " + string.Join("; ", Owners);
        }
        return "Imported but overridden by " + string.Join("; ", Owners);
    }
}

internal static class TextureImportOwnership
{
    internal static TextureImportOwnershipResult Inspect(TextureTarget target, IEnumerable<TextureSourceSnapshot> resident, string package)
    {
        TextureSourceSnapshot[] matches = resident.Where(snapshot => Matches(target, snapshot)).ToArray();
        string[] owners = matches.Select(snapshot => Owner(target, snapshot)).Distinct(StringComparer.Ordinal).ToArray();
        return new(matches.Length, matches.Count(snapshot => Owner(target, snapshot).StartsWith(package + "/", StringComparison.Ordinal)), owners);
    }

    internal static bool Matches(TextureTarget target, TextureSourceSnapshot snapshot)
    {
        TextureBuildContext context = snapshot.Context;
        if (target.Game != context.Game || (target.Variant != (context.RequestedVariant ?? "") && snapshot.Resolved.Variants?.ContainsKey(target.Variant) != true))
        {
            return false;
        }
        bool AddressMatches(TextureSourceAddress? address) => address is not null && target.Owner == address.Owner.Path && target.Subresource == address.Subresource && target.Palette == address.Palette;
        return target.Key == context.Key || AddressMatches(context.NativeAlias) || AddressMatches(context.NativeSheet);
    }

    private static string Owner(TextureTarget target, TextureSourceSnapshot snapshot)
    {
        string[] contributions = snapshot.Resolved.Provenance.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (target.Variant.Length != 0)
        {
            string prefix = "variant " + target.Variant + ": ";
            string? variant = contributions.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
            if (variant is not null)
            {
                return variant[prefix.Length..];
            }
        }
        return contributions.FirstOrDefault() ?? snapshot.Resolved.Provenance;
    }
}
