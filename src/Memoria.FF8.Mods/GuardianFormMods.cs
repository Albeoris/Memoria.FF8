using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public sealed record GuardianFormModVariant(BattleGuardianFormDefinition Profile, ImmutableArray<BattleGuardianFormActionBinding> Actions);

public sealed record GuardianFormModDocument(int Version, ImmutableArray<GuardianFormModVariant> Variants);

public sealed class GuardianFormMods
{
    public const string PayloadPath = "gameplay/gf-forms.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static GuardianFormMods Empty { get; } = new([], []);

    private GuardianFormMods(ImmutableArray<BattleGuardianFormResources> resources, ImmutableArray<string> packageIds)
    {
        Resources = resources;
        PackageIds = packageIds;
    }

    public ImmutableArray<BattleGuardianFormResources> Resources { get; }
    public ImmutableArray<string> PackageIds { get; }

    public bool SupportsGuardianForce(int guardianForceId)
    {
        return Resources.Any(resources => resources.GuardianForceId == guardianForceId);
    }

    internal static bool IsGuardianFormContentOnly(IEnumerable<string> contributions)
    {
        HashSet<string> paths = new(contributions, StringComparer.OrdinalIgnoreCase);
        paths.ExceptWith([NativeModDescriptor.FileName, "mod.ini", "README.md", "LICENSE", "LICENSE.md"]);
        return paths.SetEquals([PayloadPath]);
    }

    public static GuardianFormModDocument Parse(byte[] bytes)
    {
        GuardianFormModDocument document = JsonSerializer.Deserialize<GuardianFormModDocument>(bytes, JsonOptions) ?? throw new InvalidDataException("A GF form payload must be a document object.");
        if (document.Version != 1 || document.Variants.IsDefaultOrEmpty || document.Variants.Any(variant => variant is null))
        {
            throw new InvalidDataException("A version 1 GF form payload must contain variants.");
        }
        HashSet<string> variantIds = new(StringComparer.Ordinal);
        foreach (GuardianFormModVariant variant in document.Variants)
        {
            ValidateVariant(variant);
            if (!variantIds.Add(variant.Profile.VariantId))
            {
                throw new InvalidDataException($"Duplicate GF form variant '{variant.Profile.VariantId}'.");
            }
        }
        return document;
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, Func<BattleAssets> openAssets, out GuardianFormMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<(ModFile File, GuardianFormModVariant Variant)> candidates = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile file in catalog.FindAll(ModFormat.Memoria, PayloadPath))
        {
            try
            {
                candidates.AddRange(Parse(file.ReadBytes()).Variants.Select(variant => (file, variant)));
            }
            catch (Exception exception) when (IsPayloadError(exception))
            {
                errors.Add(new("Memoria.InvalidGuardianForm", exception.Message, file.PackageId, PayloadPath));
            }
        }
        HashSet<string> duplicateIds = candidates.GroupBy(candidate => candidate.Variant.Profile.VariantId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        HashSet<int> invalidGroups = candidates.GroupBy(candidate => candidate.Variant.Profile.GuardianForceId)
            .Where(group => group.Count() > 256 || group.Select(candidate => candidate.Variant.Profile.ProfileId).Distinct(StringComparer.Ordinal).Skip(1).Any())
            .Select(group => group.Key).ToHashSet();
        Dictionary<string, BattleGuardianFormDefinition> profiles = candidates.Where(candidate => !duplicateIds.Contains(candidate.Variant.Profile.VariantId))
            .ToDictionary(candidate => candidate.Variant.Profile.VariantId, candidate => candidate.Variant.Profile, StringComparer.Ordinal);
        List<BattleGuardianFormResources> resources = [];
        BattleAssets? assets = null;
        try
        {
            foreach ((ModFile file, GuardianFormModVariant variant) in candidates)
            {
                BattleGuardianFormDefinition profile = variant.Profile;
                try
                {
                    if (duplicateIds.Contains(profile.VariantId))
                    {
                        throw new InvalidDataException($"Multiple packages define GF form variant '{profile.VariantId}'.");
                    }
                    if (invalidGroups.Contains(profile.GuardianForceId))
                    {
                        throw new InvalidDataException($"GF {profile.GuardianForceId} must have one profile identity and at most 256 variants.");
                    }
                    foreach (BattleGuardianFormActionBinding action in variant.Actions)
                    {
                        if (action.PresentedModels.Any(id => !profiles.TryGetValue(id, out BattleGuardianFormDefinition? companion) || companion.GuardianForceId != profile.GuardianForceId))
                        {
                            throw new InvalidDataException($"Action '{action.Name}' refers to a missing model variant for the same GF.");
                        }
                        if (!NativeBattleGuardianFormPresentation.SupportsActionParticipants(action.NativeIdentity!.EffectId, action.PresentedModels.Select(id => profiles[id].ModelId)) ||
                            !action.PresentedModels.IsEmpty && !action.PresentedModels.Contains(profile.VariantId))
                        {
                            throw new InvalidDataException($"Action '{action.Name}' requires the actor and both native paired body roles in the declared participants.");
                        }
                    }
                    assets ??= openAssets();
                    resources.Add(assets.ReadGuardianFormResources(profile, variant.Actions.Add(profile.Finisher)));
                }
                catch (Exception exception) when (IsPayloadError(exception))
                {
                    errors.Add(new("Memoria.InvalidGuardianForm", $"'{profile.VariantId}': {exception.Message}", file.PackageId, PayloadPath));
                }
            }
        }
        finally
        {
            assets?.Dispose();
        }
        result = errors.Count == 0 ? new([.. resources], [.. candidates.Select(candidate => candidate.File.PackageId).Distinct(StringComparer.OrdinalIgnoreCase)]) : Empty;
        diagnostics = errors.AsReadOnly();
        return errors.Count == 0;
    }

