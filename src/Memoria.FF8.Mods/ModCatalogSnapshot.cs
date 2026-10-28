using System.Collections.ObjectModel;

namespace Memoria.FF8.Mods;

public sealed class ModCatalogSnapshot
{
    private readonly Dictionary<ModFormat, Dictionary<string, IReadOnlyList<ModFile>>> index = [];

    public ModCatalogSnapshot(long generation, IEnumerable<ModPackage> packages, IEnumerable<ModFile> files, IEnumerable<ModDiagnostic>? diagnostics = null)
    {
        Generation = generation;
        Packages = Array.AsReadOnly(packages.ToArray());
        Files = Array.AsReadOnly(files.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
        foreach (ModFormat format in Enum.GetValues<ModFormat>())
        {
            Dictionary<string, IReadOnlyList<ModFile>> entries = new(StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, ModFile> group in Files.Where(file => file.Format == format).GroupBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(group.Key, Array.AsReadOnly(group.ToArray()));
            }
            index.Add(format, entries);
        }
    }

    public long Generation { get; }
    public ReadOnlyCollection<ModPackage> Packages { get; }
    public ReadOnlyCollection<ModFile> Files { get; }
    public ReadOnlyCollection<ModDiagnostic> Diagnostics { get; }

    public IReadOnlyList<ModFile> FindAll(ModFormat format, string relativePath)
    {
        return index[format].TryGetValue(NormalizePath(relativePath), out IReadOnlyList<ModFile>? files) ? files : Array.Empty<ModFile>();
    }

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || normalized.Contains(':'))
        {
            throw new ArgumentException("A mod address must be relative to its package.", nameof(path));
        }
        bool canonical = true;
        ReadOnlySpan<char> remaining = normalized;
        while (true)
        {
            int slash = remaining.IndexOf('/');
            ReadOnlySpan<char> part = slash < 0 ? remaining : remaining[..slash];
            if (part.IsEmpty || part.SequenceEqual(".") || part.SequenceEqual(".."))
            {
                canonical = false;
                break;
            }
            if (slash < 0)
            {
                break;
            }
            remaining = remaining[(slash + 1)..];
        }
        if (canonical)
        {
            return normalized;
        }
        List<string> parts = [];
        foreach (string part in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }
            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    throw new ArgumentException("A mod address escapes its package.", nameof(path));
                }
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(part);
        }
        if (parts.Count == 0)
        {
            throw new ArgumentException("A mod address must identify a file.", nameof(path));
        }
        return string.Join('/', parts);
    }
}
