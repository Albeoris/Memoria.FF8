using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGraphicsActor(string ActorId, bool IsParty);

/// <summary>Hosted presentation bindings from graphics rows to actor identities and sides.</summary>
public sealed class NativeBattleGraphicsRoster
{
    [JsonConstructor]
    public NativeBattleGraphicsRoster(ImmutableArray<NativeBattleGraphicsActor?> rows)
    {
        Rows = rows;
        Validate();
    }

    public ImmutableArray<NativeBattleGraphicsActor?> Rows { get; }
    [JsonIgnore]
    public int ActorCount => Rows.Length;
    [JsonIgnore]
    public IEnumerable<int> PartyRows => EnumerateSide(isParty: true);
    [JsonIgnore]
    public IEnumerable<int> EnemyRows => EnumerateSide(isParty: false);

    public NativeBattleGraphicsActor this[int row]
    {
        get
        {
            if ((uint)row >= (uint)ActorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            return Rows[row] ?? throw new InvalidDataException($"Graphics row {row} has no actor binding.");
        }
    }

    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length is < NativeBattleGraphicsModelAddress.NativeRowCount or > NativeBattleGraphicsModelAddress.MaximumRowCount)
        {
            throw new InvalidDataException("The hosted graphics roster requires seven through twenty-five rows.");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (NativeBattleGraphicsActor? actor in Rows)
        {
            if (actor is not null && (string.IsNullOrWhiteSpace(actor.ActorId) || !ids.Add(actor.ActorId)))
            {
                throw new InvalidDataException("Bound graphics rows require distinct, nonempty actor IDs.");
            }
        }
        if (ids.Count == 0)
        {
            throw new InvalidDataException("The hosted graphics roster requires at least one actor binding.");
        }
    }

    private IEnumerable<int> EnumerateSide(bool isParty)
    {
        for (int row = 0; row < Rows.Length; row++)
        {
            if (Rows[row]?.IsParty == isParty)
            {
                yield return row;
            }
        }
    }
}
