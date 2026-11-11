using System.Text;
using System.Text.Json;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods;

public sealed record BehaviorModSetting(string Behavior, string Parameter, string Label, int Min, int Max, int DefaultValue, int Value)
{
    public string Description { get; init; } = string.Empty;
    public string Control { get; init; } = "slider";
    public string Unit { get; init; } = "%";
    public int Step { get; init; } = 1;
}

/// <summary>Known, declarative gameplay parameters selected by the active packages.</summary>
public sealed class BehaviorMods
{
    public const string PayloadPath = "gameplay/behaviors.json";
    public const string ConfigurationPath = "config.json";
    public const int HardcoreVitalityReductionPercent = 50;
    public const int HardcoreSpiritReductionPercent = 25;
    public const string VitalityReductionParameter = "VitalityReductionPercent";
    public const string SpiritReductionParameter = "SpiritReductionPercent";
    public const string StatusNameParameter = "StatusName";
    public const string SourceLanguageStatusNameParameter = "StatusName_{LANG}";
    private const string LegacyReductionParameter = "defenseReductionPercent";
    private static readonly string[] Parameters = [VitalityReductionParameter, SpiritReductionParameter];
    public static BehaviorMods Empty { get; } = new(null, null, null, null, null, null);

    private BehaviorMods(int? vitality, int? spirit, string? vitalityPackageId, string? spiritPackageId, string? statusNameKey, string? statusNamePackageId)
    {
        MeltdownVitalityReductionPercent = vitality;
        MeltdownSpiritReductionPercent = spirit;
        MeltdownVitalityPackageId = vitalityPackageId;
        MeltdownSpiritPackageId = spiritPackageId;
        MeltdownStatusNameKey = statusNameKey;
        MeltdownStatusNamePackageId = statusNamePackageId;
    }

    public int? MeltdownVitalityReductionPercent { get; }
    public int? MeltdownSpiritReductionPercent { get; }
    public string? MeltdownVitalityPackageId { get; }
    public string? MeltdownSpiritPackageId { get; }
    public string? MeltdownStatusNameKey { get; }
    public string? MeltdownStatusNamePackageId { get; }
    public string? MeltdownPackageId => MeltdownVitalityPackageId == MeltdownSpiritPackageId ? MeltdownVitalityPackageId : null;

