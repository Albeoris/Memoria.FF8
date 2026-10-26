using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Imaging.Tex;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class FfnxTextureResolver
{
    private static bool TryReadMenuTarget(string target, out ResourceKey key, out int palette, out bool dormant)
    {
        Match match = Regex.Match(target, @"^(?:eng/)?data/eng/menu/(?:hires/)?([a-z0-9_]+)_([0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        key = default;
        palette = 0;
        dormant = false;
        if (!match.Success || !Number(match.Groups[2].Value, out palette))
        {
            return false;
        }
        string name = match.Groups[1].Value.ToLowerInvariant();
        if (Regex.IsMatch(name, @"^iconfl0[0-3]$", RegexOptions.CultureInvariant))
        {
            name = "icon";
        }
        dormant = Regex.IsMatch(name, @"^(?:sysfld|sysfnt|start)[0-9]{2}$", RegexOptions.CultureInvariant);
        key = new("menu/" + name + ".tex");
        return true;
    }

    private static string? UnusedMenuTarget(IResourceSource source, string target, Dictionary<ResourceKey, int?> paletteCounts)
    {
        if (!TryReadMenuTarget(target, out ResourceKey key, out int palette, out bool dormant))
        {
            return null;
        }
        if (!paletteCounts.TryGetValue(key, out int? count))
        {
            ResourceContent? content = source.Find(key);
            if (content is not null)
            {
                using Stream input = content.OpenRead();
                using MemoryStream bytes = new();
                input.CopyTo(bytes);
                count = TexFile.Read(bytes.ToArray()).Palettes.Count;
            }
            paletteCounts.Add(key, count);
        }
        if (count is null)
        {
            return $"The installed source has no menu texture {key}.";
        }
        if (palette >= count)
        {
            return $"Menu texture {key} has {count} native palettes; palette {palette} has no source consumer.";
        }
        return dormant ? $"Menu texture {key} is available in the original source, but the current title and font presentation does not consume this atlas." : null;
    }
}
