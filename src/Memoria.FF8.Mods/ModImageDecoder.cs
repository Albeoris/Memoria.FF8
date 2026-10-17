using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Mods;

public sealed record DecodedModImage(PixelImage Image, IReadOnlyList<PixelImage> MipLevels);

/// <summary>CPU decoding shared by texture-package adapters. MipLevels includes the base image.</summary>
public static class ModImageDecoder
{
    private static readonly ConditionalWeakTable<ModFile, DecodedFileCache> decodedFiles = new();

    /// <summary>Reuses living mip images across catalog generations without retaining unloaded images or files.</summary>
    public static DecodedModImage Decode(ModFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return decodedFiles.GetValue(file, _ => new DecodedFileCache()).Get(file);
    }

    private sealed class DecodedFileCache
    {
        private readonly object sync = new();
        private WeakReference<PixelImage>[]? levels;

        public DecodedModImage Get(ModFile file)
        {
            lock (sync)
            {
                if (levels is not null)
                {
                    PixelImage[] living = new PixelImage[levels.Length];
                    bool complete = true;
                    for (int i = 0; i < levels.Length; i++)
                    {
                        if (!levels[i].TryGetTarget(out PixelImage? image))
                        {
                            complete = false;
                            break;
                        }
                        living[i] = image;
                    }
                    if (complete)
                    {
                        return new DecodedModImage(living[0], Array.AsReadOnly(living));
                    }
                }
                DecodedModImage decoded = DecodeFile(file);
                levels = decoded.MipLevels.Select(image => new WeakReference<PixelImage>(image)).ToArray();
                return decoded;
            }
        }
    }

    private static DecodedModImage DecodeFile(ModFile file)
    {
        try
        {
            return Decode(file.ReadBytes(), file.RelativePath);
        }
        catch (Exception exception) when (exception is not (ModTextureLoadException or OutOfMemoryException or OperationCanceledException))
        {
            throw new ModTextureLoadException(file.PackageId, file.RelativePath, exception);
        }
    }

    public static DecodedModImage Decode(byte[] bytes, string path)
    {
        Memoria.FF8.Resources.DecodedTextureImage decoded = Memoria.FF8.Resources.TextureImageDecoder.Decode(bytes, path);
        return new DecodedModImage(decoded.Image, decoded.MipLevels);
    }
}
