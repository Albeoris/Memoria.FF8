using System.Globalization;
using System.Numerics;
using System.Text;

namespace Memoria.FF8.Mods;

public sealed record TonberryRule(string Name, bool Persistent, ModFile Table, int Row);

public sealed class TonberryTables
{
    private readonly Dictionary<ulong, TonberryRule> primary = [];
    private readonly Dictionary<ulong, TonberryRule> collisions = [];
    private readonly Dictionary<BigInteger, TonberryRule> secondary = [];
    private readonly Dictionary<ulong, TonberryRule> objects = [];
    private readonly Dictionary<string, ModFile[]> images;
    private readonly ModCatalogSnapshot catalog;
    private readonly List<ModDiagnostic> diagnostics = [];

    public TonberryTables(ModCatalogSnapshot catalog)
    {
        this.catalog = catalog;
        ModFile[] files = catalog.Files.Where(file => file.Format == ModFormat.Tonberry).ToArray();
        images = files.Where(file => file.RelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).GroupBy(file => Path.GetFileName(file.RelativePath), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModFile file in files)
        {
            if (!seenFiles.Add(file.RelativePath))
            {
                continue;
            }
            string? kind = GetTableKind(file.RelativePath);
            if (kind is null)
            {
                continue;
            }
            using StringReader reader = new(Encoding.UTF8.GetString(file.ReadBytes()).TrimStart('\uFEFF'));
            int row = 0;
            while (reader.ReadLine() is { } line)
            {
                row++;
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith('*'))
                {
                    continue;
                }
                int comma = line.IndexOf(',');
                string name = comma < 0 ? "" : line[..comma].Trim();
                bool persistent = name.StartsWith('!');
                if (persistent)
                {
                    name = name[1..];
                }
                string value = comma < 0 ? "" : line[(comma + 1)..].Trim();
                if (name.Length < 2 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !name.Contains('_') || !BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger number) || number.Sign < 0 || kind != "secondary" && number > ulong.MaxValue)
                {
                    diagnostics.Add(new ModDiagnostic("Tonberry.MalformedRow", $"Invalid {kind} row {row}.", file.PackageId, file.RelativePath));
                    continue;
                }
                TonberryRule rule = new(name, persistent, file, row);
                bool inserted = kind switch
                {
                    "primary" => primary.TryAdd((ulong)number, rule),
                    "collision" => collisions.TryAdd((ulong)number, rule),
                    "secondary" => secondary.TryAdd(number, rule),
                    _ => objects.TryAdd((ulong)number, rule)
                };
                if (!inserted)
                {
                    diagnostics.Add(new ModDiagnostic("Tonberry.DuplicateKey", $"The first {kind} row for {number} wins; ignored row {row}.", file.PackageId, file.RelativePath));
                }
            }
        }
        HashSet<string> usablePackages = new(StringComparer.OrdinalIgnoreCase);
        TonberryRule[] collisionGates = collisions.Where(pair => !primary.ContainsKey(pair.Key)).Select(pair => pair.Value).ToArray();
        IEnumerable<TonberryRule> candidates = primary.Values.Concat(objects.Values).Concat(collisionGates.Length > 0 ? secondary.Values : []);
        HashSet<string> checkedNames = new(StringComparer.Ordinal);
        foreach (TonberryRule rule in candidates)
        {
            ModFile? image = FindImage(rule.Name);
            if (image is not null)
            {
                usablePackages.Add(rule.Table.PackageId);
                usablePackages.Add(image.PackageId);
                HasUsableReplacement = true;
            }
            if (!checkedNames.Add(rule.Name))
            {
                continue;
            }
            if (image is null)
            {
                diagnostics.Add(new ModDiagnostic("Tonberry.MissingImage", $"Winning rule '{rule.Name}' has no replacement PNG.", rule.Table.PackageId, rule.Table.RelativePath));
            }
            else if (images.TryGetValue(rule.Name + ".png", out ModFile[]? alternatives) && alternatives.Length > 1)
            {
                diagnostics.Add(new ModDiagnostic("Tonberry.AmbiguousImage", $"Multiple PNGs named '{rule.Name}.png'; selected {image.PackageId}/{image.RelativePath}.", rule.Table.PackageId, rule.Table.RelativePath));
            }
        }
        if (secondary.Values.Any(rule => FindImage(rule.Name) is not null))
        {
            foreach (TonberryRule gate in collisionGates)
            {
                usablePackages.Add(gate.Table.PackageId);
            }
        }
        UsablePackageIds = usablePackages;
    }

    public bool HasUsableReplacement { get; }
    public IReadOnlySet<string> UsablePackageIds { get; }
    public IReadOnlyList<ModDiagnostic> Diagnostics => diagnostics.AsReadOnly();

    public TonberryRule? Match(TonberrySignatures signature)
    {
        if (primary.TryGetValue(signature.Primary, out TonberryRule? rule))
        {
            return rule;
        }
        if (collisions.ContainsKey(signature.Primary))
        {
            return secondary.GetValueOrDefault(signature.Secondary);
        }
        return objects.GetValueOrDefault(signature.ObjectTop) ?? objects.GetValueOrDefault(signature.ObjectBottom);
    }

    public ModFile? FindImage(string name)
    {
        string path = $"textures/{name[..2]}/{name[..name.LastIndexOf('_')]}/{name}.png";
        IReadOnlyList<ModFile> legacy = catalog.FindAll(ModFormat.Tonberry, path);
        if (legacy.Count > 0)
        {
            return legacy[0];
        }
        return images.TryGetValue(name + ".png", out ModFile[]? matches) ? matches[0] : null;
    }

    private static string? GetTableKind(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Any(part => part.Equals("disabled", StringComparison.OrdinalIgnoreCase)) || !path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        string basename = parts[^1];
        if (basename.Equals("collisions.csv", StringComparison.OrdinalIgnoreCase))
        {
            return "collision";
        }
        if (basename.Equals("hash2map.csv", StringComparison.OrdinalIgnoreCase))
        {
            return "secondary";
        }
        if (parts.Length >= 2 && parts[^2].Equals("objmap", StringComparison.OrdinalIgnoreCase) || basename.Equals("objmap.csv", StringComparison.OrdinalIgnoreCase))
        {
            return "object";
        }
        if (parts.Length >= 2 && parts[^2].Equals("hashmap", StringComparison.OrdinalIgnoreCase) || basename.Equals("hash1map.csv", StringComparison.OrdinalIgnoreCase) || basename.EndsWith("_hm.csv", StringComparison.OrdinalIgnoreCase))
        {
            return "primary";
        }
        return null;
    }
}
