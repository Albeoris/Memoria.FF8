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
            BattleModelRoster.RegisterIdentity(entry.Owner, identities);
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
    public BattleActorModels(IEnumerable<BattleModelEntry<BattleModelOwner>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var order = ImmutableArray.CreateBuilder<BattleModelOwner>();
        var party = ImmutableArray.CreateBuilder<BattleModelEntry<BattlePartyModelOwner>>();
        var enemies = ImmutableArray.CreateBuilder<BattleModelEntry<BattleEnemyModelOwner>>();
        foreach (BattleModelEntry<BattleModelOwner> entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
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
        Roster = new(order);
        Party = new(party);
        Enemies = new(enemies);
    }

    private BattleActorModels(BattleModelPool<BattlePartyModelOwner> party, BattleModelPool<BattleEnemyModelOwner> enemies, BattleModelRoster roster)
    {
        Party = party;
        Enemies = enemies;
        Roster = roster;
    }

    public BattleModelPool<BattlePartyModelOwner> Party { get; }
    public BattleModelPool<BattleEnemyModelOwner> Enemies { get; }
    public BattleModelRoster Roster { get; }
    public IEnumerable<BattleModelOwner> Owners => Roster.Owners;
    public IEnumerable<BattleModelEntry<BattleModelOwner>> Entries => Owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, Get(owner)));

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

    public bool Contains(BattleModelOwner owner) => Roster.Contains(owner);

    public BattleActorModels Set(BattleModelOwner owner, NativeBattleGraphicsModel state)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(state);
        return owner switch
        {
            BattlePartyModelOwner party => new(Party.Set(party, state), Enemies, Roster),
            BattleEnemyModelOwner enemy => new(Party, Enemies.Set(enemy, state), Roster),
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
}
