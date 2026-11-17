using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public sealed record StatusNameOverride(NativeStatusId Status, LocalizedTextReference Reference, ModFile Source);

/// <summary>Status overrides and their owners from the selected native mod generation.</summary>
public sealed class StatusMods
{
    internal const string SettingsBehavior = "status";
    private const int MaximumDocumentBytes = 128 * 1024;

    public static StatusMods Empty { get; } = new([], [], [], []);

    private StatusMods(ImmutableArray<StatusStatModifier> modifiers, ImmutableArray<StatusNameOverride> names, ImmutableArray<string> packageIds, Dictionary<(NativeStatusId, BattleStatId), ModFile> modifierSources)
    {
        Modifiers = modifiers;
        Names = names;
        PackageIds = packageIds;
        this.modifierSources = modifierSources.ToImmutableDictionary();
    }

    private readonly ImmutableDictionary<(NativeStatusId, BattleStatId), ModFile> modifierSources;

    public ImmutableArray<StatusStatModifier> Modifiers { get; }
    public ImmutableArray<StatusNameOverride> Names { get; }
    public ImmutableArray<string> PackageIds { get; }

    public ModFile? ModifierSource(NativeStatusId status, BattleStatId stat) => modifierSources.GetValueOrDefault((status, stat));

    public static bool IsStatusDocument(ModFile file)
    {
        return file.Format == ModFormat.Memoria && IsStatusDocumentPath(file.RelativePath);
    }

    private static bool IsStatusDocumentPath(string path)
    {
        return path.StartsWith("statuses/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out StatusMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        if (catalog.PreparedStatusMods is { } prepared)
        {
            result = prepared;
            diagnostics = [];
            return true;
        }

        Dictionary<(NativeStatusId, BattleStatId), StatusStatModifier> modifiers = [];
        Dictionary<(NativeStatusId, BattleStatId), ModFile> sources = [];
        Dictionary<NativeStatusId, StatusNameOverride> names = [];
        List<string> packageIds = [];
        List<ModDiagnostic> errors = [];
        var packages = catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active).ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        foreach (ModDiagnostic notice in catalog.Diagnostics)
        {
            if (notice.PackageId is { } id && packages.TryGetValue(id, out ModPackage? package) && package.Format == ModFormat.Memoria && notice.RelativePath is { } path
                && ((notice.Code == "normalized-path-conflict" && IsStatusDocumentPath(path)) ||
                    (notice.Code == "escaping-link-rejected" && (path.Equals("statuses", StringComparison.OrdinalIgnoreCase) || path.StartsWith("statuses/", StringComparison.OrdinalIgnoreCase)))))
            {
                errors.Add(new("Memoria.InvalidStatusOverride", notice.Message, id, path));
            }
        }
        foreach (var group in catalog.Files.Where(IsStatusDocument).Where(file => packages.ContainsKey(file.PackageId)).GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            ModPackage package = packages[group.Key];
            HashSet<NativeStatusId> seen = [];
            foreach (ModFile file in group)
            {
                try
                {
                    StatusDocument document = Read(package, file, seen);
                    if (document.Name is { } reference)
                    {
                        names.TryAdd(document.Status, new(document.Status, reference, file));
                    }
                    foreach (StatusContribution contribution in document.Contributions)
                    {
                        BehaviorModSetting? setting = ResolveSetting(contribution.Setting, catalog.StatusPreferences, DifficultyModPolicy.LocksDefaults(catalog.Hardcore, package.Id));
                        var key = (document.Status, contribution.Modifier.Stat);
                        StatusStatModifier modifier = setting is null ? contribution.Modifier : contribution.Modifier with { Value = setting.Value };
                        if (modifiers.TryAdd(key, modifier))
                        {
                            sources.Add(key, file);
                        }
                    }
                }
                catch (Exception exception) when (IsDocumentError(exception))
                {
                    errors.Add(new("Memoria.InvalidStatusOverride", exception.Message, package.Id, file.RelativePath));
                }
            }
            packageIds.Add(package.Id);
        }
        diagnostics = errors.AsReadOnly();
        result = errors.Count == 0
            ? new([.. modifiers.Values.OrderBy(modifier => modifier.Status).ThenBy(modifier => modifier.Stat)], [.. names.Values.OrderBy(name => name.Status)], [.. packageIds], sources)
            : Empty;
        return errors.Count == 0;
    }

    internal static IReadOnlyList<BehaviorModSetting> ReadSettings(ModPackage package, IEnumerable<ModFile> files, IReadOnlyDictionary<string, int> preferences, bool lockDefaults)
    {
        HashSet<NativeStatusId> seen = [];
        List<BehaviorModSetting> settings = [];
        foreach (ModFile file in files.Where(IsStatusDocument))
        {
            foreach (StatusContribution contribution in Read(package, file, seen).Contributions)
            {
                if (ResolveSetting(contribution.Setting, preferences, lockDefaults) is { } setting)
                {
                    settings.Add(setting);
                }
            }
        }
        return settings.AsReadOnly();
    }

    private static BehaviorModSetting? ResolveSetting(BehaviorModSetting? setting, IReadOnlyDictionary<string, int> preferences, bool lockDefaults)
    {
        if (setting is null || !preferences.TryGetValue(setting.Parameter, out int value))
        {
            return setting;
        }
        if (value < setting.Min || value > setting.Max)
        {
            throw new InvalidDataException($"Saved status setting {setting.Parameter} must be an integer between {setting.Min} and {setting.Max}.");
        }
        return lockDefaults ? setting : setting with { Value = value };
    }

