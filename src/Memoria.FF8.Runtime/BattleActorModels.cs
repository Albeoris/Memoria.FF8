using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

public sealed record BattleModelEntry<TOwner> where TOwner : BattleModelOwner
{
    public BattleModelEntry(TOwner owner, NativeBattleGraphicsModel state)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(state);
        Owner = owner;
        State = state;
    }

    public TOwner Owner { get; }
    public NativeBattleGraphicsModel State { get; }
}

public sealed class BattleModelPool<TOwner> where TOwner : BattleModelOwner
{
    private readonly ImmutableDictionary<TOwner, NativeBattleGraphicsModel> states;
    private readonly ImmutableArray<BattleModelEntry<TOwner>> entries;

    public BattleModelPool(IEnumerable<BattleModelEntry<TOwner>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var states = ImmutableDictionary.CreateBuilder<TOwner, NativeBattleGraphicsModel>();
        var ordered = ImmutableArray.CreateBuilder<BattleModelEntry<TOwner>>();
        HashSet<(string Key, bool Actor)> identities = [];
        foreach (BattleModelEntry<TOwner> entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            BattleActorModels.RegisterIdentity(entry.Owner, identities);
            states.Add(entry.Owner, entry.State);
            ordered.Add(entry);
        }
        this.states = states.ToImmutable();
        this.entries = ordered.ToImmutable();
    }

    private BattleModelPool(ImmutableDictionary<TOwner, NativeBattleGraphicsModel> states, ImmutableArray<BattleModelEntry<TOwner>> entries)
    {
        this.states = states;
        this.entries = entries;
    }

    public IEnumerable<BattleModelEntry<TOwner>> Entries => entries;

    public NativeBattleGraphicsModel Get(TOwner owner) => states[owner];

    public bool TryGet(TOwner owner, [NotNullWhen(true)] out NativeBattleGraphicsModel? state) => states.TryGetValue(owner, out state);

    public bool Contains(TOwner owner) => states.ContainsKey(owner);

    internal BattleModelPool<TOwner> Set(TOwner owner, NativeBattleGraphicsModel state)
    {
        NativeBattleGraphicsModel previous = Get(owner);
        return new(states.SetItem(owner, state), entries.Replace(new(owner, previous), new(owner, state)));
    }
}

public sealed class BattleActorModels
{
    private readonly ImmutableArray<BattleModelOwner> ownerOrder;

    public BattleActorModels(IEnumerable<BattleModelEntry<BattleModelOwner>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var order = ImmutableArray.CreateBuilder<BattleModelOwner>();
        var party = ImmutableArray.CreateBuilder<BattleModelEntry<BattlePartyModelOwner>>();
        var enemies = ImmutableArray.CreateBuilder<BattleModelEntry<BattleEnemyModelOwner>>();
        HashSet<(string Key, bool Actor)> identities = [];
        foreach (BattleModelEntry<BattleModelOwner> entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            RegisterIdentity(entry.Owner, identities);
            switch (entry.Owner)
            {
                case BattlePartyModelOwner owner:
                    party.Add(new(owner, entry.State));
                    break;
                case BattleEnemyModelOwner owner:
                    enemies.Add(new(owner, entry.State));
                    break;
            }
            order.Add(entry.Owner);
        }
        Party = new(party);
        Enemies = new(enemies);
        ownerOrder = order.ToImmutable();
    }

    private BattleActorModels(BattleModelPool<BattlePartyModelOwner> party, BattleModelPool<BattleEnemyModelOwner> enemies, ImmutableArray<BattleModelOwner> ownerOrder)
    {
        Party = party;
        Enemies = enemies;
        this.ownerOrder = ownerOrder;
    }

    public BattleModelPool<BattlePartyModelOwner> Party { get; }
    public BattleModelPool<BattleEnemyModelOwner> Enemies { get; }
    public IEnumerable<BattleModelOwner> Owners => ownerOrder;
    public IEnumerable<BattleModelEntry<BattleModelOwner>> Entries => ownerOrder.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, Get(owner)));

    public NativeBattleGraphicsModel Get(BattleModelOwner owner)
    {
        return TryGet(owner, out NativeBattleGraphicsModel? state) ? state : throw new KeyNotFoundException("Model owner is not bound.");
    }

    public bool TryGet(BattleModelOwner owner, [NotNullWhen(true)] out NativeBattleGraphicsModel? state)
    {
        ArgumentNullException.ThrowIfNull(owner);
        switch (owner)
        {
            case BattlePartyModelOwner party:
                return Party.TryGet(party, out state);
            case BattleEnemyModelOwner enemy:
                return Enemies.TryGet(enemy, out state);
            default:
                state = null;
                return false;
        }
    }

    public bool Contains(BattleModelOwner owner) => TryGet(owner, out _);

    public BattleActorModels Set(BattleModelOwner owner, NativeBattleGraphicsModel state)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(state);
        return owner switch
        {
            BattlePartyModelOwner party => new(Party.Set(party, state), Enemies, ownerOrder),
            BattleEnemyModelOwner enemy => new(Party, Enemies.Set(enemy, state), ownerOrder),
            _ => throw new KeyNotFoundException("Model owner is not bound.")
        };
    }

    public BattleActorModels Add(BattleModelOwner owner, NativeBattleGraphicsModel state)
    {
        return new(Entries.Append(new(owner, state)));
    }

    public BattleActorModels Remove(BattleModelOwner owner)
    {
        RequireBound(owner);
        return new(Entries.Where(entry => entry.Owner != owner));
    }

    public BattleActorModels ReplaceOwner(BattleModelOwner owner, BattleModelOwner replacementOwner, NativeBattleGraphicsModel state)
    {
        RequireBound(owner);
        ArgumentNullException.ThrowIfNull(replacementOwner);
        if (Contains(replacementOwner))
        {
            throw new ArgumentException("The replacement model owner is already bound.", nameof(replacementOwner));
        }
        var replacement = new BattleModelEntry<BattleModelOwner>(replacementOwner, state);
        return new(Entries.Select(entry => entry.Owner == owner ? replacement : entry));
    }

    private void RequireBound(BattleModelOwner owner)
    {
        if (!Contains(owner))
        {
            throw new KeyNotFoundException("Model owner is not bound.");
        }
    }

    internal static void RegisterIdentity(BattleModelOwner owner, HashSet<(string Key, bool Actor)> identities)
    {
        if (!identities.Add(ReadIdentity(owner)))
        {
            throw new ArgumentException("A model identity is bound more than once.", nameof(owner));
        }
    }

    private static (string Key, bool Actor) ReadIdentity(BattleModelOwner owner)
    {
        return owner switch
        {
            BattlePartyActorModel actor => (actor.Actor.ActorId, true),
            BattleEnemyActorModel actor => (actor.Actor.ActorId, true),
            BattleForeignPartyModel foreign => (foreign.Presentation.Key, false),
            BattleForeignEnemyModel foreign => (foreign.Presentation.Key, false),
            _ => throw new ArgumentException("Unknown model owner.", nameof(owner))
        };
    }
}
