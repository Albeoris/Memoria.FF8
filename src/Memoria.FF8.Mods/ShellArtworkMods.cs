using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Publishes a package's Square logo through the startup artwork texture address.</summary>
public sealed class ShellArtworkMods : ITextureReplacementResolver
{
    public const string NativeSquareLogoPath = "shell/square-logo.png";
    private readonly TextureReplacement replacement;

    private ShellArtworkMods(ModFile file, DecodedModImage logo)
    {
        PackageId = file.PackageId;
        replacement = new(logo.Image, $"{file.PackageId}/{file.RelativePath}@{file.Revision}", NativeStartupArtwork.Width, NativeStartupArtwork.Height, logo.MipLevels);
    }

    public string PackageId { get; }

    public static bool IsInput(ModFormat format, string path)
    {
        return format switch
        {
            ModFormat.Memoria => path.Equals(NativeSquareLogoPath, StringComparison.OrdinalIgnoreCase),
            ModFormat.Demaster => path.Equals(RemasteredGameDataSource.SquareLogoPath, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static ShellArtworkMods? Prepare(ModCatalogSnapshot catalog, RemasteredGameDataSource? stockSource, ICollection<ModDiagnostic> diagnostics)
    {
        IEnumerable<ModFile> candidates = catalog.FindAll(ModFormat.Memoria, NativeSquareLogoPath).Concat(catalog.FindAll(ModFormat.Demaster, RemasteredGameDataSource.SquareLogoPath));
        byte[]? stockDigest = null;
        foreach (ModFile file in candidates)
        {
            try
            {
                byte[] bytes = file.ReadBytes();
                if (file.Format == ModFormat.Demaster)
                {
                    if (stockDigest is null)
                    {
                        using Stream? stock = stockSource?.OpenStockSquareLogo();
                        if (stock is null)
                        {
                            diagnostics.Add(new("Demaster.SquareLogoBaselineUnavailable", "The selected Remastered archives have no baseline Square logo for this package.", file.PackageId, file.RelativePath));
                            continue;
                        }
                        stockDigest = SHA256.HashData(stock);
                    }
                    if (SHA256.HashData(bytes).AsSpan().SequenceEqual(stockDigest))
                    {
                        diagnostics.Add(new("Demaster.StockSquareLogo", "The Square logo matches the original Remastered archive.", file.PackageId, file.RelativePath));
                        continue;
                    }
                }
                return new(file, ModImageDecoder.Decode(bytes, file.RelativePath));
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(new("ShellArtwork.InvalidSquareLogo", error.Message, file.PackageId, file.RelativePath));
            }
        }
        return null;
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        return context.Key == NativeStartupArtwork.SquareLogoTexture && context.LogicalWidth == NativeStartupArtwork.Width && context.LogicalHeight == NativeStartupArtwork.Height
            ? replacement : null;
    }
}
