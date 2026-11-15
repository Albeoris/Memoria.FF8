namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedActionBusyTests
{
    [Fact]
    public void OrdinaryRosterPreservesCameraAndSelectedModelRules()
    {
        BattleActorModels models = Models(3, 4);
        BattleModelOwner source = models.Party.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner target = models.Enemies.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner other = models.Enemies.Entries.Select(entry => entry.Owner).Last();
        NativeBattleActionGraphicsState context = Action(source, target);
        Assert.False(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context));

        models = models.Set(target, Ready() with { ResourceFlags44 = 0x100 });
        Assert.False(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { UseActionCamera = true }));

        models = models.Set(target, Ready()).Set(other, Busy());
        Assert.False(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { UseActionCamera = true }));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { CoverActors = Targets([other]) }));

        models = models.Set(other, new NativeBattleGraphicsModel(0));
        Assert.False(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { UseActionCamera = true }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothWaitModesReachLastExtendedOwnerOnEitherSide(bool party)
    {
        BattleActorModels models = Models(28, 29);
        BattleModelOwner source = models.Party.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner last = party ? models.Party.Entries.Select(entry => entry.Owner).Last() : models.Enemies.Entries.Select(entry => entry.Owner).Last();
        models = models.Set(last, Busy());

        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source, last)));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source) with { UseActionCamera = true }));
        Assert.False(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source)));
    }

    [Fact]
    public void BusySourceShortCircuitsUnknownSelectedModelReadiness()
    {
        BattleActorModels models = Models(3, 4);
        BattleModelOwner earlier = models.Party.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner source = models.Party.Entries.Select(entry => entry.Owner).Last();
        models = models.Set(source, Busy()).Set(earlier, new NativeBattleGraphicsModel(2));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source, earlier)));
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGraphics.AreActionModelsBusy(models.Set(source, Ready()), Boundary, Action(source, earlier)));
    }

    [Fact]
    public void SelectedModelChecksUseRosterOrderInsteadOfTargetOrder()
    {
        BattleActorModels models = Models(3, 4);
        BattleModelOwner source = models.Party.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner earlier = models.Enemies.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner later = models.Enemies.Entries.Select(entry => entry.Owner).Last();
        models = models.Set(earlier, Busy()).Set(later, new NativeBattleGraphicsModel(2));

        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source, later, earlier)));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source, later) with { CoverActors = Targets([earlier]) }));
        Assert.True(NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source) with { UseActionCamera = true }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetiredSourceTargetAndCoverRejectBeforeAnyReadinessCheck(bool useActionCamera)
    {
        BattleActorModels models = Models(3, 4);
        BattleModelOwner source = models.Party.Entries.Select(entry => entry.Owner).First();
        BattleModelOwner retired = models.Enemies.Entries.Select(entry => entry.Owner).Last();
        models = models.Set(source, new NativeBattleGraphicsModel(2)).ReplaceOwner(retired, new BattleEnemyActorModel(new("replacement")), Ready());
        NativeBattleActionGraphicsState context = Action(source) with { UseActionCamera = useActionCamera };

        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { Actor = new BattleRetainedModelReference.CanonicalOwner(retired) }));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { TargetActors = Targets([retired]) }));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, context with { CoverActors = Targets([retired]) }));
    }

    [Fact]
    public void MissingCameraDecisionRejectsBeforeReadiness()
    {
        BattleActorModels models = Models(3, 4);
        BattleModelOwner source = models.Party.Entries.Select(entry => entry.Owner).First();
        models = models.Set(source, new NativeBattleGraphicsModel(2));

        Assert.Throws<InvalidDataException>(() => NativeBattleActionGraphics.AreActionModelsBusy(models, Boundary, Action(source) with { UseActionCamera = null }));
    }

    private static BattleActorModels Models(int partyCount, int enemyCount)
    {
        IEnumerable<BattleModelOwner> party = Enumerable.Range(0, partyCount).Select(index => new BattlePartyActorModel(new($"party-{index}")));
        IEnumerable<BattleModelOwner> enemies = Enumerable.Range(0, enemyCount).Select(index => new BattleEnemyActorModel(new($"enemy-{index}")));
        return new(party.Concat(enemies).Select(owner => new BattleModelEntry<BattleModelOwner>(owner, Ready())));
    }

    private static readonly NativeBattleModelBoundary Boundary = NativeBattleModelBoundary.FromRetainedMemory(null);

    private static BattleRetainedModelSelection Targets(IEnumerable<BattleModelOwner> owners) => BattleRetainedModelSelection.From(owners.Select(owner => new BattleRetainedModelReference.CanonicalOwner(owner)));

    private static NativeBattleGraphicsModel Ready() => new(2) { Flags8 = 0, Kind5 = 0, ResourceFlags44 = 0x140 };

    private static NativeBattleGraphicsModel Busy() => Ready() with { ResourceFlags44 = 0 };

    private static NativeBattleActionGraphicsState Action(BattleModelOwner source, params BattleModelOwner[] targets) =>
        new(new(0), new BattleRetainedModelReference.CanonicalOwner(source), new(0), Targets(targets), BattleRetainedModelSelection.Empty, null, null, 0, null, 0) { UseActionCamera = false };
}
