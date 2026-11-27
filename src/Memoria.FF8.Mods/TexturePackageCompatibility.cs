namespace Memoria.FF8.Mods;

internal static class TexturePackageCompatibility
{
    internal static bool IsTextureContentOnly(IEnumerable<string> contributions, bool hasLocalization)
    {
        return IsPresentationContentOnly(contributions, hasLocalization, allowMovies: false);
    }

    internal static bool IsMoviePresentationOnly(IEnumerable<string> contributions, bool hasLocalization)
    {
        return IsPresentationContentOnly(contributions, hasLocalization, allowMovies: true);
    }

    private static bool IsPresentationContentOnly(IEnumerable<string> contributions, bool hasLocalization, bool allowMovies)
    {
        bool hasTexture = false;
        bool hasMovie = false;
        foreach (string source in contributions)
        {
            string path = ModCatalogSnapshot.NormalizePath(source);
            if (path.Equals("mod.ini", StringComparison.OrdinalIgnoreCase) || path.Equals(NativeModDescriptor.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (path.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
            {
                if (TextureModPaths.IsImage(path))
                {
                    hasTexture = true;
                    continue;
                }
                if (path.EndsWith(TextureModMapping.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            if (DemasterSceneLighting.IsNativeInput(path) || path.StartsWith("scene-lighting/lut/", StringComparison.OrdinalIgnoreCase) || ShellArtworkMods.IsInput(ModFormat.Memoria, path) || DemasterFieldLayers.IsPayload(ModFormat.Memoria, path))
            {
                hasTexture = true;
                continue;
            }
            if (hasLocalization && path.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (allowMovies && (path.Equals(MoviePackagePaths.ComponentsDocument, StringComparison.OrdinalIgnoreCase) || MoviePackagePaths.IsSubtitleDocument(path) || MoviePackagePaths.IsNativeMovieVideo(path) || MoviePackagePaths.IsNativeMovieCamera(path)))
            {
                hasMovie = true;
                continue;
            }
            string name = Path.GetFileName(path);
            string extension = Path.GetExtension(name);
            bool documentation = extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || extension.Length == 0;
            if (documentation && (path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) || new[] { "README", "LICENSE", "COPYING", "AUTHORS", "CHANGELOG" }.Any(prefix => name.Equals(prefix, StringComparison.OrdinalIgnoreCase) || name.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }
            return false;
        }
        return allowMovies ? hasMovie : hasTexture;
    }
}
