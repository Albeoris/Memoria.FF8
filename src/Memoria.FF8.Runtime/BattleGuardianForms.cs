using System.Collections.Immutable;

using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum BattleGuardianFormPhase
{
    Charging,
    Entering,
    Active,
    Finishing,
    Returning
}

public enum BattleGuardianFormAction
{
    Attack,
    Fire,
    HellFire,
    HeavyStrike
}

/// <summary>Small, typed profile adjustments applied to the actor's statistics.</summary>
public sealed record BattleGuardianFormAdjustments(int StrengthPercent = 100, int MagicPercent = 100);

public enum BattleGuardianFormActionSource
{
    Physical,
    Magic,
    GuardianForce
}

public enum BattleGuardianFormActionCost
{
    Native,
    Innate
}

public sealed record BattleGuardianFormActionBinding(BattleGuardianFormAction Action, string Name, BattleGuardianFormActionSource Source, int ResourceActionId, byte Sequence, BattleGuardianFormActionCost Cost = BattleGuardianFormActionCost.Native, bool Terminal = false);

public sealed record BattleGuardianFormDefinition(string ProfileId, int GuardianForceId, int ModelId, int LifetimeTicks, BattleGuardianFormAdjustments Adjustments, BattleGuardianFormActionBinding Finisher);

public static class BattleGuardianFormProfiles
{
    private static readonly BattleGuardianFormDefinition Ifrit = new("ifrit-v1", 2, 61, 900, new(), new(BattleGuardianFormAction.HellFire, "Hell Fire", BattleGuardianFormActionSource.GuardianForce, 2, 14, Terminal: true));

    public static BattleGuardianFormDefinition? Find(int guardianForceId)
    {
        return guardianForceId == Ifrit.GuardianForceId ? Ifrit : null;
    }

    internal static BattleDefinition RestoreLegacyAbilityBands(BattleDefinition definition)
    {
        if (!definition.GuardianFormsEnabled || !definition.GuardianFormResources.Any(resource => resource.GuardianForceId == Ifrit.GuardianForceId && resource.AbilityBands is null))
        {
            return definition;
        }
        return definition with { GuardianFormResources = definition.GuardianFormResources.Select(RestoreResource).ToImmutableArray() };

        BattleGuardianFormResources RestoreResource(BattleGuardianFormResources resource)
        {
            if (resource.GuardianForceId != Ifrit.GuardianForceId || resource.AbilityBands is not null)
            {
                return resource;
            }
            BattleGuardianFormActionBinding attack = new(BattleGuardianFormAction.Attack, "Attack", BattleGuardianFormActionSource.Physical, resource.PhysicalAction.ActionId, resource.PhysicalSequence);
            BattleGuardianFormActionBinding fire = new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, 1, 11, BattleGuardianFormActionCost.Innate);
            ImmutableArray<BattleGuardianFormActionBinding> common = [attack, fire, Ifrit.Finisher];
            // Old standalone components may carry only the original Attack and Fire bindings.
            ImmutableArray<BattleGuardianFormActionBinding> low = definition.NativeActions.Any(action => action is { Command: 8, ActionId: 59, AttackType: 1, Power: 20, MagicId: 0, HitCount: 1, Element: 0, Statuses0: 0, Statuses1: 0 })
                ? [attack, fire, new(BattleGuardianFormAction.HeavyStrike, "Heavy Strike", BattleGuardianFormActionSource.Physical, 59, 12), Ifrit.Finisher]
                : common;
            return resource with { AbilityBands = new(20, 30, [low, common, common]) };
        }
    }
}

public sealed record BattleGuardianFormAbilityBands(byte MediumLevel, byte HighLevel, ImmutableArray<ImmutableArray<BattleGuardianFormActionBinding>> Bands)
{
    internal ImmutableArray<BattleGuardianFormActionBinding> AtLevel(int level)
    {
        return Bands[NativeBattleEnemyLevelRules.SelectBand(level, MediumLevel, HighLevel)];
    }
}

/// <summary>Installed actions, presentation and prototype defenses prepared only for an enabled battle.</summary>
public sealed record BattleGuardianFormResources(int GuardianForceId, BattleNativeAction PhysicalAction, byte PhysicalSequence, NativeBattleGraphicsResource VisualResource)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleGuardianFormAbilityBands? AbilityBands { get; init; }
    public ImmutableArray<int> ElementDefenses { get; init; } = [];
    public ImmutableArray<byte> StatusResistances { get; init; } = [];
}

public sealed record BattleGuardianFormInvocation(long Id, int GuardianForceId, GuardianFormInvocationMode Mode);

public sealed record BattleGuardianFormState(string ActorId, int GuardianForceId, string ProfileId, BattleGuardianFormPhase Phase, long InvocationId, int RemainingTicks)
{
    public bool EntryCompleted { get; init; }
    public bool FinisherAdmitted { get; init; }
}

public sealed record BattleGuardianFormsSnapshot(bool Enabled, long LastInvocationId, ImmutableArray<BattleGuardianFormState> Forms);

/// <summary>A view of the canonical GF row, never an independently writable HP copy.</summary>
internal sealed class BattleGuardianFormVitalOwner(Func<BattleGuardianForceVital> read, Action<int> write, BattleGuardianFormAdjustments adjustments, BattleGuardianFormResources resources)
{
    public int HitPoints
    {
        get => read().HitPoints;
        set => write(value);
    }

    public int MaximumHitPoints => read().MaximumHitPoints;
    public BattleGuardianFormAdjustments Adjustments => adjustments;
    public ImmutableArray<int> ElementDefenses => resources.ElementDefenses;
    public ImmutableArray<byte> StatusResistances => resources.StatusResistances;
}