    private static void ValidateVariant(GuardianFormModVariant variant)
    {
        BattleGuardianFormDefinition profile = variant.Profile ?? throw new InvalidDataException("A GF form variant requires a profile.");
        if (!IsIdentifier(profile.ProfileId) || !IsIdentifier(profile.VariantId) || string.IsNullOrWhiteSpace(profile.Name) ||
            profile.UseInstalledModelName && profile.UseGuardianForceName ||
            profile.GuardianForceId is < 0 or >= 16 || profile.ModelId is < 0 or > 239 || profile.LifetimeTicks is < 1 or > ushort.MaxValue ||
            profile.Adjustments is null || profile.Adjustments.StrengthPercent is < 1 or > 1000 || profile.Adjustments.MagicPercent is < 1 or > 1000 ||
            profile.SupportSequence == 0 || profile.Tiers.IsDefaultOrEmpty || profile.Tiers.Any(tier => tier is null) ||
            variant.Actions.IsDefaultOrEmpty || variant.Actions.Any(action => action is null) || profile.Finisher is null)
        {
            throw new InvalidDataException("Invalid GF form profile identity, model, lifetime, statistics, or action set.");
        }
        if (profile.Finisher is not { Source: BattleGuardianFormActionSource.GuardianForce, Role: BattleGuardianFormActionRole.Finisher, Terminal: true } ||
            profile.Finisher.ResourceActionId != profile.GuardianForceId || !profile.Finisher.PresentedModels.IsEmpty ||
            variant.Actions.Any(action => action.Terminal || action.Role == BattleGuardianFormActionRole.Finisher || action.Source == BattleGuardianFormActionSource.GuardianForce))
        {
            throw new InvalidDataException("A GF form must have one terminal action from its own GF summon row.");
        }
        Dictionary<BattleGuardianFormAction, BattleGuardianFormActionBinding> actions = [];
        foreach (BattleGuardianFormActionBinding action in variant.Actions.Add(profile.Finisher))
        {
            ValidateAction(action);
            if (!actions.TryAdd(action.Action, action))
            {
                throw new InvalidDataException($"Duplicate GF form action ID {(int)action.Action}.");
            }
        }
        int previousLevel = 0;
        HashSet<BattleGuardianFormAction> referencedActions = [];
        foreach (BattleGuardianFormTier tier in profile.Tiers)
        {
            if ((previousLevel == 0 && tier.Level != 1) || tier.Level <= previousLevel || tier.Level > 100 || tier.Actions.IsDefaultOrEmpty ||
                tier.Actions.Distinct().Count() != tier.Actions.Length || tier.Actions.Any(action => !actions.ContainsKey(action)) || !tier.Actions.Contains(profile.Finisher.Action) ||
                tier.CompelledPhysical is not { } physical || !tier.Actions.Contains(physical) ||
                actions[physical] is not { Role: BattleGuardianFormActionRole.Attack, Target: BattleGuardianFormTarget.SingleEnemy, Source: BattleGuardianFormActionSource.Physical, OncePerInvocation: false, RequiresGrounded: false } ||
                tier.CompelledSpell is { } spell && (!tier.Actions.Contains(spell) || actions[spell] is not { Role: BattleGuardianFormActionRole.Spell, OncePerInvocation: false, RequiresGrounded: false }) ||
                tier.HealBeforeAttack is { } heal && (tier.Actions.Contains(heal) || !actions.TryGetValue(heal, out BattleGuardianFormActionBinding? binding) ||
                    binding is not { Role: BattleGuardianFormActionRole.Recovery, Target: BattleGuardianFormTarget.Self, RequiresGrounded: true, OncePerInvocation: true }))
            {
                throw new InvalidDataException($"Invalid GF form progression at level {tier.Level}.");
            }
            referencedActions.UnionWith(tier.Actions);
            if (tier.HealBeforeAttack is { } recovery)
            {
                referencedActions.Add(recovery);
            }
            previousLevel = tier.Level;
        }
        if (referencedActions.Count != actions.Count)
        {
            throw new InvalidDataException("Every declared GF form action must occur in the progression.");
        }
    }

