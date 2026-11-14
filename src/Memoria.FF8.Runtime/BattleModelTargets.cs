using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class BattleModelTargets : IEquatable<BattleModelTargets>
{
    private readonly ImmutableArray<BattleModelOwner> owners;

    private BattleModelTargets(ImmutableArray<BattleModelOwner> owners)
    {
        this.owners = owners;
    }

    public static BattleModelTargets Empty { get; } = new([]);

    public static BattleModelTargets From(IEnumerable<BattleModelOwner> owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        var ordered = ImmutableArray.CreateBuilder<BattleModelOwner>();
        HashSet<BattleModelOwner> seen = [];
        foreach (BattleModelOwner owner in owners)
        {
            ArgumentNullException.ThrowIfNull(owner);
            if (seen.Add(owner))
            {
                ordered.Add(owner);
            }
        }
        return ordered.Count == 0 ? Empty : new(ordered.ToImmutable());
    }

    public int Count => owners.Length;
    public bool IsEmpty => owners.IsEmpty;

    public BattleModelTargets Add(BattleModelOwner owner)
    {
        return Contains(owner) ? this : new(owners.Add(owner));
    }

    public BattleModelTargets Remove(BattleModelOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ImmutableArray<BattleModelOwner> remaining = owners.Remove(owner);
        return remaining.IsEmpty ? Empty : new(remaining);
    }

    public bool Contains(BattleModelOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owners.Contains(owner);
    }

    public bool IsSingle(BattleModelOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Count == 1 && owners[0] == owner;
    }

    public IEnumerable<BattleModelOwner> Enumerate() => owners;

    public bool Equals(BattleModelTargets? other)
    {
        return ReferenceEquals(this, other) || other is not null && owners.SequenceEqual(other.owners);
    }

    public override bool Equals(object? obj) => obj is BattleModelTargets targets && Equals(targets);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (BattleModelOwner owner in owners)
        {
            hash.Add(owner);
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(BattleModelTargets? left, BattleModelTargets? right) => Equals(left, right);

    public static bool operator !=(BattleModelTargets? left, BattleModelTargets? right) => !Equals(left, right);
}
