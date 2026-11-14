using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelLifecycleOwnerTests
{
    private static readonly BattlePartyActorModel Ally = new(new("ally"));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));

    [Fact]
    public void CompletePlannedFactsAllowIncrementalModelBinding()
    {
        BattleModelRoster roster = new([Ally, Enemy]);
        NativeBattleModelLifecycleServices services = new(Facts());

        services.Validate(roster, Graphics());
        services.Validate(roster, Graphics(Enemy));
        services.Validate(roster, Graphics(Enemy, Ally));
    }

    [Fact]
    public void NullMissingOrStaleFactsRejectEvenIfOnlyOneModelIsPresent()
    {
        BattleModelRoster roster = new([Ally, Enemy]);
        NativeBattleGraphicsState graphics = Graphics(Ally);
        BattleEnemyActorModel stale = new(new("stale"));
        ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts> facts = Facts();

        Assert.Throws<ArgumentNullException>(() => new NativeBattleModelLifecycleServices(null!));
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelLifecycleServices(facts.Remove(Enemy)).Validate(roster, graphics));
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelLifecycleServices(facts.SetItem(Enemy, null!)).Validate(roster, graphics));
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelLifecycleServices(facts.Remove(Enemy).Add(stale, new("stale", false, 40, 10, 12))).Validate(roster, graphics));
    }

    [Fact]
    public void ActorIdAndSideMustMatchTheTypedOwner()
    {
        BattleModelRoster roster = new([Ally, Enemy]);
        NativeBattleGraphicsState graphics = Graphics();
        ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts> facts = Facts();

        Assert.Throws<InvalidDataException>(() => new NativeBattleModelLifecycleServices(facts.SetItem(Ally, facts[Ally] with { ActorId = Enemy.Actor.ActorId })).Validate(roster, graphics));
        Assert.Throws<InvalidDataException>(() => new NativeBattleModelLifecycleServices(facts.SetItem(Enemy, facts[Enemy] with { IsParty = true })).Validate(roster, graphics));
    }

    [Fact]
    public void APresentModelOutsideThePlannedRosterRejects()
    {
        BattleModelRoster roster = new([Ally, Enemy]);
        BattleEnemyActorModel stale = new(new("stale"));
        NativeBattleModelLifecycleServices services = new(Facts());

        Assert.Throws<InvalidDataException>(() => services.Validate(roster, Graphics(Ally, stale)));
    }

    [Fact]
    public void ForeignPresentationCannotSupplyLogicalActorFacts()
    {
        BattleForeignEnemyModel foreign = new(new("external"));
        BattleModelRoster roster = new([Ally, foreign]);
        var facts = Facts().Remove(Enemy).Add(foreign, new("external", false, 40, 10, 12));
        NativeBattleModelLifecycleServices services = new(facts);

        Assert.Throws<InvalidDataException>(() => services.Validate(roster, Graphics(Ally, foreign)));
    }

    [Fact]
    public void ExtendedFactsAndReceiptsUseExactOwnersBeyondTwentyFive()
    {
        var facts = Facts().ToBuilder();
        for (int index = 0; index < 26; index++)
        {
            string allyId = $"extra-ally-{index}";
            string enemyId = $"extra-enemy-{index}";
            facts.Add(new BattlePartyActorModel(new(allyId)), new(allyId, true, 2, 10, 12));
            facts.Add(new BattleEnemyActorModel(new(enemyId)), new(enemyId, false, 40, 10, 12));
        }
        BattlePartyActorModel lastAlly = new(new("extra-ally-25"));
        BattleEnemyActorModel lastEnemy = new(new("extra-enemy-25"));
        BattleModelRoster roster = new(facts.Keys);
        NativeBattleModelLifecycleServices services = new(facts.ToImmutable());

        services.Validate(roster, Graphics(Ally, lastAlly, lastEnemy));
        Assert.Equal(54, services.Facts.Count);
        Assert.Equal("extra-enemy-25", services.Facts[lastEnemy].ActorId);
        NativeBattleModelParticipantReleased release = new(lastEnemy, lastEnemy.Actor.ActorId);
        NativeBattlePresentationCompletionRequested completion = new(2, 105, lastAlly, lastAlly.Actor.ActorId);
        services.Emit(release);
        services.EmitCompletion(completion);
        Assert.Equal(lastEnemy, Assert.Single(services.ReadReceipts()).Owner);
        Assert.Equal(new NativeBattlePresentationReceipt[] { release, completion }, services.ReadOrderedReceipts().ToArray());
    }

    private static ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts> Facts()
    {
        return ImmutableDictionary<BattleModelOwner, NativeBattleModelActorFacts>.Empty
            .Add(Ally, new(Ally.Actor.ActorId, true, 1, 7, 4))
            .Add(Enemy, new(Enemy.Actor.ActorId, false, 40, 6, 9));
    }

    private static NativeBattleGraphicsState Graphics(params BattleModelOwner[] owners)
    {
        BattleActorModels models = new(owners.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new NativeBattleGraphicsModel(0))));
        NativeBattleModelBoundary boundary = new(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
        return new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
    }
}