    private static void ValidateAction(BattleGuardianFormActionBinding action)
    {
        if ((uint)action.Action >= 0x100 || string.IsNullOrWhiteSpace(action.Name) || action.LabelKey is not null && string.IsNullOrWhiteSpace(action.LabelKey) || !Enum.IsDefined(action.Source) ||
            !Enum.IsDefined(action.Role) || !Enum.IsDefined(action.Target) || !Enum.IsDefined(action.Cost) ||
            action.ResourceActionId is < 0 or > ushort.MaxValue || action.Sequence == 0 || action.Power is < 0 or > byte.MaxValue ||
            action.PresentedModels.IsDefault || action.PresentedModels.Any(id => !IsIdentifier(id)) || action.PresentedModels.Distinct(StringComparer.Ordinal).Count() != action.PresentedModels.Length ||
            action.NativeIdentity is null || action.NativeCommand.HasValue && action.Source != BattleGuardianFormActionSource.Native ||
            action.NativeCommand is not (null or 8 or 15) || action.Power.HasValue && action.Source is not (BattleGuardianFormActionSource.Native or BattleGuardianFormActionSource.Physical) ||
            (action.Command, action.NativeCrisisLevel) is not ((2 or 8 or 254, null) or (15, >= 1 and <= 4)) ||
            action.Role == BattleGuardianFormActionRole.Recovery && (action.Target != BattleGuardianFormTarget.Self || action.NativeIdentity.AttackType != 3) ||
            action.Terminal && (action.Power.HasValue || action.OncePerInvocation || action.RequiresGrounded || action.RequiresFullAtb || action.Cost != BattleGuardianFormActionCost.Native) ||
            action.RequirePrototypeBinding && action.Command is not (2 or 8))
        {
            throw new InvalidDataException($"Invalid GF form action '{action.Name}' with ID {(int)action.Action}.");
        }
    }

    private static bool IsIdentifier(string? id)
    {
        return id is { Length: >= 1 and <= 64 } && char.IsAsciiLetterOrDigit(id[0]) &&
            id.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
    }

    private static bool IsPayloadError(Exception error)
    {
        return error is JsonException or IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or KeyNotFoundException or OverflowException;
    }
}
