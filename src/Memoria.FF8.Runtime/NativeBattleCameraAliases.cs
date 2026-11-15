using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>50D300's retained original BYTEs and the live aliases consumed independently by50D790.</summary>
public sealed record NativeBattleCameraAliases([property: JsonRequired] byte? Actor1D99B95, [property: JsonRequired] byte? Target1D99AAA)
{
    [JsonIgnore]
    public BattleModelOwner? Source { get; init; }

    [JsonIgnore]
    public BattleModelOwner? Target { get; init; }

    public static NativeBattleCameraAliases FromColdProcess() => new(0, 0);

    public static NativeBattleCameraAliases Unknown() => new(null, null);

    public void Validate()
    {
        if (Actor1D99B95 >= NativeBattleModelBoundary.CellCount || Target1D99AAA >= NativeBattleModelBoundary.CellCount)
        {
            throw new InvalidDataException("Native camera alias BYTEs must name original model cells.");
        }
    }

    internal NativeBattleCameraAliases Observe50D300(BattleModelOwner source, BattleModelTargets targets, BattleActorModels models, NativeBattleModelAliases aliases)
    {
        if (!models.Contains(source) || targets.Enumerate().Any(owner => !models.Contains(owner)))
        {
            throw new InvalidDataException("The camera observation requires currently bound model owners.");
        }
        BattleModelOwner? lastTarget = models.Owners.LastOrDefault(targets.Contains);
        return this with
        {
            Source = source,
            Actor1D99B95 = aliases.TryEncodeExact(source, out byte actorAlias) ? actorAlias : null,
            Target = lastTarget ?? Target,
            Target1D99AAA = lastTarget is null ? Target1D99AAA : aliases.TryEncodeExact(lastTarget, out byte targetAlias) ? targetAlias : null
        };
    }

    internal BattleModelOwner Resolve50D790(byte reference, BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        Validate();
        (BattleModelOwner? owner, byte? alias) = (reference >> 4) switch
        {
            9 => (Source, Actor1D99B95),
            10 => (Target, Target1D99AAA),
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
        BattleRetainedModelReference binding = alias is byte original
            ? new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(original))
            : owner is not null ? new BattleRetainedModelReference.CanonicalOwner(owner)
            : throw new NotSupportedException("Native50D790 requires its retained actor or target BYTE.");
        return binding.Resolve(models, boundary);
    }
}
