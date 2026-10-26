using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectBuilder
{
    private static IReadOnlyList<LocalizationArtworkFile> ReadArtwork(string directory, string language, CancellationToken cancellation)
    {
        return LocalizationArtworkExporter.Read(directory, language, cancellation).Select(source =>
        {
            string identity = source.Key.Owner.Path + "\0" + source.Key.Subresource + "\0" + source.Key.Palette.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string file = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24].ToLowerInvariant();
            LocalizationArtworkDeclaration declaration = new()
            {
                Owner = source.Key.Owner.Path,
                Subresource = source.Key.Subresource,
                Palette = source.Key.Palette,
                SourceSha256 = source.SourceSha256,
                Width = source.Width,
                Height = source.Height,
                SourceImage = "Localization/Artwork/" + file + ".source.png",
                TargetImage = "Localization/Artwork/" + file + ".target.png"
            };
            return new LocalizationArtworkFile(declaration, source.Png);
        }).ToArray();
    }
}