    private static StatusDocument Read(ModPackage package, ModFile file, HashSet<NativeStatusId> seen)
    {
        NativeModDescriptor descriptor = package.Descriptor ?? throw new InvalidDataException("A status override requires a native mod descriptor.");
        if (file.AbsolutePath is { } expected && ModResourcePath.GetAbsolutePath(package.RootDirectory, NativeModDescriptor.FileName, file.RelativePath) != expected)
        {
            throw new InvalidDataException($"Status document '{file.RelativePath}' must be inside its mod directory.");
        }
        if (file.ContentStamp is { Length: > MaximumDocumentBytes })
        {
            throw new InvalidDataException("The status override exceeds its size limit.");
        }
        byte[] content = file.ReadBytes();
        if (content.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The status override exceeds its size limit.");
        }

        using JsonDocument json = JsonDocument.Parse(content);
        Dictionary<string, JsonElement> properties = ReadObject(json.RootElement, "id", "name", "statModifiers");
        NativeStatusId status = ReadEnum<NativeStatusId>(properties.GetValueOrDefault("id"), "status id");
        if (!seen.Add(status))
        {
            throw new InvalidDataException($"Status {status} has more than one override document in this mod.");
        }
        LocalizedTextReference? name = properties.TryGetValue("name", out JsonElement nameElement)
            ? LocalizedTextReference.Parse(NativeModDescriptor.ReadText(nameElement, "name"))
            : null;
        List<StatusContribution> modifiers = [];
        if (properties.TryGetValue("statModifiers", out JsonElement stats))
        {
            foreach (var (key, statElement) in ReadObject(stats))
            {
                BattleStatId stat = ReadEnum<BattleStatId>(key, "battle stat");
                Dictionary<string, JsonElement> fields = ReadObject(statElement, "operation", "value");
                StatusStatOperation operation = ReadEnum<StatusStatOperation>(fields.GetValueOrDefault("operation"), "stat operation");
                modifiers.Add(ReadContribution(descriptor, status, stat, operation, fields.GetValueOrDefault("value")));
            }
        }
        if (name is null && modifiers.Count == 0)
        {
            throw new InvalidDataException($"Status {status} requires a name or a stat modifier.");
        }
        return new(status, name, modifiers);
    }

    private static StatusContribution ReadContribution(NativeModDescriptor descriptor, NativeStatusId status, BattleStatId stat, StatusStatOperation operation, JsonElement value)
    {
        string key = $"{descriptor.Id}/{status}/{stat}";
        int min = StatusStatRules.MinimumPercent;
        int max = StatusStatRules.MaximumPercent;
        if (value.ValueKind != JsonValueKind.Object)
        {
            int fixedValue = ReadInteger(value, key);
            ValidateRange(key, fixedValue, min, max);
            return new(new(status, stat, operation, fixedValue), null);
        }

        Dictionary<string, JsonElement> fields = ReadObject(value, "default", "min", "max", "label", "description");
        if (fields.TryGetValue("min", out JsonElement minimum))
        {
            min = ReadInteger(minimum, key + " min");
        }
        if (fields.TryGetValue("max", out JsonElement maximum))
        {
            max = ReadInteger(maximum, key + " max");
        }
        int defaultValue = ReadInteger(fields.GetValueOrDefault("default"), key + " default");
        ValidateRange(key, defaultValue, min, max);
        IReadOnlyDictionary<string, string>? labels = fields.TryGetValue("label", out JsonElement label) ? NativeModDescriptor.ReadTranslations(label, "label") : null;
        IReadOnlyDictionary<string, string>? descriptions = fields.TryGetValue("description", out JsonElement description) ? NativeModDescriptor.ReadTranslations(description, "description", multiline: true) : null;
        BehaviorModSetting setting = new(SettingsBehavior, key, labels?["en"] ?? stat.ToString(), min, max, defaultValue, defaultValue)
        {
            Description = descriptions?["en"] ?? string.Empty,
            LabelTranslations = labels,
            DescriptionTranslations = descriptions,
            Stat = stat
        };
        return new(new(status, stat, operation, defaultValue), setting);
    }

    private static void ValidateRange(string key, int value, int min, int max)
    {
        if (min < StatusStatRules.MinimumPercent || max > StatusStatRules.MaximumPercent || min > max)
        {
            throw new InvalidDataException($"Status setting {key} requires a valid percentage range within {StatusStatRules.MinimumPercent} to {StatusStatRules.MaximumPercent}.");
        }
        if (value < min || value > max)
        {
            throw new InvalidDataException($"Status setting {key} must be an integer between {min} and {max}.");
        }
    }

    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Expected a status override object.");
        }
        Dictionary<string, JsonElement> fields = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if ((allowed.Length != 0 && !allowed.Contains(property.Name, StringComparer.Ordinal)) || !fields.TryAdd(property.Name, property.Value))
            {
                throw new InvalidDataException($"Unknown or duplicate status override property '{property.Name}'.");
            }
        }
        return fields;
    }

    private static int ReadInteger(JsonElement element, string field)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw new InvalidDataException($"Status setting {field} requires an integer.");
        }
        return value;
    }

    private static T ReadEnum<T>(JsonElement element, string field) where T : struct, Enum
    {
        return ReadEnum<T>(NativeModDescriptor.ReadText(element, field), field);
    }

    private static T ReadEnum<T>(string value, string field) where T : struct, Enum
    {
        if (!Enum.TryParse(value, false, out T parsed) || Enum.GetName(parsed) != value)
        {
            throw new InvalidDataException($"Unknown {field} '{value}'.");
        }
        return parsed;
    }

    internal static bool IsDocumentError(Exception exception)
    {
        return exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException;
    }

    private sealed record StatusDocument(NativeStatusId Status, LocalizedTextReference? Name, IReadOnlyList<StatusContribution> Contributions);
    private sealed record StatusContribution(StatusStatModifier Modifier, BehaviorModSetting? Setting);
}
