using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleModelTargetsTests
{
    [Fact]
    public void DistinctOwnersEnumerateInSelectionOrderWithoutAPhysicalCapacity()
    {
        BattleModelOwner[] owners = Enumerable.Range(0, 65).Reverse().Select(index => (BattleModelOwner)new BattleEnemyActorModel(new($"enemy-{index}"))).ToArray();
        BattleModelTargets selection = BattleModelTargets.Empty;
        Assert.True(selection.IsEmpty);
        Assert.Empty(selection.Enumerate());
        foreach (BattleModelOwner owner in owners)
        {
            BattleModelTargets single = BattleModelTargets.Empty.Add(owner);
            Assert.True(single.IsSingle(owner));
            Assert.False(single.IsSingle(new BattleEnemyActorModel(new("other"))));
            Assert.Same(single, single.Add(owner));
            Assert.True(single.Remove(owner).IsEmpty);
            selection = selection.Add(owner);
        }
        Assert.Equal(65, selection.Count);
        Assert.Equal(owners, selection.Enumerate());
        Assert.Equal(owners.Where(owner => owner != owners[10]), selection.Remove(owners[10]).Enumerate());
        Assert.Equal(selection, BattleModelTargets.From(owners.Concat(owners)));
    }

    [Fact]
    public void OriginalProjectionEncodesEveryAliasedSubsetAndRejectsUnaliasedOwners()
    {
        BattleModelOwner extra = new BattleEnemyActorModel(new("extra"));
        for (ushort mask = 0; mask <= 0x7F; mask++)
        {
            BattleModelTargets selection = BattleModelTargets.From(Enumerable.Range(0, 7).Where(slot => (mask & (1 << slot)) != 0).Select(OriginalOwner));
            Assert.Equal(mask, OriginalAliases.EncodeTargetMask(selection));
            Assert.Null(OriginalAliases.EncodeTargetMask(selection.Add(extra)));
        }
    }

    [Fact]
    public void SameTextInDifferentOwnerDomainsCannotAlias()
    {
        BattleModelOwner enemy = new BattleEnemyActorModel(new("same"));
        BattleModelOwner party = new BattlePartyActorModel(new("same"));
        BattleModelOwner foreign = new BattleForeignEnemyModel(new("same"));
        BattleModelTargets selection = BattleModelTargets.From([enemy, party, foreign]);
        Assert.Equal(3, selection.Count);
        Assert.Equal(new[] { enemy, party, foreign }, selection.Enumerate());
        Assert.DoesNotContain(new BattleForeignPartyModel(new("same")), selection.Enumerate());
        Assert.Throws<ArgumentNullException>(() => selection.Add(null!));
    }
}
