using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal static class TextureImportSemantics
{
    internal static string Fingerprint(PixelImage image, TextureTarget target, TextureTargetContract contract, TextureBuildContext source, TextureNativeCoverage? coverage = null, TextureValue? installedOriginal = null)
    {
        if (coverage is null && (installedOriginal is null ? TexturePixelExchange.UsesNativeFlags(source) : TexturePixelExchange.UsesNativeFlags(source, installedOriginal)))
        {
            PixelImage original = installedOriginal?.Image ?? source.Original;
            coverage = new(original.Width, original.Height, TexturePixelExchange.CaptureNativeFlags(original));
        }
        if (coverage is not null)
        {
            try
            {
                if (coverage.Width <= 0 || coverage.Height <= 0 || (long)coverage.Width * coverage.Height > PixelImage.MaximumPixelCount || Convert.FromBase64String(coverage.Alpha).Length != (long)coverage.Width * coverage.Height)
                {
                    throw new InvalidDataException("Native coverage dimensions and flag count do not match. Re-export or restore this image's source companion.");
                }
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Native coverage flags must be valid base64. Restore the source companion.", exception);
            }
        }
        return TexturePixelExchange.SemanticFingerprint(image, target, contract, coverage?.Alpha, coverage?.Width, coverage?.Height);
    }
}
