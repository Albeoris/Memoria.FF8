using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectExport
{
    private static LocalizationArtworkFile CopyArtwork(LocalizationArtworkFile file, bool emptyTarget = false) => new(file.Declaration with { }, file.SourcePng.ToArray(), emptyTarget ? null : file.TargetPng?.ToArray());

    private static IReadOnlyList<LocalizationArtworkFile> ResolveArtwork(IReadOnlyList<LocalizationProject> projects)
    {
        Dictionary<TextureKey, LocalizationArtworkFile> result = [];
        foreach (LocalizationArtworkFile file in projects.SelectMany(project => project.Artwork))
        {
            if (!result.TryGetValue(file.Declaration.Key, out var previous))
            {
                result.Add(file.Declaration.Key, CopyArtwork(file));
            }
            else
            {
                if (!previous.Declaration.SourceSha256.Equals(file.Declaration.SourceSha256, StringComparison.OrdinalIgnoreCase) || previous.Declaration.Width != file.Declaration.Width || previous.Declaration.Height != file.Declaration.Height)
                {
                    throw new InvalidDataException($"{file.Declaration.Key}: flattening artwork requires the same original resource and native canvas.");
                }
                if (file.TargetPng is not null)
                {
                    result[file.Declaration.Key] = CopyArtwork(file);
                }
            }
        }
        return NormalizeArtworkPaths(result.Values);
    }

    private static IReadOnlyList<LocalizationArtworkFile> ReconcileArtwork(LocalizationProject existing, LocalizationProject refreshed)
    {
        Dictionary<TextureKey, LocalizationArtworkFile> previous = existing.Artwork.ToDictionary(file => file.Declaration.Key);
        List<LocalizationArtworkFile> result = [];
        foreach (LocalizationArtworkFile file in refreshed.Artwork)
        {
            previous.Remove(file.Declaration.Key, out var old);
            result.Add(CopyArtwork(file) with { TargetPng = old?.TargetPng?.ToArray() });
        }
        result.AddRange(previous.Values.Select(file => CopyArtwork(file)));
        return NormalizeArtworkPaths(result);
    }

    private static IReadOnlyList<LocalizationArtworkFile> NormalizeArtworkPaths(IEnumerable<LocalizationArtworkFile> files)
    {
        return files.OrderBy(file => file.Declaration.Owner, StringComparer.Ordinal).ThenBy(file => file.Declaration.Subresource, StringComparer.Ordinal).ThenBy(file => file.Declaration.Palette).Select(file =>
        {
            string identity = file.Declaration.Owner + "\0" + file.Declaration.Subresource + "\0" + file.Declaration.Palette.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24].ToLowerInvariant();
            return file with { Declaration = file.Declaration with { SourceImage = "Localization/Artwork/" + name + ".source.png", TargetImage = "Localization/Artwork/" + name + ".target.png" } };
        }).ToArray();
    }
}
