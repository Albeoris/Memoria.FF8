using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Mods;

/// <summary>Gameplay rules contributed by the active native mod packages.</summary>
public sealed class GuardianForceRules
{
    public const string PayloadPath = "gameplay/gf-rules.json";
    private const int MaximumDocumentBytes = 128 * 1024;
    private const int MaximumAbilityId = 115;
    private const string InvalidRule = "Memoria.InvalidGuardianForceRules";

    public static GuardianForceRules Empty { get; } = new(null, ImmutableDictionary<int, byte>.Empty, ImmutableDictionary<int, IReadOnlyDictionary<string, string>>.Empty, ImmutableDictionary<int, int>.Empty, []);

    private readonly ImmutableDictionary<int, IReadOnlyDictionary<string, string>> descriptions;

    private GuardianForceRules(int? defaultSummonDamageLimit, ImmutableDictionary<int, byte> abilityPointCosts, ImmutableDictionary<int, IReadOnlyDictionary<string, string>> descriptions, ImmutableDictionary<int, int> summonDamageLimits, ImmutableArray<string> packageIds)
    {
        DefaultSummonDamageLimit = defaultSummonDamageLimit;
        AbilityPointCosts = abilityPointCosts;
        this.descriptions = descriptions;
        SummonDamageLimits = summonDamageLimits;
        PackageIds = packageIds;
    }

    public int? DefaultSummonDamageLimit { get; }
    public ImmutableDictionary<int, byte> AbilityPointCosts { get; }
    public IReadOnlyDictionary<int, int> SummonDamageLimits { get; }
    internal ImmutableArray<string> PackageIds { get; }

    public string? Description(int id, string? language)
    {
        return descriptions.TryGetValue(id, out var translations) ? NativeModDescriptor.TextForLanguage(translations, language) : null;
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out GuardianForceRules result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        if (catalog.PreparedGuardianForceRules is { } prepared)
        {
            result = prepared;
            diagnostics = [];
            return true;
        }

        var packages = catalog.Packages.Where(package => package is { Format: ModFormat.Memoria, Status: ModEffectiveStatus.Active }).ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        List<ModDiagnostic> errors = [];
        foreach (ModDiagnostic notice in catalog.Diagnostics)
        {
            if (notice.PackageId is { } packageId && packages.ContainsKey(packageId) && notice.RelativePath is { } path
                && path.Equals(PayloadPath, StringComparison.OrdinalIgnoreCase) && notice.Code is "normalized-path-conflict" or "escaping-link-rejected")
            {
                errors.Add(new(InvalidRule, notice.Message, packageId, path));
            }
        }

        int? defaultLimit = null;
        ImmutableDictionary<int, byte>.Builder costs = ImmutableDictionary.CreateBuilder<int, byte>();
        ImmutableDictionary<int, IReadOnlyDictionary<string, string>>.Builder descriptions = ImmutableDictionary.CreateBuilder<int, IReadOnlyDictionary<string, string>>();
        ImmutableDictionary<int, int>.Builder limits = ImmutableDictionary.CreateBuilder<int, int>();
        ImmutableArray<string>.Builder packageIds = ImmutableArray.CreateBuilder<string>();
        Dictionary<(int? Ability, string Property), ModFile> owners = [];

        bool Claim(ModFile file, int? ability, string property)
        {
            if (owners.TryAdd((ability, property), file))
            {
                return true;
            }
            ModFile owner = owners[(ability, property)];
            string target = ability is { } id ? $"ability {id} property '{property}'" : $"property '{property}'";
            errors.Add(new("Memoria.ConflictingGuardianForceRules", $"GF rules assign {target} in both '{owner.PackageId}/{owner.RelativePath}' and '{file.PackageId}/{file.RelativePath}'.", file.PackageId, file.RelativePath));
            return false;
        }

        foreach (ModFile file in catalog.FindAll(ModFormat.Memoria, PayloadPath))
        {
            if (!packages.TryGetValue(file.PackageId, out ModPackage? package))
            {
                continue;
            }
            try
            {
                RuleDocument document = Read(package, file);
                if (document.DefaultLimit is { } limit && Claim(file, null, "defaultSummonDamageLimit"))
                {
                    defaultLimit = limit;
                }
                foreach (AbilityRule rule in document.Abilities)
                {
                    if (rule.Cost is { } cost && Claim(file, rule.Id, "apCost"))
                    {
                        costs.Add(rule.Id, cost);
                    }
                    if (rule.Description is { } description && Claim(file, rule.Id, "description"))
                    {
                        descriptions.Add(rule.Id, description);
                    }
                    if (rule.SummonDamageLimit is { } abilityLimit && Claim(file, rule.Id, "summonDamageLimit"))
                    {
                        limits.Add(rule.Id, abilityLimit);
                    }
                }
                packageIds.Add(package.Id);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                errors.Add(new(InvalidRule, exception.Message, file.PackageId, file.RelativePath));
            }
        }

        diagnostics = errors.AsReadOnly();
        result = errors.Count == 0 && packageIds.Count != 0 ? new(defaultLimit, costs.ToImmutable(), descriptions.ToImmutable(), limits.ToImmutable(), packageIds.ToImmutable()) : Empty;
        return errors.Count == 0;
    }

