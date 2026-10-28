using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed partial class DemasterTextureResolver
{
    private const string ContentUploadLayout = "demaster-content-upload";
    private const string ContentUploadPrefix = "hashOutput/";
    private readonly ConditionalWeakTable<TextureBuildContext, ContentUploadRecognition> contentUploads = new();
    private readonly Dictionary<ulong, ulong> contentUploadHashes = [];
    private bool? hasContentUploadTargets;

    private TextureReplacement? ResolveContentUpload(TextureBuildContext context)
    {
        if (!HasLayout(context.Source, ContentUploadLayout) || context.NativeUpload is not TextureSourceUpload upload || context.LogicalWidth <= 0 || context.LogicalHeight <= 0 || !IsContentUploadRegion(upload) || !HasContentUploadTargets())
        {
            return null;
        }
        ContentUploadRecognition recognition = contentUploads.GetValue(context, _ => RecognizeContentUpload(upload));
        if (recognition.Fingerprint is not DemasterTextureFingerprint fingerprint)
        {
            return null;
        }
        foreach (string path in WithExtensions($"{ContentUploadPrefix}{fingerprint.High64:X16}_HD"))
        {
            ModFile? file = catalog.FindAll(ModFormat.Demaster, path).FirstOrDefault();
            if (file is null)
            {
                continue;
            }
            lock (contentUploadHashes)
            {
                if (contentUploadHashes.TryGetValue(fingerprint.High64, out ulong low64) && low64 != fingerprint.Low64)
                {
                    throw ContentUploadFailure(file, "Demaster content fingerprint collides with a different native upload.");
                }
                contentUploadHashes[fingerprint.High64] = fingerprint.Low64;
            }
            DecodedModImage decoded = ModImageDecoder.Decode(file);
            if (decoded.Image.Width <= 0 || decoded.Image.Height <= 0 || decoded.Image.Width % upload.Width != 0 || decoded.Image.Height % upload.Height != 0 || decoded.Image.Width / upload.Width != decoded.Image.Height / upload.Height)
            {
                throw ContentUploadFailure(file, "Demaster content texture requires equal positive integral scaling of the native upload canvas.");
            }
            List<PixelImage> levels = [];
            foreach (PixelImage mip in decoded.MipLevels)
            {
                if ((long)upload.X * mip.Width % upload.Width != 0 || (long)upload.Y * mip.Height % upload.Height != 0 || (long)upload.RegionWidth * mip.Width % upload.Width != 0 || (long)upload.RegionHeight * mip.Height % upload.Height != 0)
                {
                    break;
                }
                int x = (int)((long)upload.X * mip.Width / upload.Width);
                int y = (int)((long)upload.Y * mip.Height / upload.Height);
                int width = (int)((long)upload.RegionWidth * mip.Width / upload.Width);
                int height = (int)((long)upload.RegionHeight * mip.Height / upload.Height);
                if (width <= 0 || height <= 0)
                {
                    break;
                }
                PixelImage crop = new(width, height);
                for (int row = 0; row < height; row++)
                {
                    mip.Pixels.AsSpan((y + row) * mip.Width + x, width).CopyTo(crop.Pixels.AsSpan(row * width, width));
                }
                levels.Add(crop);
            }
            if (levels.Count == 0)
            {
                throw ContentUploadFailure(file, "Demaster content texture cannot represent the native upload rectangle.");
            }
            return new(levels[0], $"{file.PackageId}/{file.RelativePath}@{file.Revision}", context.LogicalWidth, context.LogicalHeight, levels.AsReadOnly());
        }
        return null;
    }

    private static ContentUploadRecognition RecognizeContentUpload(TextureSourceUpload upload)
    {
        return new(DemasterTextureFingerprint.TryCalculate(upload.Pixels.Span, upload.Width, upload.Height, upload.BytesPerPixel, upload.NativeMode, out DemasterTextureFingerprint fingerprint) ? fingerprint : null);
    }

    private static bool IsContentUploadRegion(TextureSourceUpload upload)
    {
        return upload.Width is > 0 and < 1024 && upload.Height is > 0 and < 1024 && upload.BytesPerPixel is 3 or 4 && upload.X >= 0 && upload.Y >= 0 && upload.RegionWidth > 0 && upload.RegionHeight > 0 && (long)upload.X + upload.RegionWidth <= upload.Width && (long)upload.Y + upload.RegionHeight <= upload.Height;
    }

    private static bool IsContentUploadFamily(string path)
    {
        return path.StartsWith(ContentUploadPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasContentUploadTargets()
    {
        lock (contentUploadHashes)
        {
            return hasContentUploadTargets ??= catalog.Files.Any(file => file.Format == ModFormat.Demaster && IsContentUploadFamily(file.RelativePath) && Path.GetExtension(file.RelativePath) is string extension && (extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)) && IsContentUploadTarget(file.RelativePath[..^extension.Length]));
        }
    }

    private static bool IsContentUploadTarget(string stem)
    {
        if (!IsContentUploadFamily(stem) || stem.Length != ContentUploadPrefix.Length + 19 || !stem.EndsWith("_HD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return stem.AsSpan(ContentUploadPrefix.Length, 16).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    }

    private static ModTextureLoadException ContentUploadFailure(ModFile file, string message)
    {
        return new(file.PackageId, file.RelativePath, new InvalidDataException(message));
    }

    private sealed record ContentUploadRecognition(DemasterTextureFingerprint? Fingerprint);
}
