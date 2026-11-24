using System.Collections.Frozen;
using System.Security.Cryptography;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    internal sealed record PackageTextureLayouts(IReadOnlyDictionary<string, TextureLayoutMetadata> Entries, string? Identity);

    internal static PackageTextureLayouts ReadTextureLayouts(string packageId, ModFormat format, List<(string Root, string Physical, string Relative)> files)
    {
        if (format != ModFormat.Demaster)
        {
            return new(FrozenDictionary<string, TextureLayoutMetadata>.Empty, null);
        }
        int index = files.FindIndex(file => file.Relative.Equals(TextureLayoutManifest.FileName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return new(FrozenDictionary<string, TextureLayoutMetadata>.Empty, null);
        }
        try
        {
            (string root, string physical, _) = files[index];
            FileInfo info = new(physical);
            if (info.Length > TextureLayoutManifest.MaximumBytes)
            {
                throw new InvalidDataException("Texture layout metadata exceeds its size limit.");
            }
            byte[] bytes = ReadVersion(root, physical, new(info.Length, info.LastWriteTimeUtc.Ticks));
            IReadOnlyDictionary<string, TextureLayoutMetadata> entries = TextureLayoutManifest.Parse(bytes);
            HashSet<string> images = files.Where(file => file.Root.Equals(root, StringComparison.OrdinalIgnoreCase)).Select(file => file.Relative).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string path in entries.Keys)
            {
                if (!images.Contains(path))
                {
                    throw new InvalidDataException($"Texture layout declaration '{path}' requires an unpacked image at that path in the same package.");
                }
            }
            return new(entries, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            throw new PackageTextureLayoutException(packageId, exception.Message, exception);
        }
    }

    private sealed class PackageTextureLayoutException(string packageId, string message, Exception inner) : IOException(message, inner)
    {
        public string PackageId { get; } = packageId;
        public string RelativePath => TextureLayoutManifest.FileName;
    }
}
