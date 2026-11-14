namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleTypedModelOwnershipTests
{
    [Fact]
    public void Actor_and_model_identities_have_fixed_sides_and_ordinal_equality()
    {
        BattlePartyActorReference party = new("actor");
        BattleEnemyActorReference enemy = new("actor");
        Assert.Equal(BattleSide.Allies, party.Side);
        Assert.Equal(BattleSide.Enemies, enemy.Side);
        Assert.Equal(party, new BattlePartyActorReference("actor"));
        Assert.NotEqual<BattleActorReference>(party, enemy);
        Assert.NotEqual(party, new BattlePartyActorReference("Actor"));
        BattlePartyActorModel model = new(party);
        Assert.Equal(BattleSide.Allies, model.Side);
        Assert.Equal(model, new BattlePartyActorModel(new("actor")));
        Assert.NotEqual<BattleModelOwner>(model, new BattleForeignPartyModel(new("actor")));
        Assert.Equal(BattleSide.Enemies, new BattleForeignEnemyModel(new("proxy")).Side);
        Assert.Throws<ArgumentException>(() => new BattlePartyActorReference(" "));
        Assert.Throws<ArgumentException>(() => new BattleEnemyActorReference(""));
        Assert.Throws<ArgumentException>(() => new BattleForeignPresentationKey("\t"));
        Assert.Throws<ArgumentNullException>(() => new BattlePartyActorModel(null!));
        Assert.Throws<ArgumentNullException>(() => new BattleEnemyActorModel(null!));
        Assert.Throws<ArgumentNullException>(() => new BattleForeignPartyModel(null!));
        Assert.Throws<ArgumentNullException>(() => new BattleForeignEnemyModel(null!));
    }

    [Fact]
    public void Side_pools_preserve_their_order_and_global_entries_preserve_interleaving()
    {
        BattleEnemyActorModel enemy = new(new("enemy"));
        BattlePartyActorModel party = new(new("party"));
        BattleForeignEnemyModel foreignEnemy = new(new("enemy-proxy"));
        BattleForeignPartyModel foreignParty = new(new("party-proxy"));
        NativeBattleGraphicsModel physicalState = new(0);
        BattleActorModels models = new([new(enemy, physicalState), new(party, physicalState), new(foreignEnemy, physicalState), new(foreignParty, physicalState)]);

        Assert.Equal<BattleModelOwner>([enemy, party, foreignEnemy, foreignParty], models.Owners);
        Assert.Equal(models.Owners, models.Entries.Select(entry => entry.Owner));
        Assert.Equal<BattlePartyModelOwner>([party, foreignParty], models.Party.Entries.Select(entry => entry.Owner));
        Assert.Equal<BattleEnemyModelOwner>([enemy, foreignEnemy], models.Enemies.Entries.Select(entry => entry.Owner));
        Assert.True(models.Enemies.TryGet(new BattleEnemyActorModel(new("enemy")), out NativeBattleGraphicsModel? state));
        Assert.Same(physicalState, state);
        Assert.Same(physicalState, models.Get(foreignParty));
        Assert.False(models.Contains(new BattlePartyActorModel(new("foreign"))));
    }

    [Fact]
    public void Owner_uniqueness_spans_both_sides_but_keeps_actor_and_foreign_domains_distinct()
    {
        BattlePartyActorModel actor = new(new("shared"));
        BattleEnemyActorModel sameActor = new(new("shared"));
        BattleForeignEnemyModel foreign = new(new("shared"));
        BattleForeignPartyModel sameForeign = new(new("shared"));
        NativeBattleGraphicsModel state = new(0);

        BattleActorModels models = new([new(actor, state), new(foreign, state)]);
        Assert.True(models.Contains(actor));
        Assert.True(models.Contains(foreign));
        Assert.Throws<ArgumentException>(() => new BattleActorModels([new(actor, state), new(sameActor, state)]));
        Assert.Throws<ArgumentException>(() => new BattleActorModels([new(foreign, state), new(sameForeign, state)]));
        Assert.Throws<ArgumentException>(() => models.Add(actor, state));
        Assert.Throws<ArgumentException>(() => models.Add(sameActor, state));
        Assert.Throws<ArgumentException>(() => models.ReplaceOwner(foreign, sameActor, state));
        Assert.Throws<ArgumentException>(() => new BattleModelPool<BattleModelOwner>([new(actor, state), new(sameActor, state)]));
        Assert.Throws<ArgumentException>(() => new BattleModelPool<BattlePartyModelOwner>([new(actor, state), new(actor, state)]));
        Assert.Throws<ArgumentNullException>(() => new BattleModelEntry<BattleModelOwner>(actor, null!));
        Assert.Throws<ArgumentNullException>(() => new BattleActorModels([null!]));
    }

    [Fact]
    public void Add_set_replace_and_remove_leave_earlier_model_versions_intact()
    {
        BattlePartyActorModel first = new(new("party"));
        BattleEnemyActorModel retired = new(new("enemy-allocation-1"));
        BattleEnemyActorModel current = new(new("enemy-allocation-2"));
        BattlePartyActorModel last = new(new("last"));
        NativeBattleGraphicsModel state = new(0);
        NativeBattleGraphicsModel updated = state with { Flags0 = 2 };
        BattleActorModels empty = new([]);
        BattleActorModels initial = empty.Add(first, state).Add(retired, state).Add(last, state);
        BattleActorModels replaced = initial.ReplaceOwner(retired, current, updated);
        BattleActorModels set = replaced.Set(first, updated);
        BattleActorModels removed = set.Remove(last);

        Assert.Empty(empty.Owners);
        Assert.Equal<BattleModelOwner>([first, retired, last], initial.Owners);
        Assert.Equal<BattleModelOwner>([first, current, last], replaced.Owners);
        Assert.Equal<BattleModelOwner>([first, current], removed.Owners);
        Assert.Same(state, initial.Get(retired));
        Assert.False(initial.Contains(current));
        Assert.False(replaced.Contains(retired));
        Assert.False(removed.Contains(last));
        Assert.Same(state, replaced.Get(first));
        Assert.Same(updated, set.Get(first));
        Assert.Same(updated, replaced.Get(current));
        Assert.Throws<KeyNotFoundException>(() => replaced.Get(retired));
        Assert.Throws<KeyNotFoundException>(() => replaced.Set(retired, state));
        Assert.Throws<KeyNotFoundException>(() => replaced.Remove(retired));
        Assert.Throws<ArgumentException>(() => initial.ReplaceOwner(retired, retired, state));
    }

    [Fact]
    public void Targets_coalesce_equal_owners_in_first_occurrence_order_beyond_native_capacity()
    {
        BattleModelOwner[] owners = Enumerable.Range(0, 40).Select(index => new BattleEnemyActorModel(new($"enemy-{index}"))).ToArray();
        BattleModelOwner repeated = new BattleEnemyActorModel(new("enemy-0"));
        BattleModelTargets targets = BattleModelTargets.From([owners[0], repeated, .. owners.Skip(1), owners[^1]]);
        BattleModelTargets equaled = BattleModelTargets.From(Enumerable.Range(0, 40).Select(index => new BattleEnemyActorModel(new($"enemy-{index}"))));
        BattleModelOwner foreign = new BattleForeignEnemyModel(new("enemy-0"));
        BattleModelTargets modified = targets.Remove(repeated).Add(foreign);

        Assert.Equal(40, targets.Count);
        Assert.Equal(owners, targets.Enumerate());
        Assert.Equal(equaled, targets);
        Assert.Equal(equaled.GetHashCode(), targets.GetHashCode());
        Assert.NotEqual(targets, BattleModelTargets.From(owners.Reverse()));
        Assert.True(targets.Contains(repeated));
        Assert.False(modified.Contains(repeated));
        Assert.Equal(owners.Skip(1).Append(foreign), modified.Enumerate());
        Assert.Same(targets, targets.Add(repeated));
        Assert.False(targets.IsSingle(repeated));
        BattleModelTargets one = BattleModelTargets.Empty.Add(repeated);
        Assert.True(one.IsSingle(owners[0]));
        Assert.Same(BattleModelTargets.Empty, one.Remove(owners[0]));
        Assert.True(BattleModelTargets.Empty.IsEmpty);
        Assert.Throws<ArgumentNullException>(() => BattleModelTargets.From([null!]));
    }
}
