using Memoria.Launcher;

namespace Memoria.FF8.Mods;

internal static class ModResourcePath
{
    public static string GetAbsolutePath(string root, string document, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidDataException("An image path is required.");
        }

        bool fromRoot = reference.StartsWith('/');
        string path = fromRoot ? reference[1..] : reference;
        int depth = fromRoot ? 0 : document.Count(character => character == '/');
        foreach (string segment in path.Split('/'))
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                if (depth == 0)
                {
                    throw new InvalidDataException($"Mod resource address escapes its package: '{reference}'.");
                }
                depth--;
                continue;
            }
            if (segment.Length == 0 || segment.EndsWith(' ') || segment.EndsWith('.') || segment.Any(character => character < 32 || "<>:\\\"|?*".Contains(character)) || IsDevice(segment))
            {
                throw new InvalidDataException($"Unsafe mod resource address '{reference}'.");
            }
            depth++;
        }

        string package = Path.GetFullPath(root);
        string folder = fromRoot ? package : Path.GetDirectoryName(Path.GetFullPath(document, package))!;
        string absolute = Path.GetFullPath(path, folder);
        string relative = Path.GetRelativePath(package, absolute);
        if (Path.IsPathRooted(relative) || relative is "." or ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mod resource address escapes its package: '{reference}'.");
        }

        PackageManifest.EnsureNoReparsePoints(absolute);
        return absolute;
    }

    private static bool IsDevice(string segment)
    {
        string stem = segment.Split('.')[0].TrimEnd().ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 && (stem[..3] is "COM" or "LPT") && "0123456789Â¹Â²Â³".Contains(stem[3]);
    }
}
