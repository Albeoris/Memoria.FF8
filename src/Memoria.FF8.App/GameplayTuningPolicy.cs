using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Composition of reviewed package rules in catalog priority order.</summary>
internal sealed class GameplayTuningPolicy
{
    private readonly Dictionary<JunctionGameplaySetting, (decimal Value, string Owner)> settings = [];
    private (int Value, string Owner)? meltdown;
    public static GameplayTuningPolicy Empty { get; } = new();

    public static GameplayTuningPolicy FromCatalog(ModCatalogSnapshot catalog, BehaviorMods? behaviorMods = null)
    {
        if (behaviorMods is null && !BehaviorMods.TryPrepare(catalog, out behaviorMods, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        GameplayTuningPolicy policy = new();
        if (behaviorMods.MeltdownDefenseReductionPercent is int reduction)
        {
            ModPackage owner = catalog.Packages.Single(package => package.Id.Equals(behaviorMods.MeltdownPackageId, StringComparison.OrdinalIgnoreCase));
            policy.meltdown = (reduction, owner.Name);
        }
        foreach (ModPackage package in catalog.Packages.Where(package => package.Format == ModFormat.Junction && package.Status == ModEffectiveStatus.Active))
        {
            foreach (JunctionGameplayOverride setting in package.JunctionMetadata?.GameplayOverrides ?? [])
            {
                policy.settings.TryAdd(setting.Setting, (setting.Value, package.Name));
            }
        }
        policy.Apply(new GameplayTuning()).Validate();
        return policy;
    }

    public GameplayTuning Apply(GameplayTuning configured)
    {
        GameplayTuning result = configured;
        foreach (var (setting, entry) in settings)
        {
            decimal value = entry.Value;
            result = setting switch
            {
                JunctionGameplaySetting.DrawBonus => result with { DrawBonus = Integer(value) },
                JunctionGameplaySetting.ApMultiplier => result with { ApMultiplier = Integer(value) },
                JunctionGameplaySetting.ExperienceMultiplier => result with { ExperienceMultiplier = Integer(value) },
                JunctionGameplaySetting.GuardianExperienceMultiplier => result with { GuardianExperienceMultiplier = Integer(value) },
                JunctionGameplaySetting.UnlimitedMagic => result with { UnlimitedMagic = Boolean(value) },
                JunctionGameplaySetting.MaximumDamage => result with { MaximumDamage = Integer(value) },
                JunctionGameplaySetting.UnrestrictedScanRotation => result with { UnrestrictedScanRotation = Boolean(value) },
                JunctionGameplaySetting.RareItemFirst => result with { RareItem = result.RareItem with { First = value } },
                JunctionGameplaySetting.RareItemSecond => result with { RareItem = result.RareItem with { Second = value } },
                JunctionGameplaySetting.RareItemThird => result with { RareItem = result.RareItem with { Third = value } },
                JunctionGameplaySetting.RareItemFourth => result with { RareItem = result.RareItem with { Fourth = value } },
                _ => throw new NotSupportedException($"Unsupported gameplay setting {setting}.")
            };
        }
        if (meltdown is { } behavior)
        {
            result = result with { MeltdownDefenseReductionPercent = behavior.Value };
        }
        result.Validate();
        return result;
    }

    public string? UnavailableReason(string field)
    {
        if (field == nameof(GameplayTuning.MeltdownDefenseReductionPercent) && meltdown is { } behavior)
        {
            return $"Controlled by {behavior.Owner}. Change its mod option to release this rule.";
        }
        foreach (var (setting, entry) in settings)
        {
            if (Field(setting) == field)
            {
                return $"Controlled by {entry.Owner}. Change its mod option to release this rule.";
            }
        }
        return null;
    }

    public GameplayTuning EditConfigured(GameplayTuning previous, GameplayTuning submitted)
    {
        submitted.Validate();
        GameplayTuning effective = Apply(previous);
        foreach (string field in ControlledFields)
        {
            System.Reflection.PropertyInfo property = typeof(GameplayTuning).GetProperty(field)!;
            if (!Equals(property.GetValue(effective), property.GetValue(submitted)))
            {
                throw new InvalidOperationException(UnavailableReason(field));
            }
        }
        GameplayTuning configured = submitted with { };
        foreach (string field in ControlledFields)
        {
            System.Reflection.PropertyInfo property = typeof(GameplayTuning).GetProperty(field)!;
            property.SetValue(configured, property.GetValue(previous));
        }
        configured.Validate();
        return configured;
    }

    private IEnumerable<string> ControlledFields => settings.Keys.Select(Field).Concat(meltdown is null ? [] : new[] { nameof(GameplayTuning.MeltdownDefenseReductionPercent) }).Distinct();

    private static string Field(JunctionGameplaySetting setting) => setting switch
    {
        JunctionGameplaySetting.RareItemFirst or JunctionGameplaySetting.RareItemSecond or JunctionGameplaySetting.RareItemThird or JunctionGameplaySetting.RareItemFourth => nameof(GameplayTuning.RareItem),
        _ => setting.ToString()
    };

    private static int Integer(decimal value) => value == decimal.Truncate(value) ? checked((int)value) : throw new InvalidDataException("An integer gameplay rule requires a whole number.");
    private static bool Boolean(decimal value) => value is 0 or 1 ? value == 1 : throw new InvalidDataException("A boolean gameplay rule requires zero or one.");
}
