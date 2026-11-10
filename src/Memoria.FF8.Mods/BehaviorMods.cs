using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed record BehaviorModSetting(string Behavior, string Parameter, string Label, int Min, int Max, int DefaultValue, int Value);

/// <summary>Known, declarative gameplay parameters selected by the active packages.</summary>
public sealed class BehaviorMods
{
    public const string PayloadPath = "gameplay/behaviors.json";
    public const string ConfigurationPath = "config.json";
    public static BehaviorMods Empty { get; } = new(null);

    private BehaviorMods(int? meltdownDefenseReductionPercent, string? meltdownPackageId = null)
    {
        MeltdownDefenseReductionPercent = meltdownDefenseReductionPercent;
        MeltdownPackageId = meltdownPackageId;
    }

    public int? MeltdownDefenseReductionPercent { get; }
    public string? MeltdownPackageId { get; }

    public static bool IsPayloadPath(string relativePath)
    {
        return relativePath.Equals(PayloadPath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out BehaviorMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<ModDiagnostic> errors = [];
        ModFile? owner = null;
        int? selected = null;
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
                IReadOnlyList<BehaviorModSetting> settings = ReadSettings(payload.ReadBytes(), configurations.SingleOrDefault()?.ReadBytes());
                foreach (BehaviorModSetting setting in settings)
                {
                    if (owner is not null)
                    {
                        string message = $"Behavior parameter {setting.Behavior}.{setting.Parameter} is declared by both {owner.PackageId}:{owner.RelativePath} and {payload.PackageId}:{payload.RelativePath}.";
                        errors.Add(new("Memoria.BehaviorConflict", message, owner.PackageId, owner.RelativePath));
                        errors.Add(new("Memoria.BehaviorConflict", message, payload.PackageId, payload.RelativePath));
                        continue;
                    }
                    owner = payload;
                    selected = setting.Value;
                }
            }
            catch (Exception exception) when (IsDocumentError(exception))
            {
                errors.Add(new("Memoria.InvalidBehaviorMod", exception.Message, package.Key, payload?.RelativePath ?? ConfigurationPath));
            }
        }
        result = errors.Count == 0 ? new(selected, owner?.PackageId) : Empty;
        diagnostics = errors.AsReadOnly();
        return errors.Count == 0;
    }

    internal static IReadOnlyList<BehaviorModSetting> ReadSettings(byte[] payload, byte[]? configuration)
    {
        int? declared = Parse(payload, true);
        int? configured = configuration is null ? null : Parse(configuration, false);
        if (configured is not null && declared is null)
        {
            throw new InvalidDataException("Configuration overrides undeclared behavior parameter meltdown.defenseReductionPercent.");
        }
        return declared is int value ? [new("meltdown", "defenseReductionPercent", "Defense reduction (%)", 0, 100, value, configured ?? value)] : [];
    }

    internal static bool IsDocumentError(Exception exception)
    {
        return exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException;
    }

    private static int? Parse(byte[] bytes, bool payload)
    {
        string json = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        ValidateObject(root, payload ? ["schemaVersion", "meltdown"] : ["meltdown"]);
        if (payload && (!root.TryGetProperty("schemaVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int schemaVersion) || schemaVersion != 1))
        {
            throw new InvalidDataException("Behavior schemaVersion must be the integer 1.");
        }
        if (!root.TryGetProperty("meltdown", out JsonElement meltdown))
        {
            return null;
        }
        ValidateObject(meltdown, ["defenseReductionPercent"]);
        if (!meltdown.TryGetProperty("defenseReductionPercent", out JsonElement percent) || percent.ValueKind != JsonValueKind.Number || !percent.TryGetInt32(out int value) || value is < 0 or > 100)
        {
            throw new InvalidDataException("meltdown.defenseReductionPercent must be an integer from 0 to 100.");
        }
        return value;
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
