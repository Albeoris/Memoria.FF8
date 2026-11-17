using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedSequenceSelectionTests
{
    private static readonly BattlePartyActorModel OriginalParty = new(new("original-party"));
    private static readonly BattleEnemyActorModel OriginalEnemy = new(new("original-enemy"));
    private static readonly BattleEnemyActorModel OtherEnemy = new(new("other-enemy"));
    private static readonly BattleEnemyActorModel LastNative = new(new("last-native"));
    private static readonly BattlePartyActorModel FirstAddedParty = new(new("first-added-party"));
    private static readonly BattlePartyActorModel LastAddedParty = new(new("last-added-party"));
    private static readonly BattleEnemyActorModel FirstAddedEnemy = new(new("first-added-enemy"));
    private static readonly BattleEnemyActorModel UnloadedEnemy = new(new("unloaded-added-enemy"));
    private static readonly BattleEnemyActorModel LastAddedEnemy = new(new("last-added-enemy"));
    private static readonly BattlePartyActorModel Missing = new(new("missing"));
    private static readonly BattleModelRoster Roster = new([OriginalParty, OriginalEnemy, OtherEnemy, LastNative, FirstAddedParty, LastAddedParty, FirstAddedEnemy, UnloadedEnemy, LastAddedEnemy]);
    private static readonly NativeBattleModelAliases Aliases = new(Roster,
    [
        (NativeBattleModelSlot.Decode(0), OriginalParty),
        (NativeBattleModelSlot.Decode(3), OriginalEnemy),
        (NativeBattleModelSlot.Decode(4), OtherEnemy),
        (NativeBattleModelSlot.Decode(6), LastNative)
    ]);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void Geometry92_native_literal_targets_keep_the_retained_source(byte selector)
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleActionGeometry retained = Geometry(LastAddedParty, Targets(FirstAddedEnemy, LastAddedEnemy), true);

        var result = NativeBattleSequenceSelections.GeometryArguments92(graphics.ActorModels, FirstAddedParty, selector, retained, graphics.NativeBindings);

        Assert.Equal(LastAddedParty, result.Source);
        Assert.True(result.Targets.IsSingle(Aliases.DecodeLiteral(selector)));
        Assert.False(result.SourceHasNonActorSelectors);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(255)]
    public void Geometry92_other_operands_use_current_owner_and_full_source_targets(byte selector)
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleActionGeometry retained = Geometry(LastAddedParty, Targets(OriginalEnemy, FirstAddedEnemy, LastAddedEnemy), true);

        var result = NativeBattleSequenceSelections.GeometryArguments92(graphics.ActorModels, FirstAddedParty, selector, retained, graphics.NativeBindings);

        Assert.Equal(FirstAddedParty, result.Source);
        Assert.Equal(retained.SourceTargets.Resolve(graphics.ActorModels, graphics.NativeBindings), result.Targets);
        Assert.True(result.SourceHasNonActorSelectors);
    }

    [Fact]
    public void Geometry92_keeps_non_actor_selector_fact_on_both_original_and_added_owners()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleActionGeometry retained = Geometry(OriginalParty, Targets(OtherEnemy), true);

        var added = NativeBattleSequenceSelections.GeometryArguments92(graphics.ActorModels, LastAddedParty, 7, retained, graphics.NativeBindings);
        var native = NativeBattleSequenceSelections.GeometryArguments92(graphics.ActorModels, LastNative, 7, retained, graphics.NativeBindings);

        Assert.Equal(LastAddedParty, added.Source);
        Assert.Equal(LastNative, native.Source);
        Assert.Equal(retained.SourceTargets.Resolve(graphics.ActorModels, graphics.NativeBindings), added.Targets);
        Assert.Equal(retained.SourceTargets.Resolve(graphics.ActorModels, graphics.NativeBindings), native.Targets);
        Assert.True(added.SourceHasNonActorSelectors);
        Assert.True(native.SourceHasNonActorSelectors);
    }

    [Theory]
    [InlineData(7, true, false)]
    [InlineData(4, false, true)]
    public void Session_geometry92_preserves_selector_fact_only_for_retained_selection(byte selector, bool hasNonActorSelectors, bool expectedSelf)
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(session.Presentation!, NativeBattleEffectMemory.FromColdProcess());
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleVector position = new(20, 0, 40);
        NativeBattleGraphicsModel other = graphics.ActorModels.Get(OtherEnemy) with { Flags2 = 0, ModelId4 = 20, Transform = new(position, position, 0, 0, 4096) };
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(OtherEnemy, other), ActionContext = Geometry(OtherEnemy, Targets(OtherEnemy), true) };
        MethodInfo rebind = typeof(BattleSession).GetMethod("RebindNativeSequenceGeometry92", BindingFlags.Instance | BindingFlags.NonPublic)!;

        NativeBattleGraphicsState result = (NativeBattleGraphicsState)rebind.Invoke(session, [graphics, OtherEnemy, selector])!;

        NativeBattleActionGeometry geometry = Assert.IsType<NativeBattleActionGeometry>(result.ActionContext);
        Assert.Equal(hasNonActorSelectors, geometry.SourceHasNonActorSelectors);
        Assert.True(geometry.SourceTargets.Resolve(result.ActorModels, result.NativeBindings).IsSingle(OtherEnemy));
        Assert.Equal(expectedSelf, geometry.IsSelfTarget(result.ActorModels, result.NativeBindings));
        Assert.Equal(expectedSelf, geometry.UnadjustedDistance == 1000);
    }

    [Fact]
    public void Geometry92_rejects_only_reached_unbound_source_current_or_targets()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleActionGeometry retained = Geometry(LastAddedParty, Targets(FirstAddedEnemy, LastAddedEnemy), false);
        (BattleModelOwner Source, BattleModelTargets Targets, bool SourceHasNonActorSelectors) Select(BattleModelOwner owner, byte selector, NativeBattleActionGeometry? context)
        {
            return NativeBattleSequenceSelections.GeometryArguments92(graphics.ActorModels, owner, selector, context, graphics.NativeBindings);
        }

        Assert.Throws<InvalidDataException>(() => Select(Missing, 0, retained));
        Assert.Throws<InvalidDataException>(() => Select(FirstAddedParty, 1, retained));
        BattleRetainedModelReference missing = new BattleRetainedModelReference.CanonicalOwner(Missing);
        Assert.Throws<InvalidDataException>(() => Select(FirstAddedParty, 0, retained with { Source = missing }));
        Assert.Throws<InvalidDataException>(() => Select(FirstAddedParty, 7, retained with { SourceTargets = BattleRetainedModelSelection.From([missing]) }));
        Assert.Throws<NotSupportedException>(() => Select(FirstAddedParty, 7, null));
        Assert.Equal(retained.SourceTargets.Resolve(graphics.ActorModels, graphics.NativeBindings), Select(FirstAddedParty, 7, retained with { Source = missing }).Targets);
    }

    private static NativeBattleActionGeometry Geometry(BattleModelOwner source, BattleModelTargets targets, bool hasNonActorSelectors)
    {
        return new(BattleRetainedModelReference.Capture(source, Aliases), BattleRetainedModelSelection.Capture(targets, Aliases), hasNonActorSelectors);
    }

    [Fact]
    public void Select93_searches_loaded_bound_model_ids_in_model_order_and_excludes_current()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleModelTargets Select(BattleModelOwner current, byte selector) => NativeBattleSequenceSelections.SelectModels93(graphics.ActorModels, current, selector, graphics.NativeBindings);

        Assert.Equal(Targets(OriginalParty, LastNative, LastAddedParty, LastAddedEnemy), Select(FirstAddedParty, 77));
        Assert.Equal(Targets(OriginalParty, LastNative, FirstAddedParty, LastAddedParty), Select(LastAddedEnemy, 77));
        Assert.Equal(Targets(FirstAddedEnemy), Select(FirstAddedParty, 16));
        Assert.True(Select(FirstAddedParty, 255).IsEmpty);
    }

    [Fact]
    public void Select93_literal_domain_stays_native_even_with_added_models()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleModelTargets Select(BattleModelOwner current, byte selector) => NativeBattleSequenceSelections.SelectModels93(graphics.ActorModels, current, selector, graphics.NativeBindings);

        Assert.Equal(Targets(OriginalParty), Select(FirstAddedParty, 0));
        Assert.Equal(Targets(LastNative), Select(FirstAddedParty, 6));
        Assert.Throws<NotSupportedException>(() => Select(LastAddedParty, 7));
        Assert.Throws<NotSupportedException>(() => Select(FirstAddedParty, 15));
        Assert.Throws<InvalidDataException>(() => Select(FirstAddedParty, 1));
        Assert.Throws<InvalidDataException>(() => Select(Missing, 77));
    }

    [Fact]
    public void TargetBF_uses_bound_owner_side_with_native_status_and_readiness_order()
    {
        BattleActorModels models = Graphics().ActorModels;
        bool CanSelect(BattleModelOwner owner, NativeBattleGraphicsModel model) => NativeBattleSequenceSelections.CanSelectTargetBF(models.Set(owner, model), owner);
        Assert.False(CanSelect(FirstAddedParty, new(2) { Flags8 = 0x100000 }));
        Assert.False(CanSelect(LastAddedParty, new(2) { Flags8 = 0x200000, ResourceFlags44 = 0x140 }));
        Assert.True(CanSelect(FirstAddedParty, new(2) { Flags8 = 0, ResourceFlags44 = 0x40 }));
        Assert.True(CanSelect(LastAddedEnemy, new(2) { ResourceFlags44 = 0x100 }));
        Assert.False(CanSelect(LastAddedEnemy, new(2) { Flags8 = 0x300000, ResourceFlags44 = 0x20 }));
        Assert.Throws<KeyNotFoundException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(models, Missing));
    }

    private static NativeBattleGraphicsState Graphics()
    {
        return NativeBattleHostedTestFixture.Graphics(Roster, Aliases, owner =>
        {
            if (owner == FirstAddedEnemy)
            {
                return new(2) { ModelId4 = 16 };
            }
            if (owner == UnloadedEnemy || owner == OriginalEnemy || owner == OtherEnemy)
            {
                return new(0) { ModelId4 = 77 };
            }
            return new(2) { ModelId4 = 77 };
        });
    }

    private static BattleModelTargets Targets(params BattleModelOwner[] owners) => BattleModelTargets.From(owners);
}
