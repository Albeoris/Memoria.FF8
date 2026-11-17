using System.Collections.Immutable;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Composition of reviewed package rules in catalog priority order.</summary>
internal sealed class GameplayTuningPolicy
{
    internal const string LevelBasedStatBonusesPackageId = "Memoria/Level-Based Stat Bonuses";
    private bool hardcoreStatBonuses;
    internal const string ExtendedRosterPackageId = "Memoria/Extended FFVIII Roster";
    private readonly Dictionary<JunctionGameplaySetting, (decimal Value, string Owner)> settings = [];
    public static GameplayTuningPolicy Empty { get; } = new();
    public bool ExtendedRosterEnabled { get; private init; }
    public bool LevelBasedStatBonusesEnabled { get; private init; }
    public ImmutableArray<StatusStatModifier> StatusStatModifiers { get; private init; } = [];

    public static GameplayTuningPolicy FromCatalog(ModCatalogSnapshot catalog, StatusMods? statusMods = null)
    {
        if (statusMods is null && !StatusMods.TryPrepare(catalog, out statusMods, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        GameplayTuningPolicy policy = new()
        {
            StatusStatModifiers = statusMods.Modifiers,
            ExtendedRosterEnabled = catalog.Packages.Any(package => package.IsBundled && package.Status == ModEffectiveStatus.Active && package.Id.Equals(ExtendedRosterPackageId, StringComparison.OrdinalIgnoreCase)),
            LevelBasedStatBonusesEnabled = catalog.Packages.Any(package => package.IsBundled && package.Status == ModEffectiveStatus.Active && package.Id.Equals(LevelBasedStatBonusesPackageId, StringComparison.OrdinalIgnoreCase))
        };
        policy.hardcoreStatBonuses = catalog.Hardcore && policy.LevelBasedStatBonusesEnabled;
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
        GameplayTuning result = configured with { LevelBasedStatBonuses = LevelBasedStatBonusesEnabled };
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
        result.Validate();
        return result;
    }

    public string? UnavailableReason(string field)
    {
        if (field == nameof(GameplayTuning.LevelBasedStatBonuses) && LevelBasedStatBonusesEnabled)
        {
            return hardcoreStatBonuses ? ModRuntime.HardcoreStatBonusesLockReason : "Controlled by Level-Based Stat Bonuses. Disable the mod to release this rule.";
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

    private IEnumerable<string> ControlledFields => settings.Keys.Select(Field).Concat(LevelBasedStatBonusesEnabled ? [nameof(GameplayTuning.LevelBasedStatBonuses)] : []).Distinct();

    private static string Field(JunctionGameplaySetting setting) => setting switch
    {
        JunctionGameplaySetting.RareItemFirst or JunctionGameplaySetting.RareItemSecond or JunctionGameplaySetting.RareItemThird or JunctionGameplaySetting.RareItemFourth => nameof(GameplayTuning.RareItem),
        _ => setting.ToString()
    };

    private static int Integer(decimal value) => value == decimal.Truncate(value) ? checked((int)value) : throw new InvalidDataException("An integer gameplay rule requires a whole number.");
    private static bool Boolean(decimal value) => value is 0 or 1 ? value == 1 : throw new InvalidDataException("A boolean gameplay rule requires zero or one.");
}
