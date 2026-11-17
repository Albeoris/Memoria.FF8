using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed class BattleModelRoster
{
    private readonly ImmutableArray<BattleModelOwner> owners;
    private readonly ImmutableArray<BattlePartyModelOwner> party;
    private readonly ImmutableArray<BattleEnemyModelOwner> enemies;
    private readonly ImmutableHashSet<BattleModelOwner> membership;

    public BattleModelRoster(IEnumerable<BattleModelOwner> owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        var ordered = ImmutableArray.CreateBuilder<BattleModelOwner>();
        var party = ImmutableArray.CreateBuilder<BattlePartyModelOwner>();
        var enemies = ImmutableArray.CreateBuilder<BattleEnemyModelOwner>();
        var membership = ImmutableHashSet.CreateBuilder<BattleModelOwner>();
        HashSet<(string Key, bool Actor)> identities = [];
        foreach (BattleModelOwner owner in owners)
        {
            ArgumentNullException.ThrowIfNull(owner);
            RegisterIdentity(owner, identities);
            ordered.Add(owner);
            membership.Add(owner);
            switch (owner)
            {
                case BattlePartyModelOwner ally:
                    party.Add(ally);
                    break;
                case BattleEnemyModelOwner enemy:
                    enemies.Add(enemy);
                    break;
            }
        }
        this.owners = ordered.ToImmutable();
        this.party = party.ToImmutable();
        this.enemies = enemies.ToImmutable();
        this.membership = membership.ToImmutable();
    }

    public IEnumerable<BattleModelOwner> Owners => owners;
    public IEnumerable<BattlePartyModelOwner> Party => party;
    public IEnumerable<BattleEnemyModelOwner> Enemies => enemies;
    public int Count => owners.Length;

    public bool Contains(BattleModelOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return membership.Contains(owner);
    }

    internal bool ContainsPartyActor(string actorId)
    {
        foreach (BattlePartyModelOwner owner in party)
        {
            if (owner is BattlePartyActorModel actor && actor.Actor.ActorId == actorId)
            {
                return true;
            }
        }
        return false;
    }

    internal static void RegisterIdentity(BattleModelOwner owner, HashSet<(string Key, bool Actor)> identities)
    {
        (string Key, bool Actor) identity = owner switch
        {
            BattlePartyActorModel actor => (actor.Actor.ActorId, true),
            BattleEnemyActorModel actor => (actor.Actor.ActorId, true),
            BattleForeignPartyModel foreign => (foreign.Presentation.Key, false),
            BattleForeignEnemyModel foreign => (foreign.Presentation.Key, false),
            _ => throw new ArgumentException("Unknown model owner.", nameof(owner))
        };
        if (!identities.Add(identity))
        {
            throw new ArgumentException("A model identity is bound more than once.", nameof(owner));
        }
    }
}
