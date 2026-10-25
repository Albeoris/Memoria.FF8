namespace Memoria.FF8.Runtime;

public readonly record struct AchievementId(string Value);
public readonly record struct AchievementEventId(string Value);
public readonly record struct AchievementContextId(string Value);

public interface IAchievementEvent
{
    AchievementEventId EventId { get; }
    AchievementContextId ContextId { get; }
    AchievementOwner? Owner { get; }
}

public readonly record struct EnemyDefeatFacts(string AllocationId, BattleDefeatCredit Credit, byte? KillerRow, byte? GuardianForceId, byte? NativeCommand, bool CardSelected, BattleRewardPolicy RewardPolicy)
{
    public long? NativeKillTotal { get; init; }
    public BattleOutcome? AcceptedOutcome { get; init; }
    public bool ResultPresentationEligible { get; init; }
}

public readonly record struct EnemyDefeatedEvent(AchievementEventId EventId, AchievementContextId ContextId, EnemyDefeatFacts Facts) : IAchievementEvent
{
    public AchievementOwner? Owner { get; init; }
    public static EnemyDefeatedEvent FromBattle(string occurrenceId, BattleEnemyDefeat defeat, BattleRewardPolicy rewardPolicy = BattleRewardPolicy.NativeCombat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        defeat.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(defeat.AllocationId);
        return new(new AchievementEventId(defeat.AllocationId), new AchievementContextId(occurrenceId), new EnemyDefeatFacts(defeat.AllocationId, defeat.Credit, defeat.KillerRow, defeat.GuardianForceId, defeat.NativeCommand, defeat.CardSelected, rewardPolicy));
    }
}

/// <summary>Accepted completion-boundary observation, including battles without a new qualifying defeat.</summary>
public readonly record struct BattleKillTotalObservedEvent(AchievementEventId EventId, AchievementContextId ContextId, long? NativeKillTotal, BattleOutcome Outcome, BattleRewardPolicy RewardPolicy, byte FormationFlags, AchievementOwner? Owner = null) : IAchievementEvent;

public enum AchievementScope
{
    LocalProfile,
    AccountCumulative,
    SaveHighWater
}

public sealed record AchievementDefinition(AchievementId Id, string Name, string Description, long Target, AchievementScope Scope);
public sealed record AchievementSnapshot(AchievementId Id, long Counter, long Target, bool Unlocked, bool ProgressKnown = true, string Name = "")
{
    public long DisplayProgress => Math.Min(Counter, Target);
}
public sealed record AchievementChange(AchievementSnapshot Previous, AchievementSnapshot Current)
{
    public bool NewlyUnlocked => !Previous.Unlocked && Current.Unlocked;
}

public interface IAchievementService
{
    void Record<TEvent>(in TEvent fact) where TEvent : struct, IAchievementEvent;
    void RecordBatch<TEvent>(IReadOnlyList<TEvent> facts) where TEvent : struct, IAchievementEvent;
    AchievementSnapshot Get(AchievementId id);
    event Action<AchievementChange>? Committed;
}

/// <summary>Handlers return nonnegative contributions and must be pure functions of the immutable fact.</summary>
public sealed class AchievementRegistry
{
    private readonly Dictionary<AchievementId, AchievementDefinition> definitions = [];
    private readonly Dictionary<Type, List<(AchievementId Id, Func<object, long?> Evaluate)>> handlers = [];
    private bool sealedRegistry;
    public static AchievementId EnemiesDefeated100 { get; } = new("ff8.combat.enemies_defeated.100");
    public IReadOnlyCollection<AchievementDefinition> Definitions => definitions.Values;

    public void Register<TEvent>(AchievementDefinition definition, Func<TEvent, long?> handler) where TEvent : struct, IAchievementEvent
    {
        if (sealedRegistry)
        {
            throw new InvalidOperationException("Achievement registration is closed after service composition.");
        }
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(definition.Id.Value) || !definition.Id.Value.Contains('.') || string.IsNullOrWhiteSpace(definition.Name) || definition.Target <= 0 || !Enum.IsDefined(definition.Scope))
        {
            throw new ArgumentException("Achievements require a namespaced ID, name, positive target and supported scope.", nameof(definition));
        }
        if (!definitions.TryAdd(definition.Id, definition))
        {
            throw new ArgumentException("An achievement ID may be registered only once.", nameof(definition));
        }
        if (!handlers.TryGetValue(typeof(TEvent), out var registered))
        {
            registered = [];
            handlers.Add(typeof(TEvent), registered);
        }
        registered.Add((definition.Id, fact => handler((TEvent)fact)));
    }

    public static AchievementRegistry CreateCombatRegistry(bool remastered)
    {
        AchievementRegistry registry = new();
        registry.Register<BattleKillTotalObservedEvent>(new(EnemiesDefeated100, "Defeat 100 enemies", "Defeat 100 qualifying enemies.", 100, AchievementScope.SaveHighWater), fact =>
        {
            if (!Enum.IsDefined(fact.Outcome) || !Enum.IsDefined(fact.RewardPolicy))
            {
                throw new InvalidDataException("Combat achievement observation has an invalid outcome or reward policy.");
            }
            if (fact.RewardPolicy != BattleRewardPolicy.NativeCombat || fact.Outcome == BattleOutcome.Active || remastered && fact.Outcome != BattleOutcome.Escaped && (fact.Outcome != BattleOutcome.Victory || (fact.FormationFlags & 0x10) != 0))
            {
                return null;
            }
            return fact.NativeKillTotal;
        });
        return registry;
    }

    internal void Seal() => sealedRegistry = true;
    internal AchievementDefinition Definition(AchievementId id) => definitions[id];
    internal IEnumerable<(AchievementId Id, long Contribution)> Evaluate<TEvent>(TEvent fact) where TEvent : struct, IAchievementEvent
    {
        if (handlers.TryGetValue(typeof(TEvent), out var registered))
        {
            foreach (var handler in registered.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
            {
                long? contribution = handler.Evaluate(fact);
                if (contribution is null)
                {
                    continue;
                }
                if (contribution < 0)
                {
                    throw new InvalidOperationException("Achievement handlers cannot remove progress.");
                }
                yield return (handler.Id, contribution.Value);
            }
        }
    }
}
