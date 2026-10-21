using System.Collections.Immutable;

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
    HellFire
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

public sealed record BattleGuardianFormDefinition(string ProfileId, int GuardianForceId, int ModelId, int LifetimeTicks, BattleGuardianFormAdjustments Adjustments)
{
    public ImmutableArray<BattleGuardianFormActionBinding> Actions { get; init; } = [];

    public BattleGuardianFormActionBinding Binding(BattleGuardianFormAction action)
    {
        return Actions.SingleOrDefault(binding => binding.Action == action) ?? throw new NotSupportedException("This GF profile does not bind the selected form action.");
    }
}

public static class BattleGuardianFormProfiles
{
    private static readonly BattleGuardianFormDefinition Ifrit = new("ifrit-v1", 2, 61, 30 * BattleSession.FramesPerSecond, new())
    {
        Actions =
        [
            new(BattleGuardianFormAction.Attack, "Attack", BattleGuardianFormActionSource.Physical, 79, 12),
            new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, 1, 11, BattleGuardianFormActionCost.Innate),
            new(BattleGuardianFormAction.HellFire, "Hell Fire", BattleGuardianFormActionSource.GuardianForce, 2, 14, Terminal: true)
        ]
    };

    public static BattleGuardianFormDefinition? Find(int guardianForceId)
    {
        return guardianForceId == Ifrit.GuardianForceId ? Ifrit : null;
    }
}

/// <summary>Installed actions, presentation and prototype defenses prepared only for an enabled battle.</summary>
public sealed record BattleGuardianFormResources(int GuardianForceId, BattleNativeAction PhysicalAction, byte PhysicalSequence, NativeBattleGraphicsResource VisualResource)
{
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
