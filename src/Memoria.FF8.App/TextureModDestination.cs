namespace Memoria.FF8;

internal static class TextureModDestination
{
    internal static string Resolve(IEnumerable<string> configuredRoots, string destination)
    {
        string full = Path.GetFullPath(destination);
        return configuredRoots.Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, "Memoria"))))
            .Where(root => full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(root => root.Length)
            .FirstOrDefault() ?? throw new InvalidDataException("The destination must be inside a configured Memoria mod directory.");
    }
}
