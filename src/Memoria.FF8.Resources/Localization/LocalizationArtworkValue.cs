using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Resources.Localization;

/// <summary>One decoded immutable artwork replacement, preserving its native logical canvas.</summary>
public sealed record LocalizationArtworkValue(TextureKey Key, int LogicalWidth, int LogicalHeight, int Width, int Height, ImmutableArray<Rgba32> Pixels, string SourceSha256, string PackageId, string ProjectId)
{
    public void Validate()
    {
        if (LogicalWidth < 1 || LogicalHeight < 1 || Width < 1 || Height < 1 || Width > PixelImage.MaximumDimension || Height > PixelImage.MaximumDimension || (long)Width * Height > PixelImage.MaximumPixelCount || Pixels.IsDefault || Pixels.Length != (long)Width * Height || Width % LogicalWidth != 0 || Height % LogicalHeight != 0 || Width / LogicalWidth != Height / LogicalHeight || string.IsNullOrEmpty(SourceSha256) || SourceSha256.Length != 64 || !SourceSha256.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException($"{Key}: artwork must preserve its native canvas at an integer scale and bind its original resource fingerprint.");
        }
    }

    public TextureReplacement CreateReplacement(TextureBuildContext context)
    {
        if (context.LogicalWidth != LogicalWidth || context.LogicalHeight != LogicalHeight)
        {
            throw new InvalidDataException($"{Key}: localized artwork does not match its native logical canvas.");
        }
        return new(new PixelImage(Width, Height, Pixels.ToArray()), PackageId + "/" + ProjectId, LogicalWidth, LogicalHeight);
    }
}
