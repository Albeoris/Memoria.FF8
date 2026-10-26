using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public static partial class JunctionEncodedResources
{
    private static readonly HashSet<string> StaticFileFormats = new(StringComparer.Ordinal) { "tdw" };

    /// <summary>Identifies registered opaque archive files whose bytes can be published without a renderer consumer.</summary>
    public static bool IsStaticFileResource(ResourceKey key)
    {
        string[] parts = key.Path.Split('/');
        return parts.Length == 2 && (parts[0] is "main" or "menu") && IsStaticFileName(parts[1]);
    }

    private static ResourceKey? StaticFileAddress(string[] parts, int start)
    {
        if (parts.Length - start == 1 && IsStaticFileName(parts[start]))
        {
            return new ResourceKey("main/" + parts[start]);
        }
        if (parts.Length - start == 2 && (parts[start] is "main" or "menu") && IsStaticFileName(parts[start + 1]))
        {
            return new ResourceKey(parts[start] + "/" + parts[start + 1]);
        }
        return null;
    }

    private static bool IsStaticFileName(string name)
    {
        string extension = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        return extension.Length > 1 && StaticFileFormats.Contains(extension[1..]) && stem.Length > 0 && stem.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    }
}
