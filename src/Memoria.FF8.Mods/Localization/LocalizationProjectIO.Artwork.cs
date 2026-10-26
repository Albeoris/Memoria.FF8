using System.Collections.Immutable;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectIO
{
    private static IReadOnlyList<LocalizationArtworkFile> ReadArtwork(string root, LocalizationManifest manifest, Func<string, byte[]> read, List<string> missingTargets)
    {
        List<LocalizationArtworkFile> files = [];
        foreach (LocalizationArtworkDeclaration declaration in manifest.Artwork)
        {
            byte[] source = read(Resolve(root, declaration.SourceImage));
            byte[]? target = null;
            if (declaration.TargetImage is string targetPath)
            {
                string fullPath = Resolve(root, targetPath);
                try
                {
                    target = read(fullPath);
                }
                catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    missingTargets.Add(fullPath);
                }
            }
            files.Add(new(declaration, source, target));
        }
        return files;
    }

    private static void WriteArtwork(LocalizationProject project, string root, CancellationToken cancellation)
    {
        foreach (LocalizationArtworkFile file in project.Artwork)
        {
            cancellation.ThrowIfCancellationRequested();
            string source = Resolve(root, file.Declaration.SourceImage);
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllBytes(source, file.SourcePng);
            if (file.TargetPng is not null && file.Declaration.TargetImage is string targetPath)
            {
                string target = Resolve(root, targetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, file.TargetPng);
            }
        }
    }

    private static void ValidateArtworkManifest(LocalizationManifest manifest)
    {
        if (manifest.Artwork is null)
        {
            throw new InvalidDataException("Localization artwork declarations cannot be null.");
        }
        HashSet<TextureKey> keys = [];
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (LocalizationArtworkDeclaration declaration in manifest.Artwork)
        {
            if (declaration is null || string.IsNullOrEmpty(declaration.Owner) || new ResourceKey(declaration.Owner).Path != declaration.Owner || string.IsNullOrWhiteSpace(declaration.Subresource) || declaration.Subresource.Any(char.IsControl) || declaration.Palette < 0 || declaration.Width < 1 || declaration.Height < 1 || string.IsNullOrEmpty(declaration.SourceImage) || string.IsNullOrEmpty(declaration.SourceSha256) || declaration.SourceSha256.Length != 64 || !declaration.SourceSha256.All(char.IsAsciiHexDigit) || !keys.Add(declaration.Key))
            {
                throw new InvalidDataException("Artwork requires a distinct native identity, original fingerprint and positive logical canvas.");
            }
            foreach (string path in new[] { declaration.SourceImage, declaration.TargetImage }.OfType<string>())
            {
                if (!path.StartsWith("Localization/Artwork/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.Split('/').Any(part => part is "" or "." or "..") || !paths.Add(path))
                {
                    throw new InvalidDataException("Artwork PNGs require distinct relative paths under Localization/Artwork.");
                }
                foreach (string part in path.Split('/'))
                {
                    SafeStem(part);
                }
            }
        }
    }

    private static void ValidateArtworkFiles(LocalizationProject project, LocalizationProjectReadCache? cache = null)
    {
        if (project.Artwork is null || project.Artwork.Count != project.Manifest.Artwork.Count)
        {
            throw new InvalidDataException("Localization artwork payloads do not match their manifest.");
        }
        HashSet<TextureKey> keys = [];
        foreach (LocalizationArtworkFile file in project.Artwork)
        {
            if (file is null || file.Declaration is null || !keys.Add(file.Declaration.Key) || !project.Manifest.Artwork.Contains(file.Declaration) || file.SourcePng is null || file.SourcePng.Length > MaximumFileBytes || file.TargetPng?.LongLength > MaximumFileBytes || file.TargetPng is not null && file.Declaration.TargetImage is null)
            {
                throw new InvalidDataException("Artwork files must retain their declared native binding and bounded PNG payloads.");
            }
            LocalizationProjectReadCache.ArtworkDimensions source;
            if (cache is null)
            {
                var decoded = TextureImageDecoder.Decode(file.SourcePng, file.Declaration.SourceImage).Image;
                source = new(decoded.Width, decoded.Height);
            }
            else
            {
                source = cache.ReadArtworkDimensions(file.SourcePng, file.Declaration.SourceImage);
            }
            if (source.Width != file.Declaration.Width || source.Height != file.Declaration.Height)
            {
                throw new InvalidDataException("Artwork Source image dimensions differ from the native canvas.");
            }
            if (file.TargetPng is not null)
            {
                var target = cache?.ReadArtworkImage(file.TargetPng, file.Declaration.TargetImage!) ?? DecodeArtworkImage(file.TargetPng, file.Declaration.TargetImage!);
                if (target.Width % source.Width != 0 || target.Height % source.Height != 0 || target.Width / source.Width != target.Height / source.Height)
                {
                    throw new InvalidDataException("Artwork Target must preserve the native canvas at an integer scale.");
                }
            }
        }
    }

    private static LocalizationProjectReadCache.ArtworkImage DecodeArtworkImage(byte[] bytes, string path)
    {
        var image = TextureImageDecoder.Decode(bytes, path).Image;
        return new(image.Width, image.Height, image.Pixels.ToImmutableArray());
    }
}
