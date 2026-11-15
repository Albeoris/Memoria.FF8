namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedSequenceSelectionTests
{
    [Fact]
    public void Selector92UsesRetainedSourceAndExactOriginalLiteral()
    {
        BattleEnemyActorModel current = new(new("current"));
        BattlePartyActorModel source = new(new("source"));
        BattleEnemyActorModel target = new(new("literal-target"));
        BattleActorModels models = new([new(current, new(0)), new(source, new(0))]);
        NativeBattleModelBoundary boundary = EmptyBoundary();
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(6), target, models);
        NativeBattleActionGeometry retained = new(Canonical(source), BattleRetainedModelSelection.From([Canonical(current)]), true);

        var selected = NativeBattleSequenceSelections.GeometryArguments92(models, current, 6, retained, boundary);
        Assert.Equal(source, selected.Source);
        Assert.True(selected.Targets.IsSingle(target));
        Assert.False(selected.SourceHasNonActorSelectors);
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(models, current, 3, retained, boundary));
    }

    [Theory]
    [InlineData((byte)7, true)]
    [InlineData((byte)255, false)]
    public void Selector92UsesCurrentSourceAndFullRetainedTargetsAfterLiteralRange(byte selector, bool hasNonActorSelectors)
    {
        BattlePartyActorModel current = new(new("current"));
        BattlePartyActorModel retired = new(new("retired"));
        BattleModelOwner[] selected = Enumerable.Range(0, 40).Select(index => new BattleEnemyActorModel(new($"enemy-{index}"))).Reverse().ToArray();
        BattleActorModels models = new(selected.Reverse().Select(owner => new BattleModelEntry<BattleModelOwner>(owner, new(0))).Append(new(current, new(0))));
        NativeBattleActionGeometry retained = new(Canonical(retired), BattleRetainedModelSelection.From(selected.Select(Canonical)), hasNonActorSelectors);
        NativeBattleModelBoundary boundary = EmptyBoundary();

        var result = NativeBattleSequenceSelections.GeometryArguments92(models, current, selector, retained, boundary);
        Assert.Equal(current, result.Source);
        Assert.Equal(selected, result.Targets.Enumerate());
        Assert.Equal(hasNonActorSelectors, result.SourceHasNonActorSelectors);
        var empty = NativeBattleSequenceSelections.GeometryArguments92(models, current, selector, retained with { SourceTargets = BattleRetainedModelSelection.From([]) }, boundary);
        Assert.True(empty.Targets.IsEmpty);
    }

    [Fact]
    public void Selector92RequiresOnlyReachedOwnersAndItsRetainedContext()
    {
        BattlePartyActorModel current = new(new("current"));
        BattlePartyActorModel retired = new(new("retired"));
        BattleEnemyActorModel target = new(new("target"));
        BattleActorModels models = new([new(current, new(0))]);
        NativeBattleModelBoundary boundary = EmptyBoundary();
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), target, models);
        NativeBattleActionGeometry retained = new(Canonical(current), BattleRetainedModelSelection.From([new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(3))]), false);
        BattleActorModels missingLiteral = models.Remove(target);

        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(models, retired, 7, retained, boundary));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(models, current, 3, retained with { Source = Canonical(retired) }, boundary));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(missingLiteral, current, 3, retained, boundary));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(missingLiteral, current, 7, retained, boundary));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.GeometryArguments92(models, current, 7, null, boundary));
        Assert.Throws<ArgumentNullException>(() => NativeBattleSequenceSelections.GeometryArguments92(models, current, 7, retained, null!));
    }

    private static BattleRetainedModelReference Canonical(BattleModelOwner owner) => new BattleRetainedModelReference.CanonicalOwner(owner);

    [Fact]
    public void Selector93DecodesOnlyAuthoredLiteralSlots()
    {
        BattleEnemyActorModel added = new(new("added"));
        BattlePartyActorModel ally = new(new("ally"));
        BattleEnemyActorModel enemy = new(new("enemy"));
        BattleActorModels models = new([new(added, new(0))]);
        NativeBattleModelBoundary boundary = EmptyBoundary();
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(6), enemy, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(2), ally, models);

        Assert.True(NativeBattleSequenceSelections.SelectModels93(models, added, 2, boundary).IsSingle(ally));
        Assert.True(NativeBattleSequenceSelections.SelectModels93(models, added, 6, boundary).IsSingle(enemy));
        Assert.True(NativeBattleSequenceSelections.SelectModels93(models, ally, 2, boundary).IsSingle(ally));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.SelectModels93(models, added, 0, boundary));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.SelectModels93(models, added, 3, boundary));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(models, added, 7, boundary));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(models, added, 15, boundary));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.SelectModels93(models, new BattleEnemyActorModel(new("retired")), 2, boundary));
    }

    [Fact]
    public void Selector93MatchesLoadedModelIdsInGlobalOwnerOrder()
    {
        BattleEnemyActorModel current = new(new("current"));
        BattlePartyActorModel first = new(new("first"));
        BattleEnemyActorModel unloaded = new(new("unloaded"));
        BattleEnemyActorModel last = new(new("last"));
        IEnumerable<BattleModelEntry<BattleModelOwner>> otherEntries = Enumerable.Range(0, 30).Select(index => new BattleModelEntry<BattleModelOwner>(new BattleEnemyActorModel(new($"other-{index}")), new(2) { ModelId4 = 16 }));
        BattleActorModels models = new([new(current, new(2)), new(first, new(2) { ModelId4 = 77 }), new(unloaded, new(0)), .. otherEntries, new(last, new(2) { ModelId4 = 77 })]);
        NativeBattleModelBoundary boundary = EmptyBoundary();

        BattleModelTargets targets = NativeBattleSequenceSelections.SelectModels93(models, current, 77, boundary);
        Assert.Equal<BattleModelOwner>([first, last], targets.Enumerate());
        Assert.True(NativeBattleSequenceSelections.SelectModels93(models, current, 255, boundary).IsEmpty);
        BattleActorModels unknown = models.Set(unloaded, new(2));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(unknown, current, 77, boundary));
    }

    [Fact]
    public void SelectorBFAppliesPartyStatusBeforeReadingResourceReadiness()
    {
        BattlePartyActorModel party = new(new("party"));
        BattleEnemyActorModel enemy = new(new("enemy"));
        BattleActorModels models = new([new(enemy, new(2) { ResourceFlags44 = 0x100 }), new(party, new(2) { Flags8 = 0x300000 })]);

        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(models, party));
        Assert.True(NativeBattleSequenceSelections.CanSelectTargetBF(models, enemy));
        Assert.True(NativeBattleSequenceSelections.CanSelectTargetBF(models.Set(party, new(2) { Flags8 = 0, ResourceFlags44 = 0x40 }), party));
        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(models.Set(enemy, new(2) { Flags8 = 0x300000, ResourceFlags44 = 0x20 }), enemy));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(models.Set(party, new(2)), party));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(models.Set(enemy, new(2)), enemy));
        Assert.Throws<KeyNotFoundException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(models, new BattlePartyActorModel(new("enemy"))));
    }

    private static NativeBattleModelBoundary EmptyBoundary() => new(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
}
