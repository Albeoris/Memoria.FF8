using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>An ordered selection whose addressing was fixed by the geometry writer.</summary>
public sealed class BattleRetainedModelSelection
{
    private readonly ImmutableArray<BattleRetainedModelReference> references;

    private BattleRetainedModelSelection(ImmutableArray<BattleRetainedModelReference> references)
    {
        this.references = references;
    }

    public int Count => references.Length;
    public bool IsEmpty => references.IsEmpty;
    public static BattleRetainedModelSelection Empty { get; } = new([]);

    internal static BattleRetainedModelSelection From(IEnumerable<BattleRetainedModelReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        var ordered = ImmutableArray.CreateBuilder<BattleRetainedModelReference>();
        HashSet<BattleRetainedModelReference> seen = [];
        foreach (BattleRetainedModelReference reference in references)
        {
            ArgumentNullException.ThrowIfNull(reference);
            if (seen.Add(reference))
            {
                ordered.Add(reference);
            }
        }
        return ordered.Count == 0 ? Empty : new(ordered.ToImmutable());
    }

    internal static BattleRetainedModelSelection Capture(BattleModelTargets targets, NativeBattleModelAliases aliases)
    {
        return From(targets.Enumerate().Select(owner => BattleRetainedModelReference.Capture(owner, aliases)));
    }

    internal bool Contains(BattleRetainedModelReference reference) => references.Contains(reference);

    internal BattleModelTargets Resolve(BattleActorModels models, NativeBattleModelBoundary boundary)
    {
        return BattleModelTargets.From(references.Select(reference => reference.Resolve(models, boundary)));
    }

    internal BattleModelTargets Resolve(NativeBattleModelAliases aliases)
    {
        return BattleModelTargets.From(references.Select(reference => reference.Resolve(aliases)));
    }
}
