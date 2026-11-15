using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCastingCoverTests
{
    [Fact]
    public void Cover_copies_only_the_original_X_Z_words_and_restores_each_model_at_completion()
    {
        (BattleActorModels models, NativeBattleModelBoundary boundary) = Models();
        NativeBattleActionGraphicsState action = Action(1, 0, 2);
        BattleActorModels entered = NativeBattleCastingCover.Enter(models, boundary, action);
        Assert.Equal(OriginalOwner(2), NativeBattleCastingCover.Recipient(models, action.TargetActors.Resolve(OriginalAliases)));
        Assert.Equal(new NativeBattleVector(101, 302, 201), entered.Get(OriginalOwner(2)).Transform!.Position28);
        Assert.Equal(new NativeBattleVector(401, 301, 1001), entered.Get(OriginalOwner(1)).Transform!.Position28);
        Assert.Equal(models.Get(OriginalOwner(0)), entered.Get(OriginalOwner(0)));
        Assert.Equal(models.Get(OriginalOwner(2)).ResourceFlags44, entered.Get(OriginalOwner(2)).ResourceFlags44);
        BattleActorModels completed = NativeBattleCastingCover.Complete(entered, boundary, action);
        Assert.Equal(new NativeBattleVector(102, 302, 202), completed.Get(OriginalOwner(2)).Transform!.Position28);
        Assert.Equal(new NativeBattleVector(401, 301, 201), completed.Get(OriginalOwner(1)).Transform!.Position28);
        Assert.Equal((ushort)0x241, completed.Get(OriginalOwner(2)).ResourceFlags44);
        Assert.Equal(models.Get(OriginalOwner(2)).Transform!.Initial20, completed.Get(OriginalOwner(2)).Transform!.Initial20);
    }

    [Fact]
    public void Cover_source_and_recipient_can_alias_and_the_native_WORD_addition_wraps()
    {
        (BattleActorModels models, NativeBattleModelBoundary boundary) = Models();
        NativeBattleModelTransform transform = models.Get(OriginalOwner(1)).Transform! with { Initial20 = new(100, 500, 32500) };
        models = models.Set(OriginalOwner(1), models.Get(OriginalOwner(1)) with { Transform = transform });
        NativeBattleActionGraphicsState action = Action(1, 1);
        BattleActorModels entered = NativeBattleCastingCover.Enter(models, boundary, action);
        Assert.Equal(new NativeBattleVector(100, 301, -32236), entered.Get(OriginalOwner(1)).Transform!.Position28);
        BattleActorModels completed = NativeBattleCastingCover.Complete(entered, boundary, action);
        Assert.Equal(new NativeBattleVector(100, 301, 32500), completed.Get(OriginalOwner(1)).Transform!.Position28);
        Assert.Equal((ushort)0x241, completed.Get(OriginalOwner(1)).ResourceFlags44);
    }

    [Fact]
    public void Unknown_retained_recipient_or_geometry_is_not_fabricated()
    {
        (BattleActorModels models, NativeBattleModelBoundary boundary) = Models();
        Assert.Throws<NotSupportedException>(() => NativeBattleCastingCover.Enter(models, boundary, Action(1, 3)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCastingCover.Enter(models.Set(OriginalOwner(1), models.Get(OriginalOwner(1)) with { Transform = null }), boundary, Action(1, 2)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCastingCover.Complete(models.Set(OriginalOwner(2), models.Get(OriginalOwner(2)) with { ResourceFlags44 = null }), boundary, Action(1, 2)));
    }

    private static NativeBattleActionGraphicsState Action(int source, params int[] targets)
    {
        BattleModelOwner owner = OriginalOwner(source);
        return new(new BattleActionHeaderReference(0), BattleRetainedModelReference.Capture(OriginalOwner(0), OriginalAliases), new BattleContactCursor(0), BattleRetainedModelSelection.Capture(BattleModelTargets.From(targets.Select(OriginalOwner)), OriginalAliases), BattleRetainedModelSelection.From([BattleRetainedModelReference.Capture(owner, OriginalAliases)]), BattleRetainedModelReference.Capture(owner, OriginalAliases), BattleRetainedModelReference.Capture(OriginalOwner(2), OriginalAliases), 0, null, 0x50A9A0);
    }

    private static (BattleActorModels Models, NativeBattleModelBoundary Boundary) Models()
    {
        return OriginalModels(Enumerable.Range(0, 7).Select(index => (index, new NativeBattleGraphicsModel(3)
        {
            ResourceFlags44 = 0x41,
            Transform = new(new((short)(100 + index), (short)(500 + index), (short)(200 + index)), new((short)(400 + index), (short)(300 + index), (short)(600 + index)), 80, 4096, 4096)
        })));
    }
}
