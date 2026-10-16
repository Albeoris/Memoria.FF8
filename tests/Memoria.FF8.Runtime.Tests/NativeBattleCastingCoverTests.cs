using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCastingCoverTests
{
    [Fact]
    public void Cover_copies_only_the_original_X_Z_words_and_restores_each_model_at_completion()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        NativeBattleActionGraphicsState action = Action(1, 5);
        ImmutableArray<NativeBattleGraphicsModel> entered = NativeBattleCastingCover.Enter(models, action);
        Assert.Equal((byte)2, NativeBattleCastingCover.Recipient(action.TargetMask));
        Assert.Equal(new NativeBattleVector(101, 302, 201), entered[2].Transform!.Position28);
        Assert.Equal(new NativeBattleVector(401, 301, 1001), entered[1].Transform!.Position28);
        Assert.Equal(models[0], entered[0]);
        Assert.Equal(models[2].ResourceFlags44, entered[2].ResourceFlags44);
        ImmutableArray<NativeBattleGraphicsModel> restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleGraphicsModel>>(JsonSerializer.Serialize(entered));
        ImmutableArray<NativeBattleGraphicsModel> completed = NativeBattleCastingCover.Complete(restored, action);
        Assert.Equal(new NativeBattleVector(102, 302, 202), completed[2].Transform!.Position28);
        Assert.Equal(new NativeBattleVector(401, 301, 201), completed[1].Transform!.Position28);
        Assert.Equal((ushort)0x241, completed[2].ResourceFlags44);
        Assert.Equal(models[2].Transform!.Initial20, completed[2].Transform!.Initial20);
    }

    [Fact]
    public void Cover_source_and_recipient_can_alias_and_the_native_WORD_addition_wraps()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        NativeBattleModelTransform transform = models[1].Transform! with { Initial20 = new(100, 500, 32500) };
        models = models.SetItem(1, models[1] with { Transform = transform });
        NativeBattleActionGraphicsState action = Action(1, 2);
        ImmutableArray<NativeBattleGraphicsModel> entered = NativeBattleCastingCover.Enter(models, action);
        Assert.Equal(new NativeBattleVector(100, 301, -32236), entered[1].Transform!.Position28);
        ImmutableArray<NativeBattleGraphicsModel> completed = NativeBattleCastingCover.Complete(entered, action);
        Assert.Equal(new NativeBattleVector(100, 301, 32500), completed[1].Transform!.Position28);
        Assert.Equal((ushort)0x241, completed[1].ResourceFlags44);
    }

    [Fact]
    public void Unknown_retained_recipient_or_geometry_is_not_fabricated()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        Assert.Throws<NotSupportedException>(() => NativeBattleCastingCover.Enter(models, Action(1, 8)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCastingCover.Enter(models.SetItem(1, models[1] with { Transform = null }), Action(1, 4)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCastingCover.Complete(models.SetItem(2, models[2] with { ResourceFlags44 = null }), Action(1, 4)));
    }

    private static NativeBattleActionGraphicsState Action(byte source, ushort targets) => new(0, 0, 0, targets, (ushort)(1 << source), source, 2, 0, null, 0x50A9A0);

    private static ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3) { ResourceFlags44 = 0x41, Transform = new(new((short)(100 + index), (short)(500 + index), (short)(200 + index)), new((short)(400 + index), (short)(300 + index), (short)(600 + index)), 80, 4096, 4096) }).ToImmutableArray();
}