    private static RuleDocument Read(ModPackage package, ModFile file)
    {
        if (package.Descriptor is null)
        {
            throw new InvalidDataException("GF rules require a native mod descriptor.");
        }
        if (file.AbsolutePath is { } expected && ModResourcePath.GetAbsolutePath(package.RootDirectory, NativeModDescriptor.FileName, file.RelativePath) != expected)
        {
            throw new InvalidDataException("The GF rules document must be inside its mod directory.");
        }
        if (file.ContentStamp is { Length: > MaximumDocumentBytes })
        {
            throw new InvalidDataException("The GF rules document exceeds its size limit.");
        }
        byte[] content = file.ReadBytes();
        if (content.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The GF rules document exceeds its size limit.");
        }

        using JsonDocument json = JsonDocument.Parse(content);
        Dictionary<string, JsonElement> properties = ReadObject(json.RootElement, "schemaVersion", "defaultSummonDamageLimit", "abilities");
        JsonElement version = properties.GetValueOrDefault("schemaVersion");
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int schemaVersion) || schemaVersion != 1)
        {
            throw new InvalidDataException("GF rules require schemaVersion 1.");
        }
        int? defaultLimit = properties.TryGetValue("defaultSummonDamageLimit", out JsonElement defaultValue) ? ReadLimit(defaultValue, "defaultSummonDamageLimit") : null;
        List<AbilityRule> abilities = [];
        if (properties.TryGetValue("abilities", out JsonElement array))
        {
            if (array.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("GF rules 'abilities' must be an array.");
            }
            HashSet<int> ids = [];
            foreach (JsonElement element in array.EnumerateArray())
            {
                Dictionary<string, JsonElement> fields = ReadObject(element, "id", "apCost", "description", "summonDamageLimit");
                int id = ReadInteger(fields.GetValueOrDefault("id"), "id", 1, MaximumAbilityId);
                if (!ids.Add(id))
                {
                    throw new InvalidDataException($"Duplicate GF ability ID {id}.");
                }
                byte? cost = fields.TryGetValue("apCost", out JsonElement costValue) ? (byte)ReadInteger(costValue, $"ability {id} apCost", 1, byte.MaxValue) : null;
                IReadOnlyDictionary<string, string>? description = fields.TryGetValue("description", out JsonElement descriptionValue) ? NativeModDescriptor.ReadTranslations(descriptionValue, $"ability {id} description", multiline: true) : null;
                int? limit = fields.TryGetValue("summonDamageLimit", out JsonElement limitValue) ? ReadLimit(limitValue, $"ability {id} summonDamageLimit") : null;
                if (cost is null && description is null && limit is null)
                {
                    throw new InvalidDataException($"GF ability {id} requires a rule property.");
                }
                abilities.Add(new(id, cost, description, limit));
            }
        }
        if (defaultLimit is null && abilities.Count == 0)
        {
            throw new InvalidDataException("The GF rules document requires a default damage limit or at least one ability rule.");
        }
        return new(defaultLimit, abilities);
    }

    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A GF rules document and each ability entry must be objects.");
        }
        Dictionary<string, JsonElement> fields = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !fields.TryAdd(property.Name, property.Value))
            {
                throw new InvalidDataException($"Unknown or duplicate GF rules property '{property.Name}'.");
            }
        }
        return fields;
    }

    private static int ReadInteger(JsonElement element, string field, int minimum, int maximum)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value) || value < minimum || value > maximum)
        {
            throw new InvalidDataException($"GF rules '{field}' requires an integer from {minimum} to {maximum}.");
        }
        return value;
    }

    private static int ReadLimit(JsonElement element, string field)
    {
        if (element.ValueKind == JsonValueKind.String && element.ValueEquals("unlimited"))
        {
            return int.MaxValue;
        }
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int limit) || limit <= 0)
        {
            throw new InvalidDataException($"GF rules '{field}' requires 'unlimited' or an integer from 1 to {int.MaxValue}.");
        }
        return limit;
    }

    private sealed record RuleDocument(int? DefaultLimit, IReadOnlyList<AbilityRule> Abilities);
    private sealed record AbilityRule(int Id, byte? Cost, IReadOnlyDictionary<string, string>? Description, int? SummonDamageLimit);
}
