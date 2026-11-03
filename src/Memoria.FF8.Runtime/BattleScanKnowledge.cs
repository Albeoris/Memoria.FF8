using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleObserverSide
{
    Party,
    Enemy
}

/// <summary>A captured affinity; absorption is separate from damage multipliers.</summary>
public sealed record BattleKnownAffinity(decimal DamageMultiplier, bool Absorbs)
{
    public static BattleKnownAffinity FromNativeDefense(int defense)
    {
        decimal multiplier = (900m - defense) / 100m;
        return new(Math.Max(0m, multiplier), multiplier < 0m);
    }
}

/// <summary>Target IDs identify live allocations, rather than shared model resources.</summary>
public sealed record BattleScanObservation(BattleObserverSide Observer, string TargetId, string ScannerId, long Frame, ImmutableArray<BattleKnownAffinity> Affinities);

/// <summary>Immutable battle-local information obtained only by successful Scan contacts.</summary>
public sealed record BattleScanKnowledge
{
    public ImmutableArray<BattleScanObservation> Observations { get; init; } = [];

    public BattleScanObservation? Find(BattleObserverSide observer, string targetId)
    {
        return Observations.SingleOrDefault(observation => observation.Observer == observer && observation.TargetId == targetId);
    }

    public BattleScanKnowledge Observe(BattleScanObservation observation)
    {
        ValidateObservation(observation);
        return this with { Observations = Observations.Where(previous => previous.Observer != observation.Observer || previous.TargetId != observation.TargetId).Append(observation).ToImmutableArray() };
    }

    public void Validate()
    {
        if (Observations.IsDefault || Observations.Select(observation => (observation.Observer, observation.TargetId)).Distinct().Count() != Observations.Length)
        {
            throw new InvalidDataException("Scan knowledge contains missing or duplicate observation owners.");
        }
        foreach (BattleScanObservation observation in Observations)
        {
            ValidateObservation(observation);
        }
    }

    private static void ValidateObservation(BattleScanObservation observation)
    {
        if (observation is null || !Enum.IsDefined(observation.Observer) || string.IsNullOrWhiteSpace(observation.TargetId) || string.IsNullOrWhiteSpace(observation.ScannerId) || observation.Frame < 0 || observation.Affinities.IsDefault || observation.Affinities.Length != 8 || observation.Affinities.Any(affinity => affinity is null || affinity.DamageMultiplier < 0 || affinity.Absorbs && affinity.DamageMultiplier != 0))
        {
            throw new InvalidDataException("Scan observation requires an observer, allocation identities, capture frame and eight normalized affinities.");
        }
    }
}
