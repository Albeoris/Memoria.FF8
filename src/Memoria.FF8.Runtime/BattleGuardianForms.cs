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
    HeavyStrike,
    Fira,
    Firaga,
    Flare
}

public sealed record BattleGuardianFormCommand(int Id, BattleActionDefinition Action, int? LearnedAbilityId = null, byte? NativeCommand = null, byte? TargetFlags = null)
{
    internal const int LearnedAbilityOffset = 0x100;
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
    private static readonly ImmutableArray<(int Level, ImmutableArray<BattleGuardianFormAction> Actions)> IfritProgression =
    [
        (1, [BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire, BattleGuardianFormAction.HellFire]),
        (15, [BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Fire, BattleGuardianFormAction.HellFire]),
        (30, [BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Fira, BattleGuardianFormAction.HellFire]),
        (70, [BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Firaga, BattleGuardianFormAction.HellFire]),
        (100, [BattleGuardianFormAction.HeavyStrike, BattleGuardianFormAction.Firaga, BattleGuardianFormAction.Flare, BattleGuardianFormAction.HellFire])
    ];

    public static BattleGuardianFormDefinition? Find(int guardianForceId)
    {
        return guardianForceId == Ifrit.GuardianForceId ? Ifrit : null;
    }

    internal static ImmutableArray<BattleGuardianFormAction> ActionsAtLevel(int level)
    {
        return IfritProgression.Last(tier => tier.Level <= level).Actions;
    }

    public static bool HasLearnedCommand(NativeCharacterVitals? vitals, int guardianForceId, byte command)
    {
        return LearnedCommands(vitals, guardianForceId).Any(binding => binding.Command == command);
    }

    internal static IEnumerable<(int Ability, byte Command)> LearnedCommands(NativeCharacterVitals? vitals, int guardianForceId)
    {
        if (vitals is null)
        {
            yield break;
        }
        ImmutableArray<byte> commands = vitals.MenuDefinitions.CommandAbilityCommands;
        foreach (NativeGuardianForceAbility learned in vitals.GuardianForceAbilities(guardianForceId))
        {
            int index = learned.Ability - 20;
            if (learned.Learned && (uint)index < commands.Length && commands[index] is not (0 or 2 or 3 or 4 or 6))
            {
                yield return (learned.Ability, commands[index]);
            }
        }
    }

    public static ImmutableArray<BattleGuardianFormActionBinding> BindIfritInnateActions(BattleGuardianFormActionBinding attack, BattleGuardianFormActionBinding fire, BattleGuardianFormActionBinding? heavy)
    {
        ImmutableArray<BattleGuardianFormActionBinding> bindings =
        [
            attack,
            fire,
            fire with { Action = BattleGuardianFormAction.Fira, Name = "Fira", ResourceActionId = 2 },
            fire with { Action = BattleGuardianFormAction.Firaga, Name = "Firaga", ResourceActionId = 3 },
            fire with { Action = BattleGuardianFormAction.Flare, Name = "Flare", ResourceActionId = 15 },
            Ifrit.Finisher
        ];
        return heavy is null ? bindings : bindings.Add(heavy);
    }

    internal static BattleDefinition RestoreLegacyResources(BattleDefinition definition)
    {
        if (!definition.GuardianFormsEnabled || !definition.GuardianFormResources.Any(resource => resource.GuardianForceId == Ifrit.GuardianForceId && resource.InnateBindings.IsDefault))
        {
            return definition;
        }
        return definition with { GuardianFormResources = definition.GuardianFormResources.Select(RestoreResource).ToImmutableArray() };

        BattleGuardianFormResources RestoreResource(BattleGuardianFormResources resource)
        {
            if (resource.GuardianForceId != Ifrit.GuardianForceId || !resource.InnateBindings.IsDefault)
            {
                return resource;
            }
            ImmutableArray<BattleGuardianFormActionBinding> original = resource.AbilityBands is { Bands: [var low, ..] } ? low : [];
            BattleGuardianFormActionBinding attack = original.SingleOrDefault(binding => binding.Action == BattleGuardianFormAction.Attack) ??
                new(BattleGuardianFormAction.Attack, "Attack", BattleGuardianFormActionSource.Physical, resource.PhysicalAction.ActionId, resource.PhysicalSequence);
            BattleGuardianFormActionBinding fire = original.SingleOrDefault(binding => binding.Action == BattleGuardianFormAction.Fire) ??
                new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, 1, 11, BattleGuardianFormActionCost.Innate);
            BattleGuardianFormActionBinding? heavy = original.SingleOrDefault(binding => binding.Action == BattleGuardianFormAction.HeavyStrike);
            if (heavy is null && definition.NativeActions.Any(action => action is { Command: 8, ActionId: 59, AttackType: 1, Power: 20, MagicId: 0, HitCount: 1, Element: 0, Statuses0: 0, Statuses1: 0 }))
            {
                heavy = new(BattleGuardianFormAction.HeavyStrike, "Heavy Strike", BattleGuardianFormActionSource.Physical, 59, 12);
            }
            return resource with { InnateBindings = BindIfritInnateActions(attack, fire, heavy), AbilityBands = null };
        }
    }
}

public sealed record BattleGuardianFormAbilityBands(byte MediumLevel, byte HighLevel, ImmutableArray<ImmutableArray<BattleGuardianFormActionBinding>> Bands);

/// <summary>Installed actions, presentation and prototype defenses prepared only for an enabled battle.</summary>
public sealed record BattleGuardianFormResources(int GuardianForceId, BattleNativeAction PhysicalAction, byte PhysicalSequence, NativeBattleGraphicsResource VisualResource)
{
    // Kept for reading standalone saves from the original form implementation.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleGuardianFormAbilityBands? AbilityBands { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<BattleGuardianFormActionBinding> InnateBindings { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<BattleNativeCommandBinding> CommandBindings { get; init; }
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
