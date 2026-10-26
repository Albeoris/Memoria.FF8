using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed record ExternalVoiceSelection(ResourceKey Key, string PackageId, string RelativePath, double Gain);

/// <summary>Resolves field voice identities without opening encoded audio payloads.</summary>
public sealed class ExternalVoiceResolver
{
    private readonly Dictionary<ResourceKey, ModFile> files = [];
    private readonly Dictionary<string, Configuration> configurations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> sequentialIndexes = new(StringComparer.Ordinal);
    public IEnumerable<ModFile> Files => files.Values;

    public ExternalVoiceResolver(ModCatalogSnapshot catalog, Func<ModFile, bool>? eligibility = null)
    {
        foreach (ModFile file in Eligible(catalog, eligibility))
        {
            if (Address(file) is ResourceKey key)
            {
                files.TryAdd(key, file);
            }
            else if (IsConfiguration(file))
            {
                configurations.TryAdd(file.PackageId, Configuration.Parse(Encoding.UTF8.GetString(file.ReadBytes())));
            }
        }
    }

    public static IReadOnlyList<ModDiagnostic> Validate(ModCatalogSnapshot catalog, Func<ModFile, bool>? eligibility = null)
    {
        List<ModDiagnostic> diagnostics = [];
        foreach (ModFile file in Eligible(catalog, eligibility))
        {
            string path = Normalize(file.RelativePath);
            if (path.StartsWith("voice/_", StringComparison.Ordinal) && Path.GetExtension(path) == ".ogg")
            {
                diagnostics.Add(new("external-voice-namespace-unsupported", "Only field voice namespaces are supported.", file.PackageId, file.RelativePath));
            }
            if (!IsConfiguration(file))
            {
                continue;
            }
            try
            {
                _ = Configuration.Parse(Encoding.UTF8.GetString(file.ReadBytes()));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException)
            {
                diagnostics.Add(new("external-voice-config-invalid", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        return diagnostics;
    }

    public static ResourceKey? Address(ModFile file) => file.Format == ModFormat.Junction ? Address(file.RelativePath) : null;

    public static ResourceKey? Address(string path)
    {
        string normalized = Normalize(path);
        return Regex.IsMatch(normalized, @"^voice/[a-z][a-z0-9_]*/[a-z0-9_]+\.ogg$", RegexOptions.CultureInvariant) ? new ResourceKey(normalized) : null;
    }

    public ExternalVoiceSelection? Resolve(string field, int window, int message, int page, int? choice = null, int? gameMoment = null)
    {
        field = field.ToLowerInvariant();
        if (!Regex.IsMatch(field, @"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A field voice request requires a canonical field name.", nameof(field));
        }
        byte nativeWindow = unchecked((byte)window);
        byte nativeMessage = unchecked((byte)message);
        byte nativePage = unchecked((byte)page);
        char letter = (char)('a' + Math.Min(nativePage, (byte)25));
        IEnumerable<string> candidates = choice is int option
            ? [$"{field}/{nativeMessage}_{unchecked((byte)option)}"]
            : nativePage == 0
                ? [$"{field}/w{nativeWindow}_{nativeMessage}{letter}", $"{field}/{nativeMessage}{letter}", $"{field}/w{nativeWindow}_{nativeMessage}", $"{field}/{nativeMessage}"]
                : [$"{field}/w{nativeWindow}_{nativeMessage}{letter}", $"{field}/{nativeMessage}{letter}"];
        string[] ordered = candidates.ToArray();
        for (int candidate = 0; candidate < ordered.Length; candidate++)
        {
            string name = ordered[candidate];
            ResourceKey key = new($"voice/{name}.ogg");
            if (!files.TryGetValue(key, out ModFile? owner))
            {
                if (candidate == ordered.Length - 1)
                {
                    foreach (string package in configurations.Keys)
                    {
                        if (Select(name, package, gameMoment) is ExternalVoiceSelection selection)
                        {
                            return selection;
                        }
                    }
                }
                continue;
            }
            return Select(name, owner.PackageId, gameMoment);
        }
        return null;
    }

    private ExternalVoiceSelection? Select(string name, string package, int? gameMoment)
    {
        ResourceKey key = new($"voice/{name}.ogg");
        Configuration? configuration = configurations.GetValueOrDefault(package);
        Rule? rule = configuration?.Rules.GetValueOrDefault(name.Replace('/', '-'));
        if (gameMoment is >= 0 && configuration?.Rules.TryGetValue(name.Replace('/', '-') + ".gm-" + gameMoment.Value.ToString(CultureInfo.InvariantCulture), out Rule? moment) == true)
        {
            rule = moment;
        }
        string? redirect = null;
        if (rule?.Shuffle is string[] shuffle)
        {
            redirect = shuffle[Random.Shared.Next(shuffle.Length)];
        }
        if (rule?.Sequential is string[] sequential)
        {
            string indexKey = package + "\0" + name;
            int index = sequentialIndexes.GetValueOrDefault(indexKey) % sequential.Length;
            redirect = sequential[index];
            sequentialIndexes[indexKey] = (index + 1) % sequential.Length;
        }
        if (redirect is not null && files.TryGetValue(new($"voice/{redirect}.ogg"), out ModFile? alternate) && alternate.PackageId == package)
        {
            return new(new($"voice/{redirect}.ogg"), package, alternate.RelativePath, rule?.Gain ?? 2.75);
        }
        return files.TryGetValue(key, out ModFile? original) && original.PackageId == package ? new(key, package, original.RelativePath, rule?.Gain ?? 2.75) : null;
    }

    private static IEnumerable<ModFile> Eligible(ModCatalogSnapshot catalog, Func<ModFile, bool>? predicate)
    {
        HashSet<string> active = catalog.Packages.Where(package => package.Format == ModFormat.Junction && package.Status == ModEffectiveStatus.Active).Select(package => package.Id).ToHashSet(StringComparer.Ordinal);
        return catalog.Files.Where(file => file.Format == ModFormat.Junction && active.Contains(file.PackageId) && (predicate?.Invoke(file) ?? true));
    }

    private static bool IsConfiguration(ModFile file) => Normalize(file.RelativePath) == "voice/config.toml";
    private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();

    private sealed class Rule
    {
        public double? Gain { get; set; }
        public string[]? Shuffle { get; set; }
        public string[]? Sequential { get; set; }
    }

    private sealed class Configuration
    {
        public Dictionary<string, Rule> Rules { get; } = new(StringComparer.Ordinal);

        public static Configuration Parse(string text)
        {
            Configuration result = new();
            Rule? rule = null;
            HashSet<string> settings = new(StringComparer.Ordinal);
            foreach (string raw in text.TrimStart('\uFEFF').Split('\n'))
            {
                string line = RemoveComment(raw).Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    string section = line[1..^1].Trim();
                    if (!Regex.IsMatch(section, @"^[a-z][a-z0-9_]*-[a-z0-9_]+(?:\.gm-[0-9]+)?$", RegexOptions.CultureInvariant))
                    {
                        throw new NotSupportedException("Unsupported external voice section: " + section);
                    }
                    rule = new();
                    if (!result.Rules.TryAdd(section, rule))
                    {
                        throw new InvalidDataException("Duplicate external voice section: " + section);
                    }
                    settings.Clear();
                    continue;
                }
                int equals = line.IndexOf('=');
                if (rule is null || equals < 1)
                {
                    throw new InvalidDataException("External voice settings require a named section and assignment.");
                }
                string key = line[..equals].Trim();
                string value = line[(equals + 1)..].Trim();
                if (!settings.Add(key))
                {
                    throw new InvalidDataException("Duplicate external voice setting: " + key);
                }
                if (key == "volume" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int volume) && volume is >= 0 and <= 100)
                {
                    rule.Gain = volume / 100d;
                }
                else if (key is "shuffle" or "sequential")
                {
                    string[] names = ParseNames(value);
                    if (key == "shuffle")
                    {
                        rule.Shuffle = names;
                    }
                    else
                    {
                        rule.Sequential = names;
                    }
                    if (rule.Shuffle is not null && rule.Sequential is not null)
                    {
                        throw new InvalidDataException("External voice shuffle and sequential cannot be combined.");
                    }
                }
                else
                {
                    throw new NotSupportedException("Unsupported external voice setting or value: " + key);
                }
            }
            return result;
        }

        private static string[] ParseNames(string value)
        {
            if (!value.StartsWith('[') || !value.EndsWith(']'))
            {
                throw new InvalidDataException("Voice redirects require a nonempty string array.");
            }
            string[] items = value[1..^1].Split(',').Select(item => item.Trim()).ToArray();
            List<string> names = [];
            foreach (string item in items)
            {
                if (item.Length < 3 || item[0] is not ('\'' or '"') || item[^1] != item[0])
                {
                    throw new NotSupportedException("Voice redirect strings require plain quoted field/name values.");
                }
                string name = item[1..^1];
                if (!Regex.IsMatch(name, @"^[a-z][a-z0-9_]*/[a-z0-9_]+$", RegexOptions.CultureInvariant))
                {
                    throw new NotSupportedException("Unsupported voice redirect name: " + name);
                }
                names.Add(name);
            }
            return names.ToArray();
        }

        private static string RemoveComment(string raw)
        {
            char quote = '\0';
            for (int index = 0; index < raw.Length; index++)
            {
                char character = raw[index];
                if (character is '\'' or '"')
                {
                    quote = quote == '\0' ? character : quote == character ? '\0' : quote;
                }
                if (character == '#' && quote == '\0')
                {
                    return raw[..index];
                }
            }
            return raw;
        }
    }
}
