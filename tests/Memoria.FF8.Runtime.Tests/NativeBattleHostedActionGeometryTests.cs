namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionGeometryTests
{
    private static readonly BattleEnemyActorModel OriginalEnemy = new(new("enemy"));
    private static readonly BattlePartyActorModel FirstAlly = new(new("ally-7"));
    private static readonly BattlePartyActorModel LastAlly = new(new("ally-16"));
    private static readonly BattleEnemyActorModel ExtraEnemy = new(new("enemy-24"));

    [Fact]
    public void Group_uses_mixed_sides_and_registered_order_instead_of_selection_order()
    {
        NativeBattleGraphicsState initial = CreateGraphics();
        BattleModelTargets selection = BattleModelTargets.From([ExtraEnemy, LastAlly, FirstAlly]);
        NativeBattleGraphicsState graphics = Prepare(initial, OriginalEnemy, BattleModelTargets.From([OriginalEnemy, FirstAlly]), selection);
        NativeBattleActionGeometry context = graphics.ActionContext!;

        context.Validate();
        Assert.Equal(BattleModelTargets.From([FirstAlly, LastAlly, ExtraEnemy]), context.TargetActors);
        Assert.Equal(ExtraEnemy, context.Target);
        Assert.Equal((short)2048, graphics.ActorModels.Get(OriginalEnemy).Rotation14);
        Assert.Equal((ushort)4, graphics.ActorModels.Get(OriginalEnemy).Flags2);
        foreach (BattleModelOwner owner in selection.Enumerate())
        {
            Assert.Equal((ushort)0x14, graphics.ActorModels.Get(owner).Flags2);
        }
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter([Position(7), Position(16), Position(24)]);
        Assert.Equal(center, NativeBattlePoseMatrices.TransformCameraPoint(context.TargetMatrix!, default, false));
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(0, 0, 0)).ToArray(), context.TargetMatrix!.Rotation.ToArray());

        NativeBattleGraphicsState partySource = Prepare(initial, FirstAlly, BattleModelTargets.Empty, BattleModelTargets.From([OriginalEnemy, ExtraEnemy]));
        Assert.Equal((short)0, partySource.ActorModels.Get(FirstAlly).Rotation14);
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(0, 2048, 0)).ToArray(), partySource.ActionContext!.TargetMatrix!.Rotation.ToArray());
    }

    [Fact]
    public void An_extra_model_can_target_itself_without_a_native_projection()
    {
        BattleModelTargets selection = BattleModelTargets.From([ExtraEnemy]);
        NativeBattleGraphicsState graphics = Prepare(CreateGraphics(), ExtraEnemy, selection, selection);
        NativeBattleActionGeometry context = graphics.ActionContext!;

        context.Validate();
        Assert.True(context.IsSelfTarget);
        Assert.Equal(1000, context.UnadjustedDistance);
        Assert.Equal(1000, context.ContactDistance);
        Assert.Equal(selection, context.SourceTargets);
        Assert.Equal(selection, context.TargetActors);
        Assert.Equal(ExtraEnemy, context.Target);
        Assert.False((context with { SourceTargets = selection.Add(LastAlly) }).IsSelfTarget);
        Assert.False((context with { SourceHasNonActorSelectors = true }).IsSelfTarget);
        Assert.Equal(5, context.Matrices!.Value.Length);
    }

    [Fact]
    public void More_than_twenty_five_targets_contribute_to_group_flags_and_bounds()
    {
        NativeBattleGraphicsState initial = CreateGraphics();
        BattleModelOwner[] extras = Enumerable.Range(25, 27).Select(index => new BattleEnemyActorModel(new($"enemy-{index}"))).ToArray();
        BattleActorModels models = initial.ActorModels;
        for (int index = 0; index < extras.Length; index++)
        {
            models = models.Add(extras[index], State(Position(index + 25), BattleSide.Enemies));
        }
        BattleModelOwner[] ordered = [FirstAlly, ExtraEnemy, .. extras];
        BattleModelTargets requested = BattleModelTargets.From(ordered.Reverse());
        NativeBattleGraphicsState result = Prepare(initial with { ActorModels = models }, OriginalEnemy, BattleModelTargets.Empty, requested);

        Assert.Equal(29, result.ActionContext!.TargetActors!.Count);
        Assert.Equal(ordered, result.ActionContext.TargetActors.Enumerate());
        Assert.Equal(extras[^1], result.ActionContext.Target);
        Assert.All(ordered, owner => Assert.Equal((ushort)0x14, result.ActorModels.Get(owner).Flags2));
        Assert.Equal((ushort)4, models.Get(extras[^1]).Flags2);
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(ordered.Select(owner => models.Get(owner).Transform!.Position28).ToArray());
        Assert.Equal(center, NativeBattlePoseMatrices.TransformCameraPoint(result.ActionContext.TargetMatrix!, default, false));
    }

    [Fact]
    // The native script operand adapter is pending; this witness retains the original observed values.
    public void Hosted_camera_counts_the_explicit_party_and_all_selected_targets()
    {
        NativeBattleGraphicsState graphics = Prepare(CreateGraphics(), OriginalEnemy, BattleModelTargets.From([OriginalEnemy]), BattleModelTargets.From([OriginalEnemy, FirstAlly, LastAlly, ExtraEnemy]));
        Assert.Equal(2, NativeBattleCameraGraphics.ReadVariable(graphics, 19));
        Assert.Equal(1, NativeBattleCameraGraphics.ReadVariable(graphics, 27));
        Assert.Equal(4, NativeBattleCameraGraphics.ReadVariable(graphics, 26));
        Assert.Equal(24, NativeBattleCameraGraphics.ReadVariable(graphics, 25));
        Assert.Equal(0x20, NativeBattleCameraGraphics.ReadVariable(graphics, 22));

        graphics = Prepare(graphics, OriginalEnemy, BattleModelTargets.From([OriginalEnemy]), BattleModelTargets.From([LastAlly]));
        Assert.Equal(0x21, NativeBattleCameraGraphics.ReadVariable(graphics, 22));
    }

    [Fact]
    public void Geometry_rejects_unbound_owners_and_empty_resolved_targets_before_reading_bones()
    {
        NativeBattleGraphicsState initial = CreateGraphics();
        BattleEnemyActorModel other = new(new("unbound"));
        BattleModelTargets bound = BattleModelTargets.From([FirstAlly]);
        BattleModelTargets unbound = BattleModelTargets.From([other]);
        Assert.Throws<InvalidDataException>(() => Reject(other, bound, bound));
        Assert.Throws<InvalidDataException>(() => Reject(OriginalEnemy, unbound, bound));
        Assert.Throws<InvalidDataException>(() => Reject(OriginalEnemy, bound, bound.Add(other)));
        Assert.Throws<InvalidDataException>(() => Reject(OriginalEnemy, bound, BattleModelTargets.Empty));

        void Reject(BattleModelOwner source, BattleModelTargets sourceTargets, BattleModelTargets targets)
        {
            NativeBattleActionGeometryRules.PrepareGeometry50D300(initial, source, sourceTargets, targets, NativeBattleActionGeometryGlobals.FromColdProcess(), (_, _, _, _) => throw new InvalidOperationException("Unexpected bone read."));
        }
    }

    private static NativeBattleGraphicsState Prepare(NativeBattleGraphicsState graphics, BattleModelOwner source, BattleModelTargets sourceTargets, BattleModelTargets targets)
    {
        return NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, source, sourceTargets, targets, NativeBattleActionGeometryGlobals.FromColdProcess(), (state, owner, _, _) => state.ActorModels.Get(owner).Transform!.Position28);
    }

    private static NativeBattleGraphicsState CreateGraphics()
    {
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(null);
        BattleActorModels models = new([]);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), OriginalEnemy, models);
        models = models.Set(OriginalEnemy, State(Position(0), OriginalEnemy.Side))
            .Add(FirstAlly, State(Position(7), FirstAlly.Side))
            .Add(LastAlly, State(Position(16), LastAlly.Side))
            .Add(ExtraEnemy, State(Position(24), ExtraEnemy.Side));
        return new(models, boundary, 0, 0);
    }

    private static NativeBattleGraphicsModel State(NativeBattleVector position, BattleSide side)
    {
        return new(2)
        {
            Flags2 = 4,
            Flags8 = position == Position(16) || position == Position(24) ? 0x20u : 0u,
            ModelId4 = side == BattleSide.Allies ? (byte)1 : (byte)20,
            ResourceFlags44 = 1,
            Rotation12 = 0,
            Rotation14 = 0,
            Rotation16 = 0,
            SequenceState = new(),
            Transform = new(position, position, 0, position.Y, 4096)
        };
    }

    private static NativeBattleVector Position(int position) => new((short)(position * 300), (short)(position * 10), (short)(position * -100));
}