    public static bool IsPayloadPath(string relativePath)
    {
        return relativePath.Equals(PayloadPath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out BehaviorMods result, out IReadOnlyList<ModDiagnostic> diagnostics, string sourceLanguage = "en")
    {
        sourceLanguage = LocalizedTextReference.CanonicalLanguage(sourceLanguage);
        List<ModDiagnostic> errors = [];
        Dictionary<string, (ModFile Owner, int Value)> selected = new(StringComparer.Ordinal);
        (ModFile Owner, string Key)? statusName = null;
        HashSet<string> inactivePackages = catalog.Packages.Where(package => package.Status != ModEffectiveStatus.Active).Select(package => package.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, ModFile> package in catalog.Files.Where(file => file.Format == ModFormat.Memoria && !inactivePackages.Contains(file.PackageId) && (IsPayloadPath(file.RelativePath) || file.RelativePath.Equals(ConfigurationPath, StringComparison.OrdinalIgnoreCase))).GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            ModFile? payload = null;
            try
            {
                ModFile[] declarations = package.Where(file => IsPayloadPath(file.RelativePath)).ToArray();
                if (declarations.Length == 0)
                {
                    continue;
                }
                ModFile[] configurations = package.Where(file => file.RelativePath.Equals(ConfigurationPath, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (declarations.Length > 1 || configurations.Length > 1)
                {
                    throw new InvalidDataException("A package must have a single behavior payload and a single configuration document.");
                }
                payload = declarations.Single();
                List<BehaviorModSetting> settings = ParsePayload(payload.ReadBytes(), out Dictionary<string, LocalizedTextReference> statusNames);
                string? statusNameKey = SelectStatusName(statusNames, sourceLanguage);
                ApplyConfiguration(settings, configurations.SingleOrDefault()?.ReadBytes());
                if (statusNameKey is not null)
                {
                    if (statusName is { } existingName)
                    {
                        string message = $"Behavior parameter meltdown.{StatusNameParameter} is declared by both {existingName.Owner.PackageId}:{existingName.Owner.RelativePath} and {payload.PackageId}:{payload.RelativePath}.";
                        errors.Add(new("Memoria.BehaviorConflict", message, existingName.Owner.PackageId, existingName.Owner.RelativePath));
                        errors.Add(new("Memoria.BehaviorConflict", message, payload.PackageId, payload.RelativePath));
                    }
                    else
                    {
                        statusName = (payload, statusNameKey);
                    }
                }
                foreach (BehaviorModSetting setting in settings)
                {
                    if (selected.TryGetValue(setting.Parameter, out var existing))
                    {
                        string message = $"Behavior parameter {setting.Behavior}.{setting.Parameter} is declared by both {existing.Owner.PackageId}:{existing.Owner.RelativePath} and {payload.PackageId}:{payload.RelativePath}.";
                        errors.Add(new("Memoria.BehaviorConflict", message, existing.Owner.PackageId, existing.Owner.RelativePath));
                        errors.Add(new("Memoria.BehaviorConflict", message, payload.PackageId, payload.RelativePath));
                        continue;
                    }
                    selected.Add(setting.Parameter, (payload, setting.Value));
                }
            }
            catch (Exception exception) when (IsDocumentError(exception))
            {
                errors.Add(new("Memoria.InvalidBehaviorMod", exception.Message, package.Key, payload?.RelativePath ?? ConfigurationPath));
            }
        }
        selected.TryGetValue(VitalityReductionParameter, out var vitality);
        selected.TryGetValue(SpiritReductionParameter, out var spirit);
        result = errors.Count == 0 ? new(vitality.Owner is null ? null : vitality.Value, spirit.Owner is null ? null : spirit.Value, vitality.Owner?.PackageId, spirit.Owner?.PackageId, statusName?.Key, statusName?.Owner.PackageId) : Empty;
        diagnostics = errors.AsReadOnly();
        return errors.Count == 0;
    }

    internal static IReadOnlyList<BehaviorModSetting> ReadSettings(byte[] payload, byte[]? configuration)
    {
        List<BehaviorModSetting> settings = ParsePayload(payload, out _);
        ApplyConfiguration(settings, configuration);
        return settings;
    }

    private static void ApplyConfiguration(List<BehaviorModSetting> settings, byte[]? configuration)
    {
        Dictionary<string, int> configured = configuration is null ? new(StringComparer.Ordinal) : ParseConfiguration(configuration);
        foreach ((string parameter, int value) in configured)
        {
            int index = settings.FindIndex(setting => setting.Parameter == parameter);
            if (index < 0)
            {
                throw new InvalidDataException($"Configuration overrides undeclared behavior parameter meltdown.{parameter}.");
            }
            BehaviorModSetting setting = settings[index];
            ValidateValue(value, setting.Min, setting.Max, setting.Step, parameter);
            settings[index] = setting with { Value = value };
        }
    }

    internal static bool IsDocumentError(Exception exception)
    {
        return exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException;
    }

    private static JsonDocument ParseDocument(byte[] bytes)
    {
        string json = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        return JsonDocument.Parse(json);
    }

    private static List<BehaviorModSetting> ParsePayload(byte[] bytes, out Dictionary<string, LocalizedTextReference> statusNames)
    {
        statusNames = new(StringComparer.Ordinal);
        using JsonDocument document = ParseDocument(bytes);
        JsonElement root = document.RootElement;
        ValidateObject(root, ["schemaVersion", "meltdown"]);
        int version = ReadInteger(root, "schemaVersion");
        if (version is not (1 or 2))
        {
            throw new InvalidDataException("Behavior schemaVersion must be the integer 1 or 2.");
        }
        if (!root.TryGetProperty("meltdown", out JsonElement meltdown))
        {
            return [];
        }
        if (version == 1)
        {
            ValidateObject(meltdown, [LegacyReductionParameter]);
            int value = ReadInteger(meltdown, LegacyReductionParameter);
            ValidateValue(value, 0, 100, 1, LegacyReductionParameter);
            return Parameters.Select(parameter => new BehaviorModSetting("meltdown", parameter, parameter == VitalityReductionParameter ? "Vitality reduction" : "Spirit reduction", 0, 100, value, value)
            {
                Description = parameter == VitalityReductionParameter ? "Percentage by which Meltdown reduces Vitality." : "Percentage by which Meltdown reduces Spirit."
            }).ToList();
        }
        if (meltdown.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Behavior documents and behavior parameters must be JSON objects.");
        }
        string[] statusNameProperties = meltdown.EnumerateObject().Where(property => property.Name == StatusNameParameter || property.Name.StartsWith(StatusNameParameter + "_", StringComparison.Ordinal)).Select(property => property.Name).ToArray();
        ValidateObject(meltdown, [.. Parameters, .. statusNameProperties]);
        foreach (string property in statusNameProperties)
        {
            string binding = property == StatusNameParameter ? string.Empty : property == SourceLanguageStatusNameParameter ? "{LANG}" : LocalizedTextReference.CanonicalLanguage(property[(StatusNameParameter.Length + 1)..]);
            LocalizedTextReference reference = LocalizedTextReference.Parse(ReadText(meltdown, property));
            if (binding.Length != 0 && binding != "{LANG}" && !reference.UsesSourceLanguage && !reference.Key.StartsWith("$" + binding + "_", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"meltdown.{property} must reference its declared source language {binding}.");
            }
            if (!statusNames.TryAdd(binding, reference))
            {
                throw new InvalidDataException($"Duplicate behavior language binding: meltdown.{property}.");
            }
        }
        List<BehaviorModSetting> settings = [];
        foreach (JsonProperty property in meltdown.EnumerateObject())
        {
            if (statusNameProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                continue;
            }
            JsonElement metadata = property.Value;
            ValidateObject(metadata, ["label", "description", "control", "unit", "min", "max", "step", "default"]);
            string label = ReadText(metadata, "label");
            string description = ReadText(metadata, "description");
            string control = ReadText(metadata, "control");
            string unit = ReadText(metadata, "unit");
            if (unit != "%")
            {
                throw new InvalidDataException("Meltdown percentage settings require the % unit.");
            }
            if (control != "slider")
            {
                throw new InvalidDataException($"Unsupported behavior control: {control}.");
            }
            int min = ReadInteger(metadata, "min");
            int max = ReadInteger(metadata, "max");
            int step = ReadInteger(metadata, "step");
            int defaultValue = ReadInteger(metadata, "default");
            if (min < 0 || max > 100 || min > max || step < 1 || step > 100 || (max - min) % step != 0)
            {
                throw new InvalidDataException($"meltdown.{property.Name} requires percentage limits within 0 to 100 and a positive step dividing its range.");
            }
            ValidateValue(defaultValue, min, max, step, property.Name);
            settings.Add(new("meltdown", property.Name, label, min, max, defaultValue, defaultValue) { Description = description, Control = control, Unit = unit, Step = step });
        }
        return settings;
    }

    private static string? SelectStatusName(Dictionary<string, LocalizedTextReference> statusNames, string sourceLanguage)
    {
        if (statusNames.TryGetValue(sourceLanguage, out LocalizedTextReference? reference) || statusNames.TryGetValue("{LANG}", out reference) || statusNames.TryGetValue(string.Empty, out reference))
        {
            return reference.ResolveKey(sourceLanguage);
        }
        return null;
    }

    private static Dictionary<string, int> ParseConfiguration(byte[] bytes)
    {
        using JsonDocument document = ParseDocument(bytes);
        JsonElement root = document.RootElement;
        ValidateObject(root, ["meltdown"]);
        Dictionary<string, int> values = new(StringComparer.Ordinal);
        if (!root.TryGetProperty("meltdown", out JsonElement meltdown))
        {
            return values;
        }
        ValidateObject(meltdown, [VitalityReductionParameter, SpiritReductionParameter, LegacyReductionParameter]);
        if (meltdown.TryGetProperty(LegacyReductionParameter, out _))
        {
            if (meltdown.EnumerateObject().Count() != 1)
            {
                throw new InvalidDataException("Legacy Meltdown configuration cannot be combined with separate parameters.");
            }
            int value = ReadInteger(meltdown, LegacyReductionParameter);
            ValidateValue(value, 0, 100, 1, LegacyReductionParameter);
            foreach (string parameter in Parameters)
            {
                values.Add(parameter, value);
            }
            return values;
        }
        foreach (JsonProperty property in meltdown.EnumerateObject())
        {
            int value = ReadInteger(meltdown, property.Name);
            ValidateValue(value, 0, 100, 1, property.Name);
            values.Add(property.Name, value);
        }
        return values;
    }

    private static void ValidateValue(int value, int min, int max, int step, string parameter)
    {
        if (value < min || value > max || (value - min) % step != 0)
        {
            throw new InvalidDataException($"meltdown.{parameter} must be an integer from {min} to {max} in steps of {step}.");
        }
    }

    private static int ReadInteger(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out int value))
        {
            throw new InvalidDataException($"Behavior field {name} must be an integer.");
        }
        return value;
    }

    private static string ReadText(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException($"Behavior field {name} must be nonempty text.");
        }
        return property.GetString()!;
    }

    private static void ValidateObject(JsonElement element, string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Behavior documents and behavior parameters must be JSON objects.");
        }
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException($"Duplicate behavior field: {property.Name}.");
            }
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Unknown behavior field: {property.Name}.");
            }
        }
    }
}
